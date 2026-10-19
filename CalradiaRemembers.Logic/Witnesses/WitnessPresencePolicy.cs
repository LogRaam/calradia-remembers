// Code written by Gabriel Mailhot, 23/08/2026.
// Pure thinning of an already-gathered list of AMBIENT hero witnesses, per the player's WitnessDensity choice
// (player complaint: conversations are always crowded). Each eligible witness is kept with probability p(level),
// then the survivors are truncated to cap(level), in original order. Applied ON TOP of whatever cap the resolver
// already enforced while gathering (WitnessResolver.MaxLordWitnesses): Bustling's p=1.0/cap=int.MaxValue is an
// exact passthrough, so it reproduces today's list unchanged, which matters because the mod is LIVE and Bustling
// ships as the default. The rng is INJECTED (never MBRandom directly) so the mod's own crowd stays unpredictable
// in play while remaining fully deterministic here; the engine caller backs it with MBRandom.RandomFloat.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Witnesses
{
    /// <summary>Thins a gathered list of ambient hero witnesses according to the player's chosen WitnessDensity.</summary>
    public static class WitnessPresencePolicy
    {
        /// <summary>The per-witness keep chance for Sparse: rooms thin out fast, private moments become common.</summary>
        public const double SparseProbability = 0.25;

        /// <summary>The most ambient witnesses Sparse ever leaves standing.</summary>
        public const int SparseCap = 1;

        /// <summary>The per-witness keep chance for Normal: a genuine middle ground, not a near-empty room.</summary>
        public const double NormalProbability = 0.5;

        /// <summary>The most ambient witnesses Normal ever leaves standing.</summary>
        public const int NormalCap = 2;

        /// <summary>
        ///   Bustling's per-witness keep chance: 1.0, so no witness is ever rolled away. Together with
        ///   <see cref="BustlingCap" /> this makes Bustling an EXACT passthrough, pinning today's live behaviour.
        /// </summary>
        public const double BustlingProbability = 1.0;

        /// <summary>Bustling's cap: unbounded, so the existing resolver cap is the only limit left standing.</summary>
        public const int BustlingCap = int.MaxValue;

        /// <summary>The chance, per eligible witness, that <see cref="Filter{T}" /> keeps them at this density.</summary>
        public static double ProbabilityFor(WitnessDensity density)
        {
            switch (density)
            {
                case WitnessDensity.Sparse: return SparseProbability;
                case WitnessDensity.Normal: return NormalProbability;
                default:                    return BustlingProbability; // Bustling
            }
        }

        /// <summary>The most witnesses <see cref="Filter{T}" /> ever returns at this density (before any outer cap).</summary>
        public static int CapFor(WitnessDensity density)
        {
            switch (density)
            {
                case WitnessDensity.Sparse: return SparseCap;
                case WitnessDensity.Normal: return NormalCap;
                default:                    return BustlingCap; // Bustling
            }
        }

        /// <summary>
        ///   Returns the subset of <paramref name="eligible" /> to keep, in their original order. Each entry
        ///   survives the roll when <c>rng() &lt; ProbabilityFor(density)</c> (so an rng returning exactly the
        ///   probability, or above, drops that entry), then the survivors are truncated to
        ///   <see cref="CapFor" />. Null or empty input yields an empty list; a null <paramref name="rng" /> is
        ///   treated as "always roll false" (nothing survives) rather than throwing, since a caller that forgot
        ///   to wire the engine's random source should get a starkly empty room, not a crash mid-conversation.
        /// </summary>
        public static List<T> Filter<T>(IReadOnlyList<T> eligible, WitnessDensity density, Func<double> rng)
        {
            var kept = new List<T>();
            if (eligible == null || eligible.Count == 0) return kept;

            double p = ProbabilityFor(density);
            int cap = CapFor(density);

            foreach (T item in eligible)
            {
                if (kept.Count >= cap) break;

                double roll = rng?.Invoke() ?? 1.0; // no rng wired -> treat as "always fails the roll"
                if (roll < p) kept.Add(item);
            }

            return kept;
        }
    }
}
