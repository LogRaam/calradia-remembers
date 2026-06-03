// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 12b: NPC-initiated letter correspondence — daily delivery check.

#region

using System.Linq;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;

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

                PendingLetter due = profile.SentLetters
                    .FirstOrDefault(l => !l.IsDelivered && l.DeliveryDay <= today);

                if (due == null) continue;

                ModLog.Info($"[LETTER] delivering: {profile.Name} → player (reason={due.Reason}).");
                LetterPopupManager.Show(due, profile);
                return; // one popup per daily tick
            }
        }
    }
}
