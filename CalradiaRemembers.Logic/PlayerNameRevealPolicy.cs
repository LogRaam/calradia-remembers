// Code written by Gabriel Mailhot, 19/07/2026.
// Whether the player has just told a captor their own name. A brigand never knows a noble's name from any
// register (ratified with Gabriel), so the ONLY way a bandit captor comes to know it is the prisoner giving
// it up in the conversation. When they do, the captor should remember it: this decides the "they gave it up"
// moment. Pure so it can be tested; the mod persists the result onto the captor's profile.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Detects the prisoner naming themselves to their captor.</summary>
    public static class PlayerNameRevealPolicy
    {
        /// <summary>
        ///   True when <paramref name="playerMessage" /> contains the player's own name, matched as a WHOLE
        ///   word and case-insensitively, so "Arwa" is caught in "I am Arwa" or "Arwa, of Clan Thais" but a
        ///   name that merely sits inside a longer word is not. Any name part counts (first name or a
        ///   one-word epithet), because a prisoner may give either. Blank inputs never match.
        /// </summary>
        /// <param name="playerMessage">What the player just typed to the captor.</param>
        /// <param name="playerName">The player's name, possibly several words.</param>
        public static bool MentionsOwnName(string playerMessage, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerMessage) || string.IsNullOrWhiteSpace(playerName)) return false;

            foreach (string part in playerName.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries))
            {
                // A one- or two-letter connector ("of", "de") is not an identifying name; ignore it so a
                // sentence like "speak of it" cannot count as revealing a name of "Arwa of Thais".
                if (part.Length < 3) continue;
                if (ContainsWholeWord(playerMessage, part)) return true;
            }

            return false;
        }

        private static bool ContainsWholeWord(string haystack, string word)
        {
            int from = 0;
            while (true)
            {
                int at = haystack.IndexOf(word, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) return false;

                bool leftOk = at == 0 || !char.IsLetter(haystack[at - 1]);
                int end = at + word.Length;
                bool rightOk = end >= haystack.Length || !char.IsLetter(haystack[end]);
                if (leftOk && rightOk) return true;

                from = at + 1;
            }
        }
    }
}
