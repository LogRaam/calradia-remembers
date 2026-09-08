// Code written by Gabriel Mailhot, 07/09/2026.
// Companion-briefing pillar: the player asks a lord, in the vanilla dialogue, for a private word with his own
// people before "A word with you..." continues, then tells whichever companions he drew aside what he wants
// of them. What he says there becomes a standing instruction that rides with the companion into the
// conversation that follows, and ONLY into it: the target of that conversation was never present for the
// briefing and must never learn of it except in character, from the companion's own choice. Gabriel's
// rulings: several briefings STACK rather than replace (a player who forgot something goes back rather than
// starting over), but the accumulated text needs a bound or it grows without limit and eats the prompt
// budget, so the OLDEST material is dropped first, since the most recent instruction is the one the player
// means right now. A brief is attached to the companion, never to the conversation or the target, and it
// expires with the conversation it was actually carried into.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure rules for a companion's standing brief: how a new instruction stacks onto whatever the player
    ///   already told them, the bound on the accumulated text, and which of the currently-briefed companions
    ///   should have their brief cleared when a conversation closes.
    /// </summary>
    public static class CompanionBriefPolicy
    {
        /// <summary>
        ///   The bound on the accumulated brief text, in characters. A standing instruction is read in full on
        ///   EVERY turn of the conversation it rides into (it is not a one-shot cue), so it competes for prompt
        ///   budget like any other always-on section. Generous enough for several stacked instructions of a
        ///   sentence or two each, small enough that a player who keeps going back for "one more word" can
        ///   never quietly balloon a companion's slice of the prompt.
        /// </summary>
        public const int MaxChars = 500;

        private const string Separator = " ";

        /// <summary>
        ///   Trims <paramref name="text" /> down to <see cref="MaxChars" /> from the FRONT (the oldest material),
        ///   on a word boundary where one exists, so the newest instruction at the tail always survives whole:
        ///   the most recent thing the player said is the one he means right now.
        /// </summary>
        private static string Bound(string text)
        {
            if (text.Length <= MaxChars) return text;

            int cutFrom = text.Length - MaxChars;
            int wordBoundary = text.IndexOf(' ', cutFrom);

            return wordBoundary >= 0 && wordBoundary < text.Length - 1
                ? text.Substring(wordBoundary + 1)
                : text.Substring(cutFrom);
        }

        /// <summary>
        ///   Ruling #2/expiry: a brief is attached to the companion, not the conversation or the target, so
        ///   there is no "wrong" conversation for it to spend itself in, whatever conversation the briefed
        ///   companion actually takes part in next IS the one it was for. Returns the briefed companions who
        ///   are also participants of the conversation that just closed, except when that closing conversation
        ///   was the briefing chat itself (the private word): its own participants must not have their
        ///   brand-new brief wiped the instant it is given, before it ever reaches the conversation it was
        ///   meant for.
        /// </summary>
        public static List<string> HeroesToExpireOnClose(
            IEnumerable<string> briefedHeroIds, IEnumerable<string> conversationParticipantHeroIds, bool isBriefingChat)
        {
            if (isBriefingChat || briefedHeroIds == null || conversationParticipantHeroIds == null)
                return new List<string>();

            var participants = new HashSet<string>(conversationParticipantHeroIds);

            return briefedHeroIds.Where(id => !string.IsNullOrEmpty(id) && participants.Contains(id)).ToList();
        }

        /// <summary>
        ///   Appends <paramref name="newInstruction" /> (trimmed) to whatever the companion was already told
        ///   (<paramref name="existing" />, null/empty for a first briefing: ruling #3, several briefings
        ///   STACK rather than replace), then bounds the result (see <see cref="Bound" />). A blank
        ///   <paramref name="newInstruction" /> changes nothing: returns the existing text unchanged (null when
        ///   there was none), rather than let an accidental empty send wipe or pad a standing brief.
        /// </summary>
        public static string Stack(string existing, string newInstruction)
        {
            string trimmedExisting = (existing ?? "").Trim();
            string trimmedNew = (newInstruction ?? "").Trim();

            if (trimmedNew.Length == 0) return trimmedExisting.Length == 0 ? null : trimmedExisting;

            string combined = trimmedExisting.Length == 0
                ? trimmedNew
                : trimmedExisting + Separator + trimmedNew;

            return Bound(combined);
        }
    }
}
