// Code written by Gabriel Mailhot, 05/06/2026.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
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
      ///   A first-person phrase for where the letter's writer currently is, or null when it cannot be
      ///   read with confidence (the prompt then falls back to its stale-location guard rather than have
      ///   the model guess). Grounds the letter so an NPC on the march no longer writes as if still in the
      ///   town where the player last met them (player-reported: a correspondent kept placing herself in a
      ///   settlement both parties had long since left for a campaign).
      /// </summary>
      internal static string WhereaboutsFor(NpcProfile profile)
      {
         if (profile == null) return null;
         try
         {
            Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);

            return WhereaboutsForHero(sender);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   A first-person phrase for where a hero currently is, or null when it cannot be read with confidence.
      ///   Handles a hero IN a settlement (visiting or residing), one travelling with a party or army, and a
      ///   settled notable at their own home (a lord's lady with no party, previously read as "uncertain", so
      ///   her letters carried no place at all, which a player noticed as the location grounding "not working").
      /// </summary>
      private static string WhereaboutsForHero(Hero hero)
      {
         if (hero == null) return null;

         var settlement = hero.CurrentSettlement ?? hero.StayingInSettlement;
         if (settlement != null) return $"within {settlement.Name}";

         MobileParty party = hero.PartyBelongedTo;
         if (party != null)
            return party.Army != null
               ? "on campaign with the army, in the field"
               : "in the field, on the march with your own retinue";

         // No settlement and no party: a settled notable/lady is at her own hall.
         if (hero.HomeSettlement != null) return $"at home in {hero.HomeSettlement.Name}";

         return null;
      }

      /// <summary>
      ///   A line placing the PLAYER for the letter's writer, so a spouse or partner does not write "come home,
      ///   you have been gone" while the player is standing in the same hall, nor speak as though the player is
      ///   somewhere they are not. Says the player is HERE WITH the writer when they share a settlement (and
      ///   forbids summoning them home in that case), else where the player is, else null (say nothing).
      /// </summary>
      private static string PlayerSituationFor(NpcProfile profile)
      {
         try
         {
            Hero player = Hero.MainHero;
            if (player == null || profile == null) return null;

            Hero sender = Hero.FindFirst(h => h != null && h.StringId == profile.Id);
            var senderSettlement = sender?.CurrentSettlement ?? sender?.StayingInSettlement;
            var playerSettlement = player.CurrentSettlement ?? player.StayingInSettlement;

            if (senderSettlement != null && senderSettlement == playerSettlement)
               return $"The player is HERE WITH YOU, within {playerSettlement.Name}: they are present, not away. Do " +
                      "NOT tell them to come home, do not ask them to write to you, and do not speak as though they " +
                      "are far off. Speak to their being at your side.";

            string playerWhere = WhereaboutsForHero(player);
            if (!string.IsNullOrWhiteSpace(playerWhere))
               return $"The player is presently {playerWhere}, apart from you.";

            return null;
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   Diagnostic (console <c>cr.letter_where</c>): generates ONE initial letter and hands the writer's
      ///   resolved whereabouts and the letter body to <paramref name="onWritten" /> for logging, WITHOUT
      ///   queuing a courier or mutating the profile. Used to verify a letter grounds to the writer's REAL
      ///   location rather than a stale town planted in their memory. The callback fires on a background
      ///   thread once the LLM returns.
      /// </summary>
      public static void GenerateForDiagnostic(
         NpcProfile profile, LetterReason reason, string triggerContext,
         LlmServices llmServices, Action<string, string> onWritten)
      {
         if (profile == null || llmServices?.ChatService == null)
         {
            onWritten?.Invoke(null, null);

            return;
         }

         _ = RunDiagnosticAsync(profile, reason, triggerContext, llmServices, onWritten);
      }

      private static async Task RunDiagnosticAsync(
         NpcProfile profile, LetterReason reason, string triggerContext,
         LlmServices llmServices, Action<string, string> onWritten)
      {
         string whereabouts = WhereaboutsFor(profile);
         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string message = LetterPromptBuilder.BuildInitialLetterMessage(profile, reason, triggerContext, playerName, whereabouts);

         string content = await CallLlmForLetterAsync(llmServices, profile, message).ConfigureAwait(false);
         onWritten?.Invoke(whereabouts, content);
      }

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
            // Build the prompt from a SNAPSHOT taken here on the calling thread. The off-thread
            // ChatAsync below enumerates the profile's lists (events, letters, relationships); the
            // main thread mutates those same lists from ticks, world-event feeders and ProfileMutator
            // (e.g. right after a letter reply is queued). Enumerating a live list while another thread
            // mutates it is what threw "Collection was modified" during letter reply — the snapshot is
            // never mutated, so the race is gone.
            NpcProfile promptProfile = CloneForPrompt(profile);

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
                     promptProfile, world, emptySession, generationMessage,
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
      ///   A deep copy of the profile for read-only prompt building, so the off-thread LLM call never
      ///   enumerates lists the main thread is mutating. Falls back to the live profile if the clone
      ///   fails (no worse than the previous behaviour).
      /// </summary>
      private static NpcProfile CloneForPrompt(NpcProfile profile)
      {
         try { return JsonConvert.DeserializeObject<NpcProfile>(JsonConvert.SerializeObject(profile)) ?? profile; }
         catch { return profile; }
      }

      /// <summary>
      ///   Delivery delay in days, based on travel distance between the NPC and the
      ///   player. Minimum 1 day, maximum 12 days.
      /// </summary>
      /// <summary>Map distance a courier covers in one day — the single distance-to-days conversion (Tuning).</summary>
      private const float DistancePerTravelDay = 20f;

      /// <summary>
      ///   Raw courier travel time in DAYS from <paramref name="sender" /> to the player, or -1 when a position
      ///   cannot be read. The one place distance becomes days: <see cref="ComputeDeliveryDays" /> clamps it for
      ///   a courier's journey, and the letter-presence gate compares it to a day-count buffer (a time-in-days
      ///   is a far more meaningful distance measure than raw map units).
      /// </summary>
      internal static float TravelDaysTo(Hero sender)
      {
         try
         {
            MobileParty playerParty = MobileParty.MainParty;
            if (sender == null || playerParty == null) return -1f;

            Vec2 playerPos = playerParty.GetPosition2D;

            MobileParty senderParty = sender.PartyBelongedTo;
            if (senderParty != null) return senderParty.GetPosition2D.Distance(playerPos) / DistancePerTravelDay;

            var at = sender.CurrentSettlement ?? sender.StayingInSettlement;
            if (at != null) return at.GetPosition2D.Distance(playerPos) / DistancePerTravelDay;

            return -1f;
         }
         catch
         {
            return -1f;
         }
      }

      private static int ComputeDeliveryDays(Hero giver)
      {
         float days = TravelDaysTo(giver);

         return days < 0f ? 3 : Math.Max(1, Math.Min(12, (int) days));
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
            profile, reason, triggerContext, playerName, WhereaboutsFor(profile), PlayerSituationFor(profile));

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
            profile, playerLetter.Content ?? "", playerName, WhereaboutsFor(profile), PlayerSituationFor(profile));

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
            profile, originalLetter.PlayerReply ?? "", originalLetter.Reason, playerName, WhereaboutsFor(profile), PlayerSituationFor(profile));

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