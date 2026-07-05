// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar (ratified 2026-07-05): the MAP-PRESENCE mechanic resolves the persistence-vs-pursuit
// tension by giving the nemesis NO standing party at all — he only materializes when the player engages a
// qualifying (matching band culture + capture region) vanilla bandit party, via a random reveal roll. This
// is the pure pity-timer pacing for that roll: a base chance that rises with every qualifying encounter
// that DIDN'T reveal him, reaching a certainty by a fixed count so the hunt is emergent but never endless.
// The roll itself is supplied by the caller (IRandomSource et al. live in the engine layer) so this stays
// deterministic and pure.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure pacing policy for the nemesis reveal roll. Consulted whenever the player engages a bandit
    ///   party of a nemesis's band culture in his anchored region: rising chance, guaranteed after a fixed
    ///   number of qualifying misses, so "intercept several bands without finding him, but eventually you
    ///   run into him" holds true by construction.
    /// </summary>
    public static class NemesisRevealPolicy
    {
        /// <summary>TUNE: the reveal chance at the very first qualifying encounter (no pity-timer credit yet).</summary>
        public const double BaseRevealChance = 0.15;

        /// <summary>TUNE: qualifying encounters (since the last reveal) after which the reveal is guaranteed (chance == 1.0).</summary>
        public const int GuaranteeAfterEncounters = 6;

        /// <summary>
        ///   The chance, in [0,1], that THIS qualifying encounter reveals the nemesis as the party's
        ///   leader. Rises linearly from <see cref="BaseRevealChance"/> at 0 to a certainty at
        ///   <see cref="GuaranteeAfterEncounters"/> qualifying misses, and stays a certainty beyond that.
        ///   A negative <paramref name="encountersSinceReveal"/> (should never happen) is clamped to 0.
        /// </summary>
        public static double RevealChance(int encountersSinceReveal)
        {
            int clamped = Math.Max(0, encountersSinceReveal);
            if (clamped >= GuaranteeAfterEncounters) return 1.0;

            double progress = clamped / (double)GuaranteeAfterEncounters;
            return BaseRevealChance + (1.0 - BaseRevealChance) * progress;
        }

        /// <summary>
        ///   True when this qualifying encounter reveals the nemesis. The caller supplies the random
        ///   <paramref name="roll"/> (expected uniform in [0,1)) so this policy stays pure and testable;
        ///   a roll strictly below the current <see cref="RevealChance"/> reveals him.
        /// </summary>
        public static bool ShouldReveal(int encountersSinceReveal, double roll)
            => roll < RevealChance(encountersSinceReveal);
    }
}
