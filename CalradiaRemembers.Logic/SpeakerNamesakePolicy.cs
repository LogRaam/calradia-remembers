// Code written by Gabriel Mailhot, 15/09/2026.
// When the player says the name of the person standing in front of them, they mean that person.
//
// ContextualNameResolver already refuses to list the speaker as a third party. What it did not refuse was
// the speaker's NAMESAKES: fkasad (Nexus, 15/09/2026) was talking to a companion named Erevan, wrote
// "Erevan", and the prompt answered with two other Erevans he had never met - "not relevant at all", his
// words - and invited the NPC to ask which one was meant. The man being addressed was asked to identify
// himself.
//
// The rule is about EVIDENCE, not about names. A candidate found ONLY through words that also name the
// speaker is not a third party; the player was addressing the speaker. One extra word is enough to make
// it a real reference again - "Erevan the Brewer" matches on "brewer" too, which the speaker's name does
// not carry, so the Brewer survives while the bare namesake does not.
//
// Deliberately measured against the speaker's PERSONAL name only, never their clan or culture. Matching on
// a clan name is how a player asks about a house, and that question should still find the house.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a hero matched in the player's message is merely a namesake of the character
    ///   being spoken to. Pure and engine-free: the caller hands in the tokens that produced the match
    ///   and the tokens of the speaker's own name.
    /// </summary>
    public static class SpeakerNamesakePolicy
    {
        /// <summary>
        ///   True when every word that found this candidate also names the speaker, so the player was
        ///   addressing the speaker rather than naming somebody else. False when the match rests on any
        ///   word the speaker's name does not carry, and false when there is no evidence either way -
        ///   an empty match or an unnamed speaker drops nothing.
        /// </summary>
        public static bool IsMerelyANamesake(
            IEnumerable<string> matchedTokens,
            IReadOnlyCollection<string> speakerNameTokens)
        {
            if (matchedTokens == null || speakerNameTokens == null || speakerNameTokens.Count == 0) return false;

            var matched = matchedTokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            if (matched.Count == 0) return false;

            var speaker = new HashSet<string>(speakerNameTokens, StringComparer.OrdinalIgnoreCase);

            return matched.All(speaker.Contains);
        }
    }
}
