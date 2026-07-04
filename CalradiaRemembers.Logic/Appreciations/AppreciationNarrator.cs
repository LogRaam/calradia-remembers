// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar, pure foundation: qualitative, numberless chronicler prose, the positive mirror of
// GrudgeNarrator. The encyclopedia and the prompt both speak in words, never digits. Unlike a grudge,
// there is no concealment register here: an unvoiced appreciation is not "hidden" out of coldness, it is
// simply a quiet warmth the NPC has not put into words yet.

namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>Pure, numberless text for an appreciation: the warmth band word and the one-sentence prompt line.</summary>
    public static class AppreciationNarrator
    {
        // ── Warmth bands ─────────────────────────────────────────────────────

        /// <summary>Tuning: effective magnitude at/below this band reads as barely worth mentioning.</summary>
        public const int FleetingCeiling = 10;

        /// <summary>Tuning: effective magnitude at/below this band reads as a small kindness remembered.</summary>
        public const int SmallKindnessCeiling = 35;

        /// <summary>Tuning: effective magnitude at/below this band reads as a genuine debt of gratitude.</summary>
        public const int GenuineDebtCeiling = 70;

        /// <summary>
        ///   The qualitative word for an effective magnitude, four bands from "barely worth mentioning" to
        ///   "a deep and lasting gratitude" — never a number, the same discipline as GrudgeNarrator.RigidityWord.
        /// </summary>
        public static string WarmthWord(int effectiveMagnitude)
        {
            if (effectiveMagnitude <= FleetingCeiling) return "a fleeting warmth";
            if (effectiveMagnitude <= SmallKindnessCeiling) return "a small kindness remembered";
            if (effectiveMagnitude <= GenuineDebtCeiling) return "a genuine debt of gratitude";

            return "a deep and lasting gratitude";
        }

        // ── Prompt line ──────────────────────────────────────────────────────

        /// <summary>Fallback cause text when the caller has not (yet) supplied one.</summary>
        private const string FallbackCause = "a kindness you can no longer quite place";

        /// <summary>
        ///   One sentence for the NPC prompt, colouring the exchange warmly, separate from raw regard. An
        ///   appreciation fully faded (<paramref name="effectiveMagnitude" /> at or below zero) yields no
        ///   line at all. Otherwise mirrors the knownness flag: a KNOWN appreciation may be named openly by
        ///   the NPC; an unvoiced one still colours their manner quietly, without ever being named.
        /// </summary>
        public static string PromptLine(Appreciation appreciation, bool knownToPlayer, int effectiveMagnitude)
        {
            if (appreciation == null || effectiveMagnitude <= 0) return null;

            string word = WarmthWord(effectiveMagnitude);
            string cause = string.IsNullOrWhiteSpace(appreciation.CauseSummary) ? FallbackCause : appreciation.CauseSummary;

            return knownToPlayer
                ? $"You feel {word} toward them for {cause}, and you may name it openly if the moment calls for it."
                : $"You privately feel {word} toward them for {cause}; you have said nothing of it, but it warms how you treat them all the same.";
        }

        // ── Display line (encyclopedia, third person) ───────────────────────

        /// <summary>Fallback cause text for the display line when the caller has not (yet) supplied one.</summary>
        private const string DisplayFallbackCause = "a kindness from before";

        /// <summary>
        ///   One third-person chronicler sentence for the encyclopedia's "Kindnesses"/"Debts of gratitude"
        ///   section: no numbers, ever. Null when there is nothing worth showing a player, either because
        ///   the appreciation is null or fully faded today (<paramref name="effectiveMagnitude" /> at or
        ///   below zero). Names the target as "you" when <paramref name="targetIsPlayer" />, else by
        ///   <paramref name="targetName" /> (falls back to "them" if that name is missing). Causes are
        ///   stored as full first-person sentences (they double as prompt text), so trailing punctuation is
        ///   trimmed before embedding to avoid a double period, the same fix GrudgeNarrator carries.
        /// </summary>
        public static string DisplayLine(Appreciation appreciation, bool targetIsPlayer, string targetName, int effectiveMagnitude)
        {
            if (appreciation == null || effectiveMagnitude <= 0) return null;

            string word = WarmthWord(effectiveMagnitude);
            string verb = DisplayVerbFor(word);
            string target = targetIsPlayer ? "you" : (string.IsNullOrWhiteSpace(targetName) ? "them" : targetName);
            string cause = string.IsNullOrWhiteSpace(appreciation.CauseSummary)
                ? DisplayFallbackCause
                : appreciation.CauseSummary.Trim().TrimEnd('.', '!', '?');

            return $"{verb} {word} toward {target}, for {cause}.";
        }

        /// <summary>A verb keyed to the warmth band, so the sentence reads naturally at every band.</summary>
        private static string DisplayVerbFor(string warmthWord)
        {
            switch (warmthWord)
            {
                case "a fleeting warmth":               return "Feels";
                case "a small kindness remembered":      return "Carries";
                case "a genuine debt of gratitude":      return "Holds";
                default:                                 return "Cherishes"; // "a deep and lasting gratitude"
            }
        }
    }
}
