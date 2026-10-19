// Code written by Gabriel Mailhot, 10/07/2026.
// Round-table group conversation: when the player opens the floor to the whole group (rather than addressing one
// person), the mod starts a burst where each present NPC weighs in and they can address each other. This is the
// pure detector: reading the player's free text to tell "everyone, speak up" from "so, Rhagaea, what do you think".
// The turn-order and cap arithmetic live in RoundTablePolicy; spawning the actual turns is engine glue (mod-side).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure rules for detecting a round-table "open the floor to everyone" intent in player text.</summary>
    public static class RoundTableIntentDetector
    {
        private static readonly string[] ExplicitPhrases =
        {
            "round table",
            "round-table",
            "tour de table",
            "open the floor",
            "hear you all",
            "hear from everyone",
            "hear from all of you"
        };

        private static readonly string[] GroupTokens =
        {
            "everyone",
            "all of you",
            "you all",
            "you three",
            "you two",
            "the rest of you",
            "tout le monde",
            "vous tous",
            "vous autres",
            "chacun",
            "chacune"
        };

        private static readonly string[] SolicitationCues =
        {
            "think",
            "thoughts",
            "opinion",
            "opinions",
            "views",
            "view",
            "say",
            "speak",
            "weigh in",
            "your take",
            "pensez",
            "pense",
            "avis",
            "dites",
            "dire",
            "parlez",
            "parler",
            "qu en pensez",
            "en pensez"
        };

        /// <summary>
        ///   True when the player's message is opening the floor to the whole group (so the mod can start a
        ///   round-table where each present NPC weighs in), as opposed to addressing one person.
        /// </summary>
        public static bool Detects(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var lowered = message.ToLowerInvariant();

            if (ContainsAny(lowered, ExplicitPhrases))
                return true;

            return ContainsAny(lowered, GroupTokens) && ContainsAny(lowered, SolicitationCues);
        }

        /// <summary>
        ///   True when any needle appears as a WHOLE WORD (or whole phrase) in the haystack. Round-table
        ///   audit D8: plain substring matching fired on words that merely contain a cue, and every false
        ///   positive costs a burst of up to three extra LLM calls. "Everyone says the roads are dangerous"
        ///   matched the group token "everyone" plus the cue "say" hiding inside "says", and in French
        ///   "la direction du camp" matched "dire" inside "direction". A cue must be a word the player
        ///   actually used, not a fragment of another one.
        /// </summary>
        private static bool ContainsAny(string haystack, string[] needles)
        {
            for (var i = 0; i < needles.Length; i++)
                if (ContainsWord(haystack, needles[i]))
                    return true;

            return false;
        }

        private static bool ContainsWord(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return false;

            int from = 0;
            while (from <= haystack.Length - needle.Length)
            {
                int at = haystack.IndexOf(needle, from, StringComparison.Ordinal);
                if (at < 0) return false;

                bool leftClear = at == 0 || !char.IsLetterOrDigit(haystack[at - 1]);
                int after = at + needle.Length;
                bool rightClear = after >= haystack.Length || !char.IsLetterOrDigit(haystack[after]);

                if (leftClear && rightClear) return true;

                from = at + 1;
            }

            return false;
        }
    }
}
