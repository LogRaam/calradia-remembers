// Code written by Gabriel Mailhot, 10/07/2026.
// A faithful, content companion who has served the player through war may, once the comrade bond runs deep enough,
// ask for a private audience to confess genuine affection and become the player's SECRET LOVER (kept discreet to
// avoid jealousy). This is the pure decision only: whether a companion could ever confide, and whether the weekly
// roll fires this week. Spawning the audience, the popup, and any refusal cooldown bookkeeping are engine glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for a companion's secret affection confession (eligibility plus the weekly roll).</summary>
    public static class CompanionSecretAffectionPolicy
    {
        /// <summary>Minimum regard (a deep comrade bond) before a companion would ever confide this.</summary>
        public const int MinRegard = 50;

        /// <summary>Cap on the weekly chance a fully-bonded companion finally confides.</summary>
        public const int MaxWeeklyChancePercent = 40;

        /// <summary>After the player turns the audience popup away, do not ask again for this many days.</summary>
        public const int RefusalCooldownDays = 60;

        /// <summary>
        ///   Whether a companion could EVER confide (all must hold): they are a companion, orientation-compatible
        ///   with the player, adult content is enabled, they are content (not unhappy), they are not already
        ///   attached to the player (spouse/consort/secret-lover/committed), and the bond is deep enough.
        /// </summary>
        public static bool IsEligible(bool isCompanion, bool orientationCompatible, bool adultEnabled,
                                      bool isContent, bool alreadyAttached, int regard)
            => isCompanion && orientationCompatible && adultEnabled && isContent && !alreadyAttached
               && regard >= MinRegard;

        /// <summary>
        ///   Given eligibility, the weekly chance (percent) this companion finally confides, scaling with how far
        ///   the bond runs beyond MinRegard: clamp((regard - MinRegard) * 2 + 5, 5, MaxWeeklyChancePercent).
        /// </summary>
        public static int WeeklyChancePercent(int regard)
        {
            int raw = (regard - MinRegard) * 2 + 5;
            if (raw < 5) return 5;
            if (raw > MaxWeeklyChancePercent) return MaxWeeklyChancePercent;
            return raw;
        }

        /// <summary>True when a 0..99 roll lands within this week's chance.</summary>
        public static bool ShouldConfideThisWeek(int regard, int roll)
            => roll >= 0 && roll < WeeklyChancePercent(regard);
    }
}
