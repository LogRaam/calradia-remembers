// Code written by Gabriel Mailhot, 26/06/2026.
// Bastards (design layer) — how GRAVE a discovered bastard is depends on the culture's view of infidelity.
// A strictly-monogamous culture (high exclusivity outrage) treats the proof of a bastard as deep dishonour;
// a more permissive one shrugs. Reuses the jealousy system's per-culture `JealousyOutrage`. Pure so the scale
// — and its tuning — stays protected by tests.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Scales the cost of a discovered bastard scandal (the standing hit and how far it spreads) by the
    ///   culture's outrage at infidelity — the same <see cref="JealousyOutrage" /> the jealousy system reads.
    /// </summary>
    public static class BastardScandalPolicy
    {
        /// <summary>High outrage — a deep dishonour. Percent of the base cost. Tuning.</summary>
        public const int GravePercent = 150;

        /// <summary>Medium outrage — the ordinary cost. Tuning.</summary>
        public const int NormalPercent = 100;

        /// <summary>Low outrage — the culture mostly shrugs. Tuning.</summary>
        public const int MildPercent = 50;

        /// <summary>The scandal's cost as a percent of its base, by the culture's outrage at infidelity.</summary>
        public static int SeverityPercent(JealousyOutrage outrage)
            => outrage switch {
                JealousyOutrage.High => GravePercent,
                JealousyOutrage.Medium => NormalPercent,
                _ => MildPercent
            };

        /// <summary>
        ///   Scales a base scandal cost by the culture's outrage. A positive base never scales to nothing
        ///   (a scandal always costs at least a little, even in a permissive culture).
        /// </summary>
        public static int Scale(int baseValue, JealousyOutrage outrage)
        {
            int scaled = baseValue * SeverityPercent(outrage) / 100;

            return baseValue > 0
                ? Math.Max(1, scaled)
                : scaled;
        }
    }
}
