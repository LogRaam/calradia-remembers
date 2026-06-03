// Code written by Gabriel Mailhot, 03/06/2026.
// Sprint 12d: player-to-NPC letter correspondence — creation and delivery pipeline.

#region

using System;
using System.Linq;
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
        private const float MapUnitsPerDay  = 20f;
        private const int   MinDeliveryDays = 1;
        private const int   MaxDeliveryDays = 12;

        /// <summary>
        ///   Creates a new player letter, computes the delivery delay based on distance,
        ///   appends it to the recipient's profile, persists, and returns the delivery
        ///   day count for use in the player notification.
        /// </summary>
        public static int CreateAndQueue(NpcProfile recipientProfile, string content,
            int today, BannerlordNpcMemoryStore store)
        {
            int deliveryDays = ComputeDeliveryDays(recipientProfile);
            var letter = new PlayerLetter {
                RecipientId   = recipientProfile.Id,
                RecipientName = recipientProfile.Name,
                Content       = content,
                SentOnDay     = today,
                DeliveryDay   = today + deliveryDays
            };

            recipientProfile.ReceivedPlayerLetters.Add(letter);
            store.Set(recipientProfile);
            ModLog.Info($"[PLAYER LETTER] queued to {recipientProfile.Name} — " +
                        $"delivery day {today + deliveryDays} ({deliveryDays}d).");
            return deliveryDays;
        }

        /// <summary>
        ///   Daily pass: marks in-transit letters as delivered when their
        ///   <see cref="PlayerLetter.DeliveryDay" /> has arrived. No popup —
        ///   the player discovers the effect at their next conversation with the NPC.
        /// </summary>
        public static void CheckDeliveries(BannerlordNpcMemoryStore store, int today)
        {
            if (store == null) return;

            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile?.ReceivedPlayerLetters == null
                    || profile.ReceivedPlayerLetters.Count == 0) continue;

                bool changed = false;
                foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
                {
                    if (letter.IsDelivered || letter.DeliveryDay > today) continue;
                    letter.IsDelivered = true;
                    changed = true;
                    ModLog.Info($"[PLAYER LETTER] delivered to {profile.Name} " +
                                $"(sent day {letter.SentOnDay}).");
                }

                if (changed) store.Set(profile);
            }
        }

        /// <summary>
        ///   Marks all delivered-but-unread letters on a profile as read. Called after
        ///   the NPC's first dialogue response that follows delivery, so the letter
        ///   section is not re-injected into subsequent prompts.
        /// </summary>
        public static void MarkLettersRead(NpcProfile profile, BannerlordNpcMemoryStore store)
        {
            if (profile?.ReceivedPlayerLetters == null) return;

            bool changed = false;
            foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
            {
                if (!letter.IsDelivered || letter.HasBeenRead) continue;
                letter.HasBeenRead = true;
                changed = true;
            }

            if (changed)
            {
                store?.Set(profile);
                ModLog.Info($"[PLAYER LETTER] marked letters read for {profile.Name}.");
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static int ComputeDeliveryDays(NpcProfile profile)
        {
            try
            {
                Hero recipient = Hero.FindFirst(h => h.StringId == profile.Id);
                MobileParty recipientParty = recipient?.PartyBelongedTo;
                MobileParty playerParty    = MobileParty.MainParty;

                if (recipientParty == null || playerParty == null) return 3;

                float dist = playerParty.GetPosition2D.Distance(recipientParty.GetPosition2D);
                int   days = (int)Math.Ceiling(dist / MapUnitsPerDay);

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
    }
}
