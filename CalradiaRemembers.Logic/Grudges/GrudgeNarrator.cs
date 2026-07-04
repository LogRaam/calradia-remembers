// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: qualitative, numberless chronicler prose — the encyclopedia and the
// prompt both speak in words, never digits (ROADMAP: "narrative text, no numbers, the cause + a
// qualitative rigidity word ... the resolution hint only if discovered"). The same knownness flag that
// gates encyclopedia surfacing drives the prompt in mirror: named openly when known, concealed when not.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>Pure, numberless text for a grudge: the rigidity band word and the one-sentence prompt line.</summary>
    public static class GrudgeNarrator
    {
        // ── Rigidity bands ───────────────────────────────────────────────────

        /// <summary>Tuning: rigidity at/below this band reads as barely worth mentioning.</summary>
        public const int PassingCeiling = 15;

        /// <summary>Tuning: rigidity at/below this band still stings, freshly.</summary>
        public const int FreshWoundCeiling = 40;

        /// <summary>Tuning: rigidity at/below this band has settled into a long-carried resentment.</summary>
        public const int OldBitternessCeiling = 65;

        /// <summary>
        ///   The qualitative word for a rigidity value, five bands from "barely worth mentioning" to
        ///   "no path exists" — never a number, per the ROADMAP's encyclopedia surfacing rule.
        /// </summary>
        public static string RigidityWord(int rigidity)
        {
            if (rigidity >= GrudgePolicy.VendettaRigidity) return "unforgivable";
            if (rigidity <= PassingCeiling) return "a passing grievance";
            if (rigidity <= FreshWoundCeiling) return "a fresh wound";
            if (rigidity <= OldBitternessCeiling) return "an old bitterness";

            return "a deep grudge";
        }

        // ── Prompt line ──────────────────────────────────────────────────────

        /// <summary>Fallback cause text when the caller has not (yet) supplied one.</summary>
        private const string FallbackCause = "something unspoken between you";

        /// <summary>
        ///   One sentence for the NPC prompt, colouring the exchange separately from warmth. A grudge
        ///   fully decayed or resolved (<paramref name="effectiveMagnitude" /> at or below zero) yields
        ///   no line at all: it no longer tints anything. Otherwise, mirrors the knownness flag: a KNOWN
        ///   grudge may be named openly; a HIDDEN one instructs concealment, courteous but cold (the
        ///   validated assassin register), never naming the true cause.
        /// </summary>
        public static string PromptLine(Grudge grudge, bool knownToPlayer, int effectiveMagnitude)
        {
            if (grudge == null || effectiveMagnitude <= 0) return null;

            string word = RigidityWord(grudge.Rigidity);
            string cause = string.IsNullOrWhiteSpace(grudge.CauseSummary) ? FallbackCause : grudge.CauseSummary;

            return knownToPlayer
                ? $"You nurse {word} toward them over {cause}, and you may name it openly if the moment calls for it."
                : $"You privately nurse {word} toward them over {cause}, but you conceal it: stay courteous and cold, never name the true cause, and never hint at it either, no allusions, no knowing references, no veiled phrases that point at it; if pressed about your coolness, deflect to some other plausible irritation.";
        }

        // ── Display line (encyclopedia, third person) ───────────────────────

        /// <summary>Fallback cause text for the display line when the caller has not (yet) supplied one.</summary>
        private const string DisplayFallbackCause = "an old matter";

        /// <summary>
        ///   One third-person chronicler sentence for the encyclopedia's "Grievances" section: no numbers,
        ///   ever (the rigidity word carries all the qualitative weight). Null when there is nothing worth
        ///   showing a player, either because the grudge is gone (<paramref name="grudge" /> null,
        ///   <see cref="Grudge.IsResolved" />) or fully decayed today (<paramref name="effectiveMagnitude" />
        ///   at or below zero). Names the target as "you" when <paramref name="targetIsPlayer" />, else by
        ///   <paramref name="targetName" /> (falls back to "them" if that name is missing). Appends a second
        ///   sentence hinting the resolution path from <paramref name="resolutionKind" />, but only once
        ///   <see cref="Grudge.ResolutionHintDiscovered" /> is true; the player who has not yet learned HOW
        ///   to mend it only ever sees the grievance itself.
        /// </summary>
        public static string DisplayLine(Grudge grudge, bool targetIsPlayer, string targetName, int effectiveMagnitude, GrudgeResolutionKind resolutionKind)
        {
            if (grudge == null || grudge.IsResolved || effectiveMagnitude <= 0) return null;

            string word = RigidityWord(grudge.Rigidity);
            string verb = DisplayVerbFor(word);
            string target = targetIsPlayer ? "you" : (string.IsNullOrWhiteSpace(targetName) ? "them" : targetName);
            // Causes are stored as full first-person sentences (they double as prompt text), so they usually
            // end in their own punctuation. Trim it before embedding so the display reads "...over X." not
            // "...over X.." (a double period the player noticed on the encyclopedia page).
            string cause = string.IsNullOrWhiteSpace(grudge.CauseSummary)
                ? DisplayFallbackCause
                : grudge.CauseSummary.Trim().TrimEnd('.', '!', '?');

            string sentence = $"{verb} {word} against {target}, over {cause}.";

            if (!grudge.ResolutionHintDiscovered) return sentence;

            string hint = DisplayHintFor(resolutionKind);

            return string.IsNullOrEmpty(hint) ? sentence : $"{sentence} {hint}";
        }

        /// <summary>A verb keyed to the rigidity band, so the sentence reads naturally at every band.</summary>
        private static string DisplayVerbFor(string rigidityWord)
        {
            switch (rigidityWord)
            {
                case "a passing grievance": return "Bears";
                case "a fresh wound":       return "Nurses";
                case "an old bitterness":   return "Holds";
                case "unforgivable":        return "Harbors";
                default:                    return "Carries"; // "a deep grudge"
            }
        }

        /// <summary>The resolution-hint sentence, once <see cref="Grudge.ResolutionHintDiscovered" /> allows it.</summary>
        private static string DisplayHintFor(GrudgeResolutionKind resolutionKind)
        {
            switch (resolutionKind)
            {
                case GrudgeResolutionKind.Amends:       return "A sincere amend might yet mend it.";
                case GrudgeResolutionKind.Gift:         return "A worthy gift might soften it.";
                case GrudgeResolutionKind.Deed:         return "Only a deed in their favour would answer for it.";
                case GrudgeResolutionKind.Time:         return "Time may be the only balm.";
                case GrudgeResolutionKind.Unforgivable: return "There is no mending this.";
                default:                                return null;
            }
        }
    }
}
