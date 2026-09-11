// Code written by Gabriel Mailhot, 23/06/2026.
// Camp life (ROADMAP) — pure foundation. Companions form opinions about EACH OTHER: trait compatibility
// drives their mutual standing over time, and at the extremes a companion will raise it with the player
// (a grievance or praise) or two well-matched ones may wed. The simulation is mechanical (this + native
// GetRelation); the LLM only narrates at the player's point of contact. Kept pure and unit-tested.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How two companions stand with each other — banded from their accumulated mutual relation.</summary>
    public enum RapportBand
    {
        Feud,
        Tense,
        Neutral,
        Cordial,
        Close
    }

    /// <summary>
    ///   Pure camp-life arithmetic: the trait COMPATIBILITY that nudges two companions toward bonding or
    ///   clashing, the band their accumulated relation falls into, whether the feeling is strong enough to
    ///   raise with the player, and whether two are warm enough to wed. The host reads the heroes' traits /
    ///   marital state and feeds plain numbers; weights/thresholds are tunable constants.
    /// </summary>
    public static class CompanionRapport
    {
        // Per-axis weight of a shared (vs opposed) leaning. Values clash hardest (honor), then temperament
        // (mercy) and method (calculating); shared daring (valor) and largesse (generosity) bond more mildly.
        private const int HonorWeight = 3;
        private const int MercyWeight = 2;
        private const int CalcWeight  = 2;
        private const int ValorWeight = 1;
        private const int GenWeight   = 1;

        /// <summary>
        ///   Trait compatibility between two companions (each trait −2..+2): positive = they bond, negative
        ///   = they grate. Each axis adds its weight when the two share a leaning, subtracts it when opposed,
        ///   and contributes nothing when either is neutral on that axis. Range about −9..+9.
        ///   <para>
        ///     Takes the two heroes' trait sets rather than TEN LOOSE INTS (2026-09-10). This was the longest
        ///     unguarded run left anywhere in Logic: valorA, honorA, mercyA, generosityA, calculatingA, then
        ///     the same five again for B, and nothing but their order told any of them apart. Slip one
        ///     position anywhere in that line and every axis after it is weighed against the wrong trait, at
        ///     the wrong weight, in silence.
        ///   </para>
        ///   <para>
        ///     It invents no type to fix that: <see cref="RomanticTraits" /> already carries exactly these
        ///     five values on exactly this scale, in this same namespace, built from a live Hero by the mod's
        ///     own adapter. Asking for the type that exists is the whole of the change. The two arguments sit
        ///     side by side without a name to separate them because the answer is SYMMETRIC: swapping the
        ///     companions returns the same number, which is what a compatibility is.
        ///   </para>
        /// </summary>
        public static int Compatibility(RomanticTraits a, RomanticTraits b)
        {
            a ??= new RomanticTraits();
            b ??= new RomanticTraits();

            return Axis(a.Honor, b.Honor, HonorWeight)
                 + Axis(a.Mercy, b.Mercy, MercyWeight)
                 + Axis(a.Calculating, b.Calculating, CalcWeight)
                 + Axis(a.Valor, b.Valor, ValorWeight)
                 + Axis(a.Generosity, b.Generosity, GenWeight);
        }

        private static int Axis(int a, int b, int weight)
        {
            if (a == 0 || b == 0) return 0;                       // a neutral leaning neither bonds nor grates
            return Math.Sign(a) == Math.Sign(b) ? weight : -weight;
        }

        /// <summary>The band two companions' accumulated mutual relation falls into.</summary>
        public static RapportBand Band(int relation)
        {
            if (relation >= 30) return RapportBand.Close;
            if (relation >= 10) return RapportBand.Cordial;
            if (relation >= -9) return RapportBand.Neutral;
            if (relation >= -29) return RapportBand.Tense;
            return RapportBand.Feud;
        }

        /// <summary>Mutual relation magnitude at/above which the feeling is worth a companion raising with the player.</summary>
        public const int RaiseThreshold = 25;

        /// <summary>True when two companions feel strongly enough about each other (warm OR bitter) to mention it.</summary>
        public static bool WorthRaising(int relation) => relation >= RaiseThreshold || relation <= -RaiseThreshold;

        /// <summary>Mutual warmth two companions must reach before they would consider marrying.</summary>
        public const int MarriageThreshold = 40;

        /// <summary>
        ///   Whether two companions could wed, from facts the host has already resolved (gender, marital
        ///   state, age) plus their mutual relation. The host enforces clan rules; this is the warmth gate.
        /// </summary>
        public static bool CanMarry(int relation, bool bothUnmarried, bool oppositeGender, bool bothAdult)
            => bothUnmarried && oppositeGender && bothAdult && relation >= MarriageThreshold;
    }
}
