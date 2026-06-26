// Code written by Gabriel Mailhot, 25/06/2026.
// Bastards (design layer) — a hidden birth's risk of coming to light. Decided 2026-06-25: the more people
// who know the secret, the likelier it leaks (mother + affair witnesses + the grown bastard). Pure so the
// curve — and its tuning — stays protected by tests.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How likely a concealed bastard is discovered in a given period, as a function of how many people
    ///   know. Each knower may independently let it slip, so risk rises with their number — 1 - (1-p)^n —
    ///   capped so even a crowd never makes exposure a certainty in a single period. Per-knower discretion
    ///   (a loyal confidant vs a loose-tongued rival) is a later refinement; this foundation is count-only.
    /// </summary>
    public static class BastardDiscoveryPolicy
    {
        /// <summary>Base chance (percent) that a single knower lets the secret slip in one period. Tuning.</summary>
        public const int PerKnowerLeakPercent = 5;

        /// <summary>Ceiling so even many knowers never make exposure certain in one period. Tuning.</summary>
        public const int MaxDiscoveryPercent = 60;

        /// <summary>
        ///   The chance (percent, 0..<see cref="MaxDiscoveryPercent" />) the secret is discovered this period
        ///   given <paramref name="knowerCount" /> people know. No knowers can never leak it.
        /// </summary>
        public static int DiscoveryChancePercent(int knowerCount)
        {
            if (knowerCount <= 0) return 0;

            double staysHidden = Math.Pow(1.0 - PerKnowerLeakPercent / 100.0, knowerCount);
            int chance = (int) Math.Round((1.0 - staysHidden) * 100.0);

            return Math.Min(chance, MaxDiscoveryPercent);
        }
    }
}
