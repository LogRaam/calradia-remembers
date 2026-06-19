// Code written by Gabriel Mailhot, 05/06/2026.

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
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Generates letter content via the LLM and queues a <see cref="PendingLetter" />
   ///   on the sending NPC's profile. Reuses the existing <see cref="NpcChatService" />
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
         NpcProfile profile,
         Hero giverHero,
         LetterReason reason,
         string triggerContext,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
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
         NpcProfile profile,
         Hero giverHero,
         PendingLetter originalLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         _ = GenerateReplyAsync(profile, giverHero, originalLetter, llmServices, store, today)
            .ContinueWith(t =>
            {
               if (t.Exception != null)
                  ModLog.Error($"[LETTER] reply generation failed for {profile.Name}: {t.Exception.GetBaseException().Message}");
            }, TaskScheduler.Default);
      }

      /// <summary>
      ///   Fire-and-forget entry point triggered when a player letter is delivered.
      ///   The LLM decides whether to reply; if yes, it picks a delay (1-7 days) and
      ///   writes the letter body. A <see cref="PendingLetter" /> is stored with
      ///   <see cref="PendingLetter.DispatchDay" /> set; the reply courier is spawned
      ///   on the main-thread daily tick once that day arrives.
      /// </summary>
      public static void QueueReplyToPlayerLetter(
         NpcProfile profile,
         Hero senderHero,
         PlayerLetter playerLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         _ = GeneratePlayerLetterReplyAsync(profile, senderHero, playerLetter, llmServices, store, today)
            .ContinueWith(t =>
            {
               if (t.Exception != null)
                  ModLog.Error($"[LETTER] player-letter reply generation failed for {profile.Name}: {t.Exception.GetBaseException().Message}");
            }, TaskScheduler.Default);
      }

      #region private

      /// <summary>
      ///   Calls the LLM via the existing ChatService pipeline with a fresh empty
      ///   session so the letter-generation exchange is never mixed into the NPC's
      ///   real conversation history. Returns only <c>response.Dialogue</c>.
      /// </summary>
      private static async Task<string> CallLlmForLetterAsync(
         LlmServices llmServices,
         NpcProfile profile,
         string generationMessage)
      {
         try
         {
            var emptySession = new ChatSession();
            var world = new WorldState {
               CurrentDay = (int) CampaignTime.Now.ToDays,
               Season = "",
               ActiveConflicts = ""
            };

            NpcChatResult result;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
               CancellationToken token = cts.Token;
               result = await Task.Run(
                  () => llmServices.ChatService.ChatAsync(
                     profile, world, emptySession, generationMessage,
                     EncounterContext.Empty, token),
                  token).ConfigureAwait(false);
            }

            if (result.IsSuccess) return result.Response?.Dialogue;

            ModLog.Error($"[LETTER] LLM call failed: {result.ErrorMessage}");

            return null;
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
            MobileParty giverParty = giver.PartyBelongedTo;
            MobileParty playerParty = MobileParty.MainParty;

            if (giverParty == null && giver.CurrentSettlement != null)
            {
               // NPC is in a settlement — use settlement position.
               Vec2? playerPos = playerParty?.GetPosition2D;

               if (playerPos == null) return 3;
               float dist = giver.CurrentSettlement.GetPosition2D.Distance(playerPos.Value);

               return Math.Max(1, Math.Min(12, (int) (dist / 20f)));
            }

            if (giverParty == null || playerParty == null) return 3;
            float d = giverParty.GetPosition2D.Distance(playerParty.GetPosition2D);

            return Math.Max(1, Math.Min(12, (int) (d / 20f)));
         }
         catch
         {
            return 3;
         }
      }

      private static async Task GenerateInitialAsync(
         NpcProfile profile,
         Hero giverHero,
         LetterReason reason,
         string triggerContext,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         if (llmServices?.ChatService == null) return;

         // Add a placeholder immediately so CanSendLetter sees a letter in-transit while the
         // LLM call is in flight, preventing duplicate letters from consecutive daily ticks.
         var placeholder = new PendingLetter {
            SenderId = profile.Id,
            SenderName = profile.Name,
            Reason = reason,
            Content = null,
            SentOnDay = today,
            DispatchDay = int.MaxValue, // sentinel: never dispatched until LLM fills it in
            DeliveryDay = today + 1,    // marks as in-transit for CanSendLetter during async window
            IsDelivered = false
         };
         profile.SentLetters.Add(placeholder);
         store.Set(profile);

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string message = LetterPromptBuilder.BuildInitialLetterMessage(
            profile, reason, triggerContext, playerName);

         string content = await CallLlmForLetterAsync(llmServices, profile, message);

         if (string.IsNullOrWhiteSpace(content))
         {
            // LLM failed — remove placeholder so the next evaluation can retry.
            profile.SentLetters.Remove(placeholder);
            store.Set(profile);
            return;
         }

         int deliveryDays = ComputeDeliveryDays(giverHero);
         placeholder.Content = content;
         placeholder.DispatchDay = today;           // courier departs as soon as letter is written
         placeholder.DeliveryDay = today + deliveryDays; // fallback if courier not yet spawned on load
         store.Set(profile);
         ModLog.Info($"[LETTER] queued from {profile.Name} ({reason}), courier dispatches day {today}.");
      }

      private static async Task GeneratePlayerLetterReplyAsync(
         NpcProfile profile,
         Hero senderHero,
         PlayerLetter playerLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         if (llmServices?.ChatService == null) return;
         if (string.IsNullOrWhiteSpace(playerLetter.Content)) return;

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string message = LetterPromptBuilder.BuildPlayerLetterReplyDecisionMessage(
            profile, playerLetter.Content ?? "", playerName);

         string rawDecision = await CallLlmForLetterAsync(llmServices, profile, message);

         if (string.IsNullOrWhiteSpace(rawDecision)) return;

         // Parse PASS vs DELAY:N + letter body. Tolerant: default delay on bad format.
         string trimmed = rawDecision.Trim();
         if (trimmed.StartsWith("PASS", StringComparison.OrdinalIgnoreCase)
             || trimmed.Equals("no reply", StringComparison.OrdinalIgnoreCase)
             || trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
         {
            // Log the LLM's reason (everything after "PASS:" on the first line).
            var reason = string.Empty;
            int colon = trimmed.IndexOf(':');
            if (colon >= 0 && colon < trimmed.Length - 1)
               reason = trimmed.Substring(colon + 1).Split('\n')[0].Trim();
            string reasonSuffix = string.IsNullOrEmpty(reason)
               ? ""
               : $" — {reason}";
            ModLog.Info($"[LETTER] {profile.Name} chose not to reply{reasonSuffix}");

            return;
         }

         var delayDays = 2;
         string letterBody = trimmed;

         int firstNewline = trimmed.IndexOf('\n');
         string firstLine = firstNewline > 0
            ? trimmed.Substring(0, firstNewline).Trim()
            : trimmed;

         if (firstLine.StartsWith("DELAY:", StringComparison.OrdinalIgnoreCase))
         {
            string delayStr = firstLine.Substring("DELAY:".Length).Trim();
            if (int.TryParse(delayStr, out int parsed) && parsed is >= 1 and <= 14) delayDays = parsed;
            letterBody = firstNewline > 0
               ? trimmed.Substring(firstNewline).Trim()
               : string.Empty;
         }

         if (string.IsNullOrWhiteSpace(letterBody)) return;

         int dispatchDay = today + delayDays;
         int travelDays = ComputeDeliveryDays(senderHero);

         var reply = new PendingLetter {
            SenderId = profile.Id,
            SenderName = profile.Name,
            Reason = LetterReason.PlayerLetterReply,
            Content = letterBody,
            SentOnDay = today,
            DispatchDay = dispatchDay,
            DeliveryDay = dispatchDay + travelDays, // fallback if courier lost before spawning
            IsDelivered = false
         };

         profile.SentLetters.Add(reply);
         store.Set(profile);
         ModLog.Info($"[LETTER] {profile.Name} will reply to player's letter — courier departs day {dispatchDay} ({delayDays}d delay).");
      }

      private static async Task GenerateReplyAsync(
         NpcProfile profile,
         Hero giverHero,
         PendingLetter originalLetter,
         LlmServices llmServices,
         BannerlordNpcMemoryStore store,
         int today)
      {
         if (llmServices?.ChatService == null) return;

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string message = LetterPromptBuilder.BuildReplyLetterMessage(
            profile, originalLetter.PlayerReply ?? "", originalLetter.Reason, playerName);

         string content = await CallLlmForLetterAsync(llmServices, profile, message);

         if (string.IsNullOrWhiteSpace(content)) return;

         int deliveryDays = ComputeDeliveryDays(giverHero);
         var reply = new PendingLetter {
            SenderId = profile.Id,
            SenderName = profile.Name,
            Reason = originalLetter.Reason,
            Content = content,
            SentOnDay = today,
            DispatchDay = today,
            DeliveryDay = today + deliveryDays,
            IsDelivered = false
         };

         profile.SentLetters.Add(reply);
         store.Set(profile);
         ModLog.Info($"[LETTER] reply queued from {profile.Name}, courier dispatches day {today}.");
      }

      #endregion
   }
}