// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod's LetterTriggerEvaluator: the anti-spam gate shared by every kind of
// unprompted NPC letter (romantic, spouse, marriage solicitation, tournament/battle congratulations).
// Pure so the cadence — a silent regression here either floods the player with letters or leaves the
// world mute — stays protected by tests.

#region

using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The timing rules for NPC-initiated correspondence: whether an NPC may send another letter
    ///   right now, and how long since they last had reason to (a proxy for the last meeting). Pure
    ///   over the NPC's stored letters and events; the host owns who is eligible and what to write.
    /// </summary>
    public static class LetterCadence
    {
        /// <summary>
        ///   True when this NPC may send a new letter: nothing of theirs is still in transit, and the
        ///   cooldown has elapsed since the most recent letter they sent. The first letter is always
        ///   allowed. Prevents both letter spam and stacking unread mail on the road.
        /// </summary>
        public static bool CanSendLetter(NpcProfile profile, int today, int cooldownDays)
        {
            if (profile?.SentLetters == null || profile.SentLetters.Count == 0) return true;

            // Block while a letter is still on its way; never stack mail in transit. 1.9 (courier audit): a LOST
            // letter never delivers, so it must NOT count as in transit, or a single waylaid courier would gag the
            // NPC's correspondence forever.
            bool inTransit = profile.SentLetters.Any(l => !l.IsDelivered && !l.IsLost && l.DeliveryDay > today);
            if (inTransit) return false;

            // Otherwise enforce the cooldown from the most recently sent letter.
            int lastSent = profile.SentLetters.Max(l => l.SentOnDay);
            return today - lastSent >= cooldownDays;
        }

        /// <summary>
        ///   The world-wide throttle on unprompted daily correspondence: true only when enough days
        ///   have passed since ANY NPC last wrote unprompted. Layered on top of the per-NPC cooldown,
        ///   this bounds the CUMULATIVE flood when the player has many correspondents at once.
        ///   <paramref name="lastGlobalLetterDay"/> is null when none has been sent yet.
        /// </summary>
        public static bool CanSendGlobally(int? lastGlobalLetterDay, int today, int cooldownDays)
            => !lastGlobalLetterDay.HasValue || today - lastGlobalLetterDay.Value >= cooldownDays;

        /// <summary>
        ///   Days since the NPC's most recent notable event (a proxy for "how long since they last
        ///   saw the player"). <see cref="int.MaxValue"/> when they have no recorded history, so a
        ///   "long enough since we last met" gate treats a stranger as maximally overdue.
        /// </summary>
        public static int DaysSinceLastEvent(NpcProfile profile, int today)
        {
            if (profile?.Events == null || profile.Events.Count == 0) return int.MaxValue;
            return today - profile.Events.Max(e => e.gameDay);
        }
    }
}
