// Code written by Gabriel Mailhot, 23/06/2026.
// Companion happiness & retention (ROADMAP) — the pure model, built first. A companion's HAPPINESS is their
// satisfaction in your service (distinct from stance, which is their regard for YOU). Deterministic drivers
// nudge a 0..100 score; it drifts gently back toward a baseline so neither a single slight nor a single
// kindness is permanent; bands read content → restless → disgruntled, and below a floor they mean to leave.
// The engine maps game events (errands, pay, idleness, being kept from a loved one, treatment) to these
// drivers and persists the score; this stays pure and unit-tested.

namespace CalradiaRemembers.Logic
{
    /// <summary>How a companion feels about being in your service — the bands the score falls into.</summary>
    public enum HappinessBand
    {
        /// <summary>Soured on your service — speaks of leaving, and below the floor will act on it.</summary>
        Disgruntled,

        /// <summary>Patience thinning; not yet a grievance, but not content.</summary>
        Restless,

        /// <summary>At ease in your service.</summary>
        Content,

        /// <summary>Devoted — they would follow you anywhere.</summary>
        Devoted
    }

    /// <summary>A reason a companion's happiness moves. The engine fires these; the magnitudes live here.</summary>
    public enum HappinessDriver
    {
        SuitedErrand,      // sent on an errand that fits them (a rogue to steal, a scout for news)
        UnsuitedErrand,    // employed, but not to their taste
        ErrandSucceeded,
        ErrandFailed,
        Captured,          // taken prisoner on your business
        Freed,             // won back to your side
        Paid,              // given coin / a share of loot
        Idle,              // left kicking their heels (applied on an idle tick)
        KeptFromLovedOne,  // a spouse / lover is not with them (applied on a tick)
        WellTreated,       // a gift, a kept promise, a kindness
        Mistreated         // a slight, a broken word
    }

    /// <summary>
    ///   Pure happiness arithmetic: clamp, banding, the "wants to leave" floor, the per-driver magnitudes,
    ///   and a gentle drift back to baseline. All magnitudes/thresholds are tunable constants Gabriel
    ///   adjusts by in-game feel.
    /// </summary>
    public static class CompanionHappiness
    {
        public const int Min     = 0;
        public const int Max     = 100;
        public const int Default = 60;   // where a freshly-recruited companion starts and drifts back toward

        // Band thresholds (a score AT the threshold reads as that band).
        private const int DevotedAt  = 80;
        private const int ContentAt  = 50;
        private const int RestlessAt = 25;

        /// <summary>At or below this, the companion means to leave your service.</summary>
        public const int LeaveThreshold = 15;

        public static int Clamp(int score)
        {
            if (score < Min) return Min;
            if (score > Max) return Max;
            return score;
        }

        /// <summary>Moves a score by a raw delta, clamped to [Min, Max].</summary>
        public static int Apply(int score, int delta) => Clamp(score + delta);

        /// <summary>Applies a named driver's magnitude to the score, clamped.</summary>
        public static int ApplyDriver(int score, HappinessDriver driver) => Apply(score, DeltaFor(driver));

        /// <summary>
        ///   Eases the score toward <see cref="Default"/> by at most <paramref name="step"/> (recovery /
        ///   relaxation over time), without overshooting. A no-op once already at baseline.
        /// </summary>
        public static int DriftToBaseline(int score, int step)
        {
            if (step <= 0 || score == Default) return Clamp(score);
            if (score > Default) return Clamp(score - System.Math.Min(step, score - Default));
            return Clamp(score + System.Math.Min(step, Default - score));
        }

        public static HappinessBand Band(int score)
        {
            if (score >= DevotedAt) return HappinessBand.Devoted;
            if (score >= ContentAt) return HappinessBand.Content;
            if (score >= RestlessAt) return HappinessBand.Restless;
            return HappinessBand.Disgruntled;
        }

        /// <summary>True when the companion is unhappy enough to move toward leaving your service.</summary>
        public static bool WantsToLeave(int score) => score <= LeaveThreshold;

        /// <summary>A short natural-language reading of the band, for the encyclopedia line and prompts.</summary>
        public static string Describe(int score)
        {
            switch (Band(score))
            {
                case HappinessBand.Devoted:     return "devoted to your service";
                case HappinessBand.Content:     return "content in your service";
                case HappinessBand.Restless:    return "restless, their patience thinning";
                default:                        return WantsToLeave(score)
                                                    ? "bitterly unhappy — they mean to leave you"
                                                    : "disgruntled, and speaking of leaving";
            }
        }

        /// <summary>The magnitude each driver moves happiness by (positive = happier). Tunable.</summary>
        public static int DeltaFor(HappinessDriver driver)
        {
            switch (driver)
            {
                case HappinessDriver.SuitedErrand:     return  6;
                case HappinessDriver.UnsuitedErrand:   return  1;  // still employed — mildly welcome
                case HappinessDriver.ErrandSucceeded:  return  5;
                case HappinessDriver.ErrandFailed:     return -4;
                case HappinessDriver.Captured:         return -12;
                case HappinessDriver.Freed:            return  3;
                case HappinessDriver.Paid:             return  5;
                case HappinessDriver.Idle:             return -3;
                case HappinessDriver.KeptFromLovedOne: return -3;
                case HappinessDriver.WellTreated:      return  4;
                case HappinessDriver.Mistreated:       return -8;
                default:                               return  0;
            }
        }
    }
}
