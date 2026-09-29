// Code written by Gabriel Mailhot, 20/08/2026.
// Player report (Desporion): he proposed marriage to his daughter by letter, the terms were "agreed" over
// three letters, then he denied it all when the player spoke to him in person. LetterPopupManager.MarkReceived
// recorded only a GENERIC event ("Received a letter from X (Reason)"), never the letter's substance, so the
// live chat prompt (which reads the NPC's own profile.Events) had no idea a marriage was proposed and the
// NPC contradicted his own letters. This mirrors the two-stage captive-scene/conversation memory pattern
// (ConversationSummaryService + CaptiveSceneSummarizer/ConversationSummarizer): a synchronous base memory is
// ALWAYS recorded first (guaranteed capture even if the async call fails or the game closes before it
// returns), then, only for substantive reasons, an async first-person LLM summary of the letter's actual
// content replaces that base event by value.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Records a letter's SUBSTANCE, not just its arrival, into the sender NPC's own memory. Single entry
    ///   point shared by every site that delivers an NPC-authored letter to the player.
    /// </summary>
    public static class LetterMemoryFinalizer
    {
        /// <summary>
        ///   Records the base memory synchronously on <paramref name="profile" /> (always, so the fallback line
        ///   is guaranteed), then, when <see cref="LetterMemoryPolicy.ShouldRemember" /> allows it, fires an
        ///   async LLM summary of <paramref name="content" /> that REPLACES the base event by value once it
        ///   returns. <paramref name="npcIsSender" /> is true when <paramref name="profile" /> is the letter's
        ///   own author (the only direction wired today: an NPC-authored letter, whether an initial letter or
        ///   a <see cref="LetterReason.PlayerLetterReply" />, arriving to the player via
        ///   <see cref="LetterPopupManager.MarkReceived" />). The caller persists <paramref name="profile" />
        ///   again afterward if it goes on to mutate it further (matches the existing MarkReceived flow).
        /// </summary>
        public static void RecordLetterMemory(
            NpcProfile profile,
            LetterReason reason,
            string content,
            bool npcIsSender,
            int today,
            NpcMemoryCampaignBehavior behavior,
            string writtenBy = null)
        {
            if (profile == null || behavior?.Store == null) return;

            // Gabriel (28/09/2026): a letter the lord had written in their name is still theirs to remember.
            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
            string baseSummary = LetterMemoryPolicy.BaseMemory(reason, profile.Name, playerName, npcIsSender, dictated: npcIsSender && !string.IsNullOrWhiteSpace(writtenBy));

            // Identified, because the enrichment below comes back for this exact event once the model has
            // read the letter — and two letters from the same person on one day are identical by value.
            NotableEvent baseEvent = new NotableEvent(today, NotableEventType.Other, baseSummary).Identified();
            profile.Events.Add(baseEvent);
            behavior.Store.Set(profile);
            ModLog.Info($"[LETTER MEMORY] base event recorded for {profile.Name}: \"{baseSummary}\"");

            if (!LetterMemoryPolicy.ShouldRemember(reason)) return;
            if (string.IsNullOrWhiteSpace(content) || behavior.LlmServices?.HousekeepingClient == null) return;

            _ = GenerateAndApplyAsync(behavior, profile.Id, baseEvent, content, npcIsSender, playerName, writtenBy);
        }

        /// <summary>
        ///   The sender's memory of writing <paramref name="letter" />, recorded ONCE: at dispatch since 26/09/2026
        ///   (raphareish: a letter lost on the road left its sender no memory of it, so asked about it they said
        ///   they had "been busy"), else on delivery for a letter an older save queued before this existed.
        /// </summary>
        public static void RememberSending(NpcProfile profile, PendingLetter letter, int today, NpcMemoryCampaignBehavior behavior)
        {
            if (profile == null || letter == null || letter.SenderRemembers) return;

            letter.SenderRemembers = true;
            RecordLetterMemory(profile, letter.Reason, letter.Content, true, today, behavior, letter.WrittenBy);
        }

        #region private

        private static async Task GenerateAndApplyAsync(
            NpcMemoryCampaignBehavior behavior,
            string profileId,
            NotableEvent baseEvent,
            string content,
            bool npcIsSender,
            string playerName,
            string writtenBy)
        {
            try
            {
                NpcProfile npc = behavior.Store.Get(profileId);

                if (npc == null) return;

                var summarizer = new LetterMemorySummarizer(behavior.LlmServices.HousekeepingClient);

                string rich;
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    rich = await Task.Run(() => summarizer.SummarizeAsync(
                                        npc, content, npcIsSender, playerName, ModSettings.ReplyLanguage, cts.Token, writtenBy))
                                     .ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(rich)) return;

                // Re-fetch fresh and replace the base event, found by its IDENTITY rather than by an equal
                // value: two letters from the same person on the same day are byte-identical as anchors.
                NpcProfile fresh = behavior.Store.Get(profileId);

                if (fresh == null) return;
                int idx = fresh.IndexOfAnchor(baseEvent.Id, baseEvent);

                if (idx < 0) return;

                fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
                behavior.Store.Set(fresh);
                ModLog.Info($"[LETTER MEMORY] rich summary applied for {npc.Name}: \"{rich.Trim()}\"");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[LETTER MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}
