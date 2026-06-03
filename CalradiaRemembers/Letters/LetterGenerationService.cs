// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 12b: NPC-initiated letter correspondence.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Generates letter content via the LLM and queues a <see cref="PendingLetter"/>
    ///   on the sending NPC's profile. Reuses the existing <see cref="NpcChatService"/>
    ///   pipeline so the NPC's full personality, history, and romantic context are
    ///   automatically included. Only <c>response.Dialogue</c> is used — no profile
    ///   mutations, no action dispatch, no quest events.
    /// </summary>
    public static class LetterGenerationService
    {
        /// <summary>
        ///   Fire-and-forget entry point for an NPC-initiated letter.
        ///   Generates content asynchronously, then adds the letter to
        ///   <paramref name="profile" /> and persists via <paramref name="store" />.
        /// </summary>
        public static void QueueInitialLetter(
            NpcProfile profile, Hero giverHero, LetterReason reason, string triggerContext,
            LlmServices llmServices, BannerlordNpcMemoryStore store, int today)
        {
            _ = GenerateInitialAsync(profile, giverHero, reason, triggerContext, llmServices, store, today)
                  .ContinueWith(t =>
                  {
                      if (t.Exception != null)
                          ModLog.Error($"[LETTER] generation failed for {profile.Name}: {t.Exception.GetBaseException().Message}");
                  }, TaskScheduler.Default);
        }

        /// <summary>
        ///   Fire-and-forget entry point for the NPC's reply to a player response.
        /// </summary>
        public static void QueueReplyLetter(
            NpcProfile profile, Hero giverHero, PendingLetter originalLetter,
            LlmServices llmServices, BannerlordNpcMemoryStore store, int today)
        {
            _ = GenerateReplyAsync(profile, giverHero, originalLetter, llmServices, store, today)
                  .ContinueWith(t =>
                  {
                      if (t.Exception != null)
                          ModLog.Error($"[LETTER] reply generation failed for {profile.Name}: {t.Exception.GetBaseException().Message}");
                  }, TaskScheduler.Default);
        }

        #region private

        private static async Task GenerateInitialAsync(
            NpcProfile profile, Hero giverHero, LetterReason reason, string triggerContext,
            LlmServices llmServices, BannerlordNpcMemoryStore store, int today)
        {
            if (llmServices?.ChatService == null) return;

            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
            string message = LetterPromptBuilder.BuildInitialLetterMessage(
                profile, reason, triggerContext, playerName);

            string content = await CallLlmForLetterAsync(llmServices, profile, message);
            if (string.IsNullOrWhiteSpace(content)) return;

            int deliveryDays = ComputeDeliveryDays(giverHero);
            var letter = new PendingLetter {
                SenderId    = profile.Id,
                SenderName  = profile.Name,
                Reason      = reason,
                Content     = content,
                SentOnDay   = today,
                DeliveryDay = today + deliveryDays,
                IsDelivered = false
            };

            profile.SentLetters.Add(letter);
            store.Set(profile);
            ModLog.Info($"[LETTER] queued from {profile.Name} ({reason}), arrives in {deliveryDays} day(s).");
        }

        private static async Task GenerateReplyAsync(
            NpcProfile profile, Hero giverHero, PendingLetter originalLetter,
            LlmServices llmServices, BannerlordNpcMemoryStore store, int today)
        {
            if (llmServices?.ChatService == null) return;

            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
            string message = LetterPromptBuilder.BuildReplyLetterMessage(
                profile, originalLetter.PlayerReply, originalLetter.Reason, playerName);

            string content = await CallLlmForLetterAsync(llmServices, profile, message);
            if (string.IsNullOrWhiteSpace(content)) return;

            int deliveryDays = ComputeDeliveryDays(giverHero);
            var reply = new PendingLetter {
                SenderId    = profile.Id,
                SenderName  = profile.Name,
                Reason      = originalLetter.Reason,
                Content     = content,
                SentOnDay   = today,
                DeliveryDay = today + deliveryDays,
                IsDelivered = false
            };

            profile.SentLetters.Add(reply);
            store.Set(profile);
            ModLog.Info($"[LETTER] reply queued from {profile.Name}, arrives in {deliveryDays} day(s).");
        }

        /// <summary>
        ///   Calls the LLM via the existing ChatService pipeline with a fresh empty
        ///   session so the letter-generation exchange is never mixed into the NPC's
        ///   real conversation history. Returns only <c>response.Dialogue</c>.
        /// </summary>
        private static async Task<string> CallLlmForLetterAsync(
            LlmServices llmServices, NpcProfile profile, string generationMessage)
        {
            try
            {
                var emptySession = new ChatSession();
                var world = new WorldState {
                    CurrentDay = (int)CampaignTime.Now.ToDays,
                    Season     = "",
                    ActiveConflicts = ""
                };

                NpcChatResult result;
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    result = await Task.Run(
                        () => llmServices.ChatService.ChatAsync(
                            profile, world, emptySession, generationMessage,
                            EncounterContext.Empty, cts.Token),
                        cts.Token).ConfigureAwait(false);
                }

                if (!result.IsSuccess)
                {
                    ModLog.Error($"[LETTER] LLM call failed: {result.ErrorMessage}");
                    return null;
                }

                return result.Response?.Dialogue;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[LETTER] CallLlmForLetterAsync threw: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///   Delivery delay in days, based on travel distance between the NPC and the
        ///   player. Minimum 1 day, maximum 12 days.
        /// </summary>
        private static int ComputeDeliveryDays(Hero giver)
        {
            try
            {
                MobileParty giverParty  = giver.PartyBelongedTo;
                MobileParty playerParty = MobileParty.MainParty;

                if (giverParty == null && giver.CurrentSettlement != null)
                {
                    // NPC is in a settlement — use settlement position.
                    var playerPos = playerParty?.GetPosition2D;
                    if (playerPos == null) return 3;
                    float dist = giver.CurrentSettlement.GetPosition2D.Distance(playerPos.Value);
                    return Math.Max(1, Math.Min(12, (int)(dist / 20f)));
                }

                if (giverParty == null || playerParty == null) return 3;
                float d = giverParty.GetPosition2D.Distance(playerParty.GetPosition2D);
                return Math.Max(1, Math.Min(12, (int)(d / 20f)));
            }
            catch
            {
                return 3;
            }
        }

        #endregion
    }
}
