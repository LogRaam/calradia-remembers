// Code written by Gabriel Mailhot, 07/08/2026.
// The letter archive keeps a player's whole correspondence with a contact so it can be read back later
// as a dated thread. By design the retained history is UNBOUNDED (2026-08-24, Gabriel): losing an old
// letter has no value to the player, so nothing is ever evicted, and the byte[] JSON profile store carries
// the full history (it chunks past the native-string ceiling that a plain string blob would hit). This
// policy is now only membership: whether a given letter counts as archived correspondence (delivered, not
// lost, with a body) and whether a contact has any correspondence at all, which gates the "Letters" entry
// on that hero's encyclopedia page and the in-letter correspondence panel.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>Archive-membership rules for a contact's letter correspondence (retention is unbounded by design).</summary>
    public static class LetterArchivePolicy
    {
        /// <summary>
        ///   An NPC-&gt;player letter counts as archived correspondence: delivered, not lost, with a body, and READ: a letter
        ///   still waiting in the letter box, or thrown away unread, is not in the book (Gabriel, 30/10/2026: "une lettre lue
        ///   est enlevee de la boite aux lettres"; a letter readable in the book while still counted in the box would let
        ///   the count drift).
        /// </summary>
        public static bool IsArchived(PendingLetter letter)
            => letter != null && letter.IsDelivered && !letter.IsLost && !letter.InBox && !letter.ThrownAway && !string.IsNullOrWhiteSpace(letter.Content);

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
    }
}
