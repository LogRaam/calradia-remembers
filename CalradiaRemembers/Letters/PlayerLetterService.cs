// Code written by Gabriel Mailhot, 04/06/2026.

#region

using System;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Creates and queues letters from the player to NPCs, runs the daily delivery
   ///   check, and marks delivered letters as read after the NPC's first response.
   ///   Symmetric to <see cref="LetterDeliveryService" /> which handles NPC→player flow.
   /// </summary>
   public static class PlayerLetterService
   {
      private const float MapUnitsPerDay = 20f;
      private const int MaxDeliveryDays = 12;
      private const int MinDeliveryDays = 1;

      /// <summary>
      ///   Daily pass. Sprint 12c: letters with a live courier are delivered by
      ///   <see cref="CourierService.TickCouriers" /> on arrival; this method now only
      ///   handles the date-based fallback for letters whose courier never spawned
      ///   (spawn failed or a legacy letter). Lost letters are skipped entirely.
      /// </summary>
      public static void CheckDeliveries(BannerlordNpcMemoryStore store, int today)
      {
         if (store == null) return;

         // Courier-driven delivery (arrival / safety deadline / vanished) runs first.
         CourierService.TickCouriers(store, today);

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile?.ReceivedPlayerLetters == null
                || profile.ReceivedPlayerLetters.Count == 0) continue;

            var changed = false;
            foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
            {
               if (letter.IsDelivered || letter.IsLost) continue;
               if (!string.IsNullOrEmpty(letter.CourierPartyId)) continue; // courier handles it
               if (letter.DeliveryDay > today) continue;

               letter.IsDelivered = true;
               RecordReceipt(profile, letter); // file the durable memory so it survives past the read flag
               changed = true;
               ModLog.Info($"[PLAYER LETTER] delivered (date fallback) to {profile.Name} " + $"(sent day {letter.SentOnDay}).");
            }

            if (changed) store.Set(profile);
         }
      }

      /// <summary>
      ///   Creates a new player letter, computes the delivery delay based on distance,
      ///   appends it to the recipient's profile, persists, and returns the delivery
      ///   day count for use in the player notification.
      /// </summary>
      /// <summary>
      ///   Queues a player letter. Returns whether it is DELIVERABLE (false when the recipient is dead, captured,
      ///   or absent, in which case it is recorded lost and no courier is spawned) and the estimated delivery days
      ///   (1.7 courier audit: the caller charges the fee only when Deliverable is true).
      /// </summary>
      public static (bool Deliverable, int DeliveryDays) CreateAndQueue(NpcProfile recipientProfile,
                                       string content,
                                       int today,
                                       BannerlordNpcMemoryStore store)
      {
         int deliveryDays = ComputeDeliveryDays(recipientProfile);
         var letter = new PlayerLetter {
            RecipientId = recipientProfile.Id,
            RecipientName = recipientProfile.Name,
            Content = content,
            SentOnDay = today,
            DeliveryDay = today + deliveryDays
         };

         recipientProfile.ReceivedPlayerLetters.Add(letter);

         // Sprint 12c: a dead, captured, or vanished recipient cannot receive letters —
         // mark the letter undeliverable up front and spawn no courier.
         Hero recipient = Hero.FindFirst(h => h.StringId == recipientProfile.Id);
         if (!CourierService.CanReceiveLetter(recipient))
         {
            letter.IsLost = true;
            store.Set(recipientProfile);
            ModLog.Info($"[PLAYER LETTER] {recipientProfile.Name} cannot receive letters " + $"(dead/captured/absent) — marked undeliverable.");

            return (false, deliveryDays);
         }

         // If the recipient is one of the player's OWN companions currently away on an errand, a letter is how
         // the player orders them home WITHOUT riding out to meet them. Schedule the verified recall (courier
         // delay): on a later daily tick they actually abandon the errand and rejoin the party — a real deed,
         // not just the letter's words. The letter itself still delivers as normal flavour.
         if (CompanionMissionBehavior.Instance?.IsAway(recipient.StringId) == true)
         {
            (bool sent, string msg) = CompanionMissionBehavior.Instance.OrderRecallByLetter(recipient);
            if (sent) ModLog.Info($"[PLAYER LETTER] recall order to away companion {recipient.Name}: {msg}");
         }

         // Spawn a visible courier carrying this letter. On success the courier drives
         // delivery (on arrival); on spawn failure the date-based fallback delivers. An escorted
         // correspondence thread (the player paid for a guard) rides immune to interception.
         CourierService.TrySpawnCourier(letter, recipient, escorted: recipientProfile.EscortedChannel);

         store.Set(recipientProfile);
         ModLog.Info($"[PLAYER LETTER] queued to {recipientProfile.Name} — " + $"delivery day {today + deliveryDays} ({deliveryDays}d), " + $"courier={letter.CourierPartyId ?? "none"}.");

         return (true, deliveryDays);
      }

      /// <summary>
      ///   Marks all delivered-but-unread letters on a profile as read. Called after
      ///   the NPC's first dialogue response that follows delivery, so the letter
      ///   section is not re-injected into subsequent prompts.
      /// </summary>
      public static void MarkLettersRead(NpcProfile profile, BannerlordNpcMemoryStore store)
      {
         if (profile?.ReceivedPlayerLetters == null) return;

         var changed = false;
         foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
         {
            if (!letter.IsDelivered || letter.HasBeenRead) continue;

            letter.HasBeenRead = true;
            changed = true;
         }

         if (!changed) return;

         store?.Set(profile);
         ModLog.Info($"[PLAYER LETTER] marked letters read for {profile.Name}.");
      }

      /// <summary>
      ///   Files a durable memory on the recipient that they received this letter from the player, so the letter
      ///   is still remembered in a later face-to-face conversation. Without it, a player letter is injected into
      ///   the prompt ONLY while delivered-and-unread and vanishes after the NPC's first reply (player report: the
      ///   NPC later says it is "still awaiting a letter"). Mirrors the NPC->player direction (LetterPopupManager).
      ///   Called once, at delivery; the caller persists the profile.
      /// </summary>
      public static void RecordReceipt(NpcProfile profile, PlayerLetter letter)
      {
         if (profile?.Events == null || letter == null) return;

         var today = (int) CampaignTime.Now.ToDays;
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";
         string snippet = Snippet(letter.Content);
         string summary = string.IsNullOrWhiteSpace(snippet)
            ? $"Received a letter from {player}."
            : $"Received a letter from {player}: \"{snippet}\"";

         profile.Events.Add(new NotableEvent(today, NotableEventType.Other, summary));

         // Retention: the delivered player letter now joins the readable archive; keep it bounded. Mutates the
         // profile in place; every caller of RecordReceipt persists the profile immediately afterward.
         LetterArchiveRetention.EnforceCap(profile);

         ModLog.Info($"[PLAYER LETTER] filed a receipt memory on {profile.Name}.");
      }

      #region private

      /// <summary>A one-line, length-capped preview of the letter body for the receipt memory.</summary>
      private static string Snippet(string content)
      {
         if (string.IsNullOrWhiteSpace(content)) return "";

         string s = content.Trim().Replace("\r", " ").Replace("\n", " ");
         const int max = 140;

         return s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "...";
      }

      // ── Private helpers ───────────────────────────────────────────────────

      private static int ComputeDeliveryDays(NpcProfile profile)
      {
         try
         {
            Hero recipient = Hero.FindFirst(h => h.StringId == profile.Id);
            MobileParty recipientParty = recipient?.PartyBelongedTo;
            MobileParty playerParty = MobileParty.MainParty;

            if (recipientParty == null || playerParty == null) return 3;

            float dist = playerParty.GetPosition2D.Distance(recipientParty.GetPosition2D);
            var days = (int) Math.Ceiling(dist / MapUnitsPerDay);

            return days < MinDeliveryDays ? MinDeliveryDays
               : days > MaxDeliveryDays ? MaxDeliveryDays
               : days;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PLAYER LETTER] ComputeDeliveryDays failed: {ex.Message}");

            return 3;
         }
      }

      #endregion
   }
}