// Code written by Gabriel Mailhot, 07/08/2026.
// The letter archive keeps a player's whole correspondence with a contact so it can be read back later
// as a dated thread. Retained letters live in the byte[] JSON profile store, which has a native-string
// size ceiling, so the retained history MUST be bounded: this policy decides which delivered letters to
// drop once a contact's archive grows past the cap (the oldest go first), and never touches a letter that
// is still in flight, waylaid, or blank (pruning one of those would corrupt the delivery pipeline, not
// the archive). It also answers whether a contact has any correspondence at all, which gates the
// "Letters" button on that hero's encyclopedia page.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Retention cap + archive-membership rules for a contact's letter correspondence.</summary>
    public static class LetterArchivePolicy
    {
        /// <summary>
        ///   Maximum delivered letters retained per contact (across both directions). Generous for real play,
        ///   yet a hard bound so the profile blob cannot grow without limit in a very long correspondence.
        ///   When exceeded, the oldest delivered letters are dropped.
        /// </summary>
        public const int PerContactCap = 30;

        /// <summary>An NPC-&gt;player letter counts as archived correspondence: delivered, not lost, with a body.</summary>
        public static bool IsArchived(PendingLetter letter)
            => letter != null && letter.IsDelivered && !letter.IsLost && !string.IsNullOrWhiteSpace(letter.Content);

        /// <summary>A player-&gt;NPC letter counts as archived correspondence: delivered, not lost, with a body.</summary>
        public static bool IsArchived(PlayerLetter letter)
            => letter != null && letter.IsDelivered && !letter.IsLost && !string.IsNullOrWhiteSpace(letter.Content);

        /// <summary>True when at least one delivered letter exists in either direction (gates the archive button).</summary>
        public static bool HasCorrespondence(IEnumerable<PendingLetter> sent, IEnumerable<PlayerLetter> received)
        {
            if (sent != null && sent.Any(IsArchived)) return true;
            if (received != null && received.Any(IsArchived)) return true;

            return false;
        }

        /// <summary>
        ///   Chooses the delivered letters to drop so the retained archive holds at most <paramref name="cap" />
        ///   letters across both directions, dropping the OLDEST first. Only archived (delivered, not lost, non-blank)
        ///   letters are ever eligible: an in-flight, waylaid, or blank-placeholder letter is not counted and never
        ///   selected, so the delivery pipeline that owns those is untouched. Returns the exact letter objects to
        ///   remove from each list; the caller does the removal and persists.
        /// </summary>
        public static (IReadOnlyList<PendingLetter> Incoming, IReadOnlyList<PlayerLetter> Outgoing) SelectPrunable(
            IReadOnlyList<PendingLetter> sent, IReadOnlyList<PlayerLetter> received, int cap)
        {
            var incoming = new List<PendingLetter>();
            var outgoing = new List<PlayerLetter>();
            if (cap <= 0) return (incoming, outgoing);

            var archived = new List<(int Day, PendingLetter In, PlayerLetter Out)>();

            if (sent != null)
                foreach (PendingLetter l in sent)
                    if (IsArchived(l))
                        archived.Add((ArchiveDay(l.SentOnDay, l.DeliveryDay), l, null));

            if (received != null)
                foreach (PlayerLetter l in received)
                    if (IsArchived(l))
                        archived.Add((ArchiveDay(l.SentOnDay, l.DeliveryDay), null, l));

            int excess = archived.Count - cap;
            if (excess <= 0) return (incoming, outgoing);

            // OrderBy is stable, so among letters of the same day the ones added first (NPC letters, then player
            // letters, both in list order) are dropped first, a deterministic oldest-first eviction.
            foreach ((int _, PendingLetter In, PlayerLetter Out) in archived.OrderBy(e => e.Day).Take(excess))
            {
                if (In != null) incoming.Add(In);
                else if (Out != null) outgoing.Add(Out);
            }

            return (incoming, outgoing);
        }

        private static int ArchiveDay(int sentOnDay, int deliveryDay)
            => sentOnDay > 0 ? sentOnDay : deliveryDay;
    }
}
