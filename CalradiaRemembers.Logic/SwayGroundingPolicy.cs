// Code written by Gabriel Mailhot, 01/10/2026.
// sway_opinion turns a character's regard against (or toward) a THIRD person "at the player's word". Maifailun
// (Nexus, 01/10/2026): the player said "Have you enjoyed the tournament my lady" and bowed; Lady Bolat turned
// against Lady Decantia (-6), whom nobody had named. The guard lived only in the prompt's wording. A case about
// somebody names them: if the player never spoke of that person in this conversation, there was no case.

#region

using System;
using System.Linq;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether the player actually spoke of the person a sway is about.</summary>
    public static class SwayGroundingPolicy
    {
        /// <summary>Words of a name that say what someone is, not who: "Lady Decantia" is found by "Decantia".</summary>
        private static readonly string[] Titles = {"lady", "lord", "sir", "dame", "king", "queen", "emperor", "empress", "prince", "princess", "khan", "the", "of"};

        /// <summary>
        ///   True when the player's words in this conversation name the target (any part of the name beyond a
        ///   title, as a whole word; a name in a script without spaces is found inside the text). Null words mean
        ///   the path carries no conversation at all, which this rule does not judge.
        /// </summary>
        public static bool PlayerSpokeOf(string playerWords, string targetName)
        {
            if (playerWords == null) return true;
            if (string.IsNullOrWhiteSpace(playerWords) || string.IsNullOrWhiteSpace(targetName)) return false;

            return targetName.Split(new[] {' ', '\t', ',', '.', '\'', '-'}, StringSplitOptions.RemoveEmptyEntries)
                             .Where(part => !Titles.Contains(part.ToLowerInvariant()))
                             .Any(part => Names(playerWords, part));
        }

        private static bool Names(string words, string part)
        {
            bool latin = part.All(c => c < 128);
            if (!latin) return words.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
            if (part.Length < 3) return false;

            return Regex.IsMatch(words, $@"\b{Regex.Escape(part)}\b", RegexOptions.IgnoreCase);
        }
    }
}
