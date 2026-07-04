// Code written by Gabriel Mailhot, 21/06/2026.
// Turns an NpcStance into the natural-language posture block the prompt injects, so the NPC speaks in
// keeping with every axis at once — and any believable COMBINATION reads correctly ("you dislike them, yet
// respect and fear them"). Pure so the tier thresholds, and the composition of combinations, stay
// protected by tests. Only non-neutral axes are voiced, keeping the block tight.
//
// SECOND PERSON, addressed to the NPC ("You are afraid of the player") — NOT third person. The whole prompt
// frames the model as "You are [NPC]", so a third-person note ("They are afraid of you") clashes pronouns
// and lets the model FLIP the feeling onto the player (observed: a feared lord told the player THEY looked
// afraid). Keeping the note in the same person as the prompt makes the direction unflippable.
//
// Updated 04/07/2026 (P0.2, affect-coherence reform): Summarize() no longer voices Affection — that value
// is already stated once, in words, by the prompt's CURRENT STANCE line, so this block is Trust/Respect/Fear
// only. Prevents the same ReputationWithPlayer number from being read aloud twice in one prompt.

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

        /// <summary>How you regard the player in warmth, or null when neither fond nor hostile.</summary>
        public static string AffectionPhrase(int a)
        {
            if (a >= Strong) return "You are genuinely fond of the player.";
            if (a >= Mild) return "You warm to the player.";
            if (a <= -Strong) return "You bear the player real hatred.";
            if (a <= -Mild) return "You dislike the player.";
            return null;
        }

        /// <summary>How far you trust the player's word, or null when neither trusting nor wary.</summary>
        public static string TrustPhrase(int t)
        {
            if (t >= Strong) return "You trust the player implicitly.";
            if (t >= Mild) return "You trust the player's word.";
            if (t <= -Strong) return "You deeply distrust the player.";
            if (t <= -Mild) return "You are wary of the player's word.";
            return null;
        }

        /// <summary>How much you esteem the player's worth, or null when neither impressed nor contemptuous.</summary>
        public static string RespectPhrase(int r)
        {
            if (r >= Strong) return "You hold the player in high regard.";
            if (r >= Mild) return "You respect the player's worth.";
            if (r <= -Strong) return "You hold the player in open contempt.";
            if (r <= -Mild) return "You think little of the player.";
            return null;
        }

        /// <summary>
        ///   How much you fear the player, or null when unafraid. Fear is the player's proof that their power
        ///   bites, so it is voiced as VISIBLE CONDUCT — and it GRADUATES: the more afraid you are, the more
        ///   you give way. A flicker defers on small things; true dread makes you grant what you would never
        ///   otherwise concede. Never a private feeling the NPC can hide behind a proud face.
        /// </summary>
        public static string FearPhrase(int f)
        {
            if (f >= 80)
                return "You are gripped by fear of the player, and your composure frays — you grant what you would " +
                       "otherwise refuse, agree rather than risk their wrath, and offer no defiance worth the name; " +
                       "you want only to get through this without provoking them.";
            if (f >= 60)
                return "You are openly afraid of the player, and it shows — you weigh your words, yield ground, and " +
                       "concede points you would normally hold rather than risk their wrath; you back down where you " +
                       "would otherwise stand firm, and any defiance you muster is brittle and anxious.";
            if (f >= 30)
                return "You are wary and intimidated by the player — a guardedness runs through your manner; you tread " +
                       "carefully, blunt your challenges, and think twice before crossing them.";
            if (f >= 10)
                return "You feel a flicker of unease around the player — a faint caution, quick to defer on small things, " +
                       "that you would never admit aloud.";
            return null;
        }

        /// <summary>
        ///   The posture block for the prompt — one line per axis that is not neutral, in a stable
        ///   order (trust, respect, fear). Null when the NPC is wholly neutral on these axes, so the
        ///   block is skipped entirely. Believable combinations compose naturally from the per-axis lines.
        ///
        ///   Warmth (Affection) is deliberately NOT restated here (P0.2 of the affect-coherence reform,
        ///   04/07/2026): Affection is a live mirror of <c>NpcProfile.ReputationWithPlayer</c>
        ///   (<see cref="NpcStance" />'s own doc, and <c>StanceService.ToStance</c>), and the prompt
        ///   already states that exact number in words once, in the CURRENT STANCE line
        ///   (<c>PromptBuilder.AppendCurrentStance</c> / <c>DescribePersonalRegard</c>). Voicing it a
        ///   second time here, via <see cref="AffectionPhrase" />, made the same value read twice under
        ///   two different vocabularies. <see cref="AffectionPhrase" /> itself is kept (and still tested)
        ///   as a documented, available phrasing — just no longer wired into this composition.
        /// </summary>
        public static string Summarize(NpcStance stance)
        {
            if (stance == null) return null;

            var lines = new List<string>();
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
