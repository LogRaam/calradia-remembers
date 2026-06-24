// Code written by Gabriel Mailhot, 24/06/2026.
// Pure text/response heuristics for the chat flow, extracted from the (large) ChatViewModel so they can be
// read and validated as unit tests rather than buried in UI orchestration. No engine or UI dependency.

#region

using System;
using System.Linq;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Small pure heuristics over LLM text and parsed responses, used by the conversation flow.</summary>
    public static class DialogueHeuristics
    {
        // Markers that an LLM reply is an out-of-character refusal rather than the character's speech.
        private static readonly string[] RefusalMarkers =
        {
            "i must decline", "i won't generate", "i will not generate", "i won't continue",
            "i cannot continue", "i can't continue", "i won't participate", "i won't write",
            "i will not write", "as an ai", "within appropriate bounds", "against my guidelines",
            "my guidelines", "i don't feel comfortable", "i'm not comfortable", "i am not comfortable",
            "switch to a consensual", "non-consensual sexual", "sexual coercion", "even in fiction",
            "i won't role-play", "i won't roleplay", "i must refuse", "appropriate bounds"
        };

        /// <summary>
        ///   True when the text reads as an out-of-character model refusal rather than the character's
        ///   in-character speech — used to keep a guardrail message from ever being shown under an NPC's name.
        /// </summary>
        public static bool LooksLikeRefusal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.ToLowerInvariant();

            foreach (string marker in RefusalMarkers)
                if (t.Contains(marker))
                    return true;

            return false;
        }

        /// <summary>
        ///   True when the NPC's dialogue ends with a question mark, signalling the other party may now
        ///   respond. Ignores trailing whitespace and a trailing stage direction (<c>*...*</c>).
        /// </summary>
        public static bool NpcAskedQuestion(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return false;
            string stripped = Regex.Replace(dialogue, @"\*[^*]*\*\s*$", "").TrimEnd();

            return stripped.EndsWith("?");
        }

        /// <summary>
        ///   Strips a trailing parenthetical role from a witness name, e.g. "A soldier (one of your men)"
        ///   → "A soldier" — defends against the LLM echoing the full prompt label as the reaction name.
        /// </summary>
        public static string StripWitnessRole(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            int paren = name.IndexOf('(');

            return paren > 0
                ? name.Substring(0, paren).Trim()
                : name.Trim();
        }

        /// <summary>True when the parsed response carries an action of the given type (case-insensitive).</summary>
        public static bool HasAction(ParsedResponse response, string type)
            => response?.Actions != null
            && response.Actions.Any(a => string.Equals(a.Type, type, StringComparison.OrdinalIgnoreCase));
    }
}
