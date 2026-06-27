// Code written by Gabriel Mailhot, 27/06/2026.
// Extension Surface, Volet B — the validation rules at the mission-registration door. A third-party mod
// registers a MISSION for the player into CR's verified-quest system; before it becomes a real quest, its
// scalars and text are normalised: title/description bounded (the save-blob guard), reward gold/relation
// clamped (CR pays only within its own caps — the external mod cannot promise a fortune), and the optional
// deadline normalised. Pure so the contract — and its tuning — stays protected by tests; the giver-hero
// resolution, dedup, journal mirror and reward grant are engine-side in the Missions facade.

namespace CalradiaRemembers.Logic
{
    /// <summary>The normalisation rules every externally-registered player mission passes through.</summary>
    public static class ExternalMissionRules
    {
        /// <summary>The longest a mission title may be (the giver's one-line ask). Tuning.</summary>
        public const int MaxTitleChars = 80;

        /// <summary>The longest a description may be — the save-blob guard against an oversized injected line. Tuning.</summary>
        public const int MaxDescriptionChars = 300;

        /// <summary>The most gold CR will ever pay out for one mission, whatever the mod asks for. Tuning.</summary>
        public const int MaxRewardGold = 100000;

        /// <summary>
        ///   The most personal regard a mission may grant — matched to CR's native quest-reward cap, which is
        ///   fixed at issue and bypasses the relation gate because the deed is verified. Tuning.
        /// </summary>
        public const int MaxRewardRelation = 10;

        /// <summary>The shortest a deadline may be once one is set (a same-day deadline is unfair). Tuning.</summary>
        public const int MinDeadlineDays = 1;

        /// <summary>The longest a deadline may run — a year of Calradian days. Tuning.</summary>
        public const int MaxDeadlineDays = 365;

        /// <summary>Clamps a promised gold reward into the payable range (never negative, never a fortune).</summary>
        public static int ClampRewardGold(int gold)
            => gold < 0
                ? 0
                : gold > MaxRewardGold
                    ? MaxRewardGold
                    : gold;

        /// <summary>Clamps a promised relation reward into CR's gated quest-reward range.</summary>
        public static int ClampRewardRelation(int relation)
            => relation < 0
                ? 0
                : relation > MaxRewardRelation
                    ? MaxRewardRelation
                    : relation;

        /// <summary>
        ///   Trims and bounds a title to its blob-safe length. Null or whitespace returns null, which the
        ///   caller treats as an invalid (rejected) registration — a mission with no title says nothing.
        /// </summary>
        public static string BoundTitle(string title) => Bound(title, MaxTitleChars);

        /// <summary>
        ///   Trims and bounds a description to the blob-safe length. Null or whitespace returns null; the
        ///   caller may then fall back to the title, but a mission with neither is rejected.
        /// </summary>
        public static string BoundDescription(string description) => Bound(description, MaxDescriptionChars);

        /// <summary>
        ///   Normalises an optional deadline expressed in days-from-now. Null or a non-positive value means
        ///   "no deadline"; a positive value is clamped into the fair range. The caller adds it to the issue day.
        /// </summary>
        public static int? NormalizeDeadlineDays(int? days)
        {
            if (!days.HasValue || days.Value <= 0) return null;

            return days.Value < MinDeadlineDays
                ? MinDeadlineDays
                : days.Value > MaxDeadlineDays
                    ? MaxDeadlineDays
                    : days.Value;
        }

        /// <summary>
        ///   True when the registration carries the identity a mission needs: a stable id (for dedup and the
        ///   later completion signal) and a source-mod tag (to attribute and, if it misbehaves, mute it).
        /// </summary>
        public static bool HasRequiredIdentity(string id, string sourceMod)
            => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(sourceMod);

        private static string Bound(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string trimmed = text.Trim();

            return trimmed.Length <= max
                ? trimmed
                : trimmed.Substring(0, max);
        }
    }
}
