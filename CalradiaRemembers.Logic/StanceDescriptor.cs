// Code written by Gabriel Mailhot, 21/06/2026.
// Turns an NpcStance into the natural-language posture block the prompt injects, so the NPC speaks in
// keeping with every axis at once — and any believable COMBINATION reads correctly ("dislikes you, yet
// respects and fears you"). Pure so the tier thresholds, and the composition of combinations, stay
// protected by tests. Only non-neutral axes are voiced, keeping the block tight.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Reads an <see cref="NpcStance"/> into prompt-ready posture lines, one per axis that matters.</summary>
    public static class StanceDescriptor
    {
        private const int Strong = 40;
        private const int Mild = 15;

        /// <summary>How they regard you in warmth, or null when neither fond nor hostile.</summary>
        public static string AffectionPhrase(int a)
        {
            if (a >= Strong) return "They are genuinely fond of you.";
            if (a >= Mild) return "They warm to you.";
            if (a <= -Strong) return "They bear you real hatred.";
            if (a <= -Mild) return "They dislike you.";
            return null;
        }

        /// <summary>How far they trust your word, or null when neither trusting nor wary.</summary>
        public static string TrustPhrase(int t)
        {
            if (t >= Strong) return "They trust you implicitly.";
            if (t >= Mild) return "They trust your word.";
            if (t <= -Strong) return "They deeply distrust you.";
            if (t <= -Mild) return "They are wary of your word.";
            return null;
        }

        /// <summary>How much they esteem your worth, or null when neither impressed nor contemptuous.</summary>
        public static string RespectPhrase(int r)
        {
            if (r >= Strong) return "They hold you in high regard.";
            if (r >= Mild) return "They respect your worth.";
            if (r <= -Strong) return "They hold you in open contempt.";
            if (r <= -Mild) return "They think little of you.";
            return null;
        }

        /// <summary>How much they fear you, or null when unafraid.</summary>
        public static string FearPhrase(int f)
        {
            if (f >= 60) return "They are openly afraid of you.";
            if (f >= 30) return "They are wary and intimidated by you.";
            if (f >= 10) return "They feel a flicker of unease around you.";
            return null;
        }

        /// <summary>
        ///   The posture block for the prompt — one line per axis that is not neutral, in a stable
        ///   order (warmth, trust, respect, fear). Null when the NPC is wholly neutral, so the block is
        ///   skipped entirely. Believable combinations compose naturally from the per-axis lines.
        /// </summary>
        public static string Summarize(NpcStance stance)
        {
            if (stance == null) return null;

            var lines = new List<string>();
            Add(lines, AffectionPhrase(stance.Affection));
            Add(lines, TrustPhrase(stance.Trust));
            Add(lines, RespectPhrase(stance.Respect));
            Add(lines, FearPhrase(stance.Fear));

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        private static void Add(List<string> lines, string line)
        {
            if (line != null) lines.Add(line);
        }
    }
}
