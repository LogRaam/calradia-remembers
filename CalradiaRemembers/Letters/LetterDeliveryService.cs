// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 12b: NPC-initiated letter correspondence — daily delivery check.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Runs on every daily tick, scans all NPC profiles for letters whose
    ///   <see cref="PendingLetter.DeliveryDay" /> has passed, and shows one popup
    ///   per tick via <see cref="LetterPopupManager" />. One letter per day prevents
    ///   the player from being overwhelmed after a long absence.
    /// </summary>
    public static class LetterDeliveryService
    {
        public static void CheckDeliveries(BannerlordNpcMemoryStore store, int today)
        {
            if (store == null) return;

            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile?.SentLetters == null || profile.SentLetters.Count == 0) continue;

                // A longing/loneliness letter must not reach the player while its sender is standing beside them
                // (the "she wrote she misses me while riding in my own party" report): this date-based fallback
                // otherwise delivered it regardless of proximity, defeating the courier deferral. A divorce letter
                // is always delivered, near or not, so the player is never blindsided by an unfolding divorce.
                Hero sender = Hero.FindFirst(h => h != null && h.StringId == id);
                bool near = sender != null && HeroProximity.IsNearPlayer(sender);

                // Skip letters that have an active reply courier — the courier handles delivery.
                // Never deliver a blank letter: an in-flight placeholder whose LLM call failed or was
                // interrupted mid-session keeps Content null, and delivering it showed the player
                // "(The letter is blank.)". A letter with no body is not ready, so it is not due.
                var candidates = profile.SentLetters
                    .Where(l => !l.IsDelivered && !l.IsLost
                             && !string.IsNullOrWhiteSpace(l.Content)
                             && string.IsNullOrEmpty(l.CourierPartyId)
                             && l.DeliveryDay <= today
                             && !LetterDeliveryPolicy.ShouldDeferDelivery(l.Reason, near))
                    .ToList();

                if (candidates.Count == 0) continue;

                PendingLetter due = null;
                foreach (PendingLetter candidate in candidates)
                {
                    // A letter must never outlive the intent that wrote it: re-validate against the sender's
                    // CURRENT intention here and drop it silently if that intention has lapsed (a reconciled
                    // spouse no longer wants a divorce; a lord who made peace no longer threatens war).
                    if (LetterIntentionResolver.ShouldDrop(candidate, sender))
                    {
                        profile.SentLetters.Remove(candidate);
                        store.Set(profile);
                        ModLog.Info($"[LETTER] dropped (sender's intention lapsed): {profile.Name} -> player (reason={candidate.Reason}).");

                        continue;
                    }

                    due = candidate;

                    break;
                }

                if (due == null) continue;

                ModLog.Info($"[LETTER] delivering: {profile.Name} → player (reason={due.Reason}).");
                LetterPopupManager.Show(due, profile);
                return; // one popup per daily tick
            }
        }
    }
}
