// Code written by Gabriel Mailhot, 25/06/2026, re-keyed 2026-06-26.
// Bastards (design layer) — a hidden birth's risk of coming to light, evaluated each SEASON. Decided
// 2026-06-26 (Gabriel, option A): the only state we keep is which children are the player's bastards and
// their age, so the risk is keyed on AGE (an infant is easy to hide; an older child is seen at court and
// starts to resemble their father) — not a persisted knower-set. Pure so the curve — and its tuning — stays
// protected by tests.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Bastards
{
    /// <summary>
    ///   How likely a concealed bastard is discovered this SEASON, rising with the child's age. The state the
    ///   host keeps is minimal — the bastard's identity (age is read live) — so the curve depends only on age.
    /// </summary>
    public static class BastardDiscoveryPolicy
    {
        /// <summary>Floor chance each season even for an infant — a careless word, a midwife's gossip. Tuning.</summary>
        public const int BaseSeasonalPercent = 3;

        /// <summary>How much each year of the child's age adds to the seasonal discovery chance. Tuning.</summary>
        public const int PerYearPercent = 2;

        /// <summary>Ceiling so discovery is never a certainty in a single season. Tuning.</summary>
        public const int MaxDiscoveryPercent = 40;

        /// <summary>
        ///   The chance (percent, <see cref="BaseSeasonalPercent" />..<see cref="MaxDiscoveryPercent" />) a
        ///   concealed bastard is discovered this season, given the child's age in years. A negative age is
        ///   treated as zero (the floor).
        /// </summary>
        public static int DiscoveryChancePercent(int ageYears)
        {
            int age = ageYears < 0 ? 0 : ageYears;

            return Math.Min(MaxDiscoveryPercent, BaseSeasonalPercent + age * PerYearPercent);
        }
    }
}
