// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar, pure foundation: formation guardrails (anti-noise), decay (slower than a grudge
// fades — a kindness LINGERS), the per-NPC surfacing cap, and the cross-interaction payoff: a live grudge
// TARNISHES a standing appreciation (ROADMAP ~line 310). Every constant here is a tuning knob; changing
// one is a one-line, test-covered decision. Trait direction is the MIRROR of GrudgePolicy: a grudge's
// Mercy/Calculating axis is flipped here onto Generosity/Mercy vs Calculating (a warm heart feels a
// kindness readily and keeps it long; a cold ledger-keeper barely notices one and lets it go quickly).

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>
    ///   Every fact <see cref="AppreciationPolicy.ShouldForm" /> needs about a candidate appreciation,
    ///   before it becomes a persisted <see cref="Appreciation" />.
    /// </summary>
    public sealed class AppreciationCandidateFacts
    {
        /// <summary>Which system is proposing this appreciation.</summary>
        public AppreciationSource Source { get; set; }

        /// <summary>The proposed intensity, 0..100.</summary>
        public int Magnitude { get; set; }

        /// <summary>Anti-noise guardrail: true only when the kindness is clearly attributable to the target. An unattributable event never forms an appreciation, however large.</summary>
        public bool IsAttributable { get; set; }

        /// <summary>The holder's trait levels.</summary>
        public TraitFacts Traits { get; set; }
    }

    /// <summary>Pure rules governing whether an appreciation forms, how it decays, which ones surface, and how a live grudge tarnishes it.</summary>
    public static class AppreciationPolicy
    {
        // ── Formation (anti-noise guardrail) ────────────────────────────────

        /// <summary>Tuning: the base magnitude floor below which a kindness is too minor to register at all. Deliberately the same numeric value as GrudgePolicy.MagnitudeFloor, for comparability across the two mirrored systems.</summary>
        public const int MagnitudeFloor = 15;

        /// <summary>Tuning: each positive Generosity level LOWERS the formation floor (a warm heart is moved by a smaller kindness). The mirror of GrudgePolicy.MercyFormationFloorPerLevel, which RAISES the grudge floor.</summary>
        public const int GenerosityFormationFloorReliefPerLevel = 5;

        /// <summary>Tuning: each positive Mercy level also lowers the formation floor, on top of Generosity.</summary>
        public const int MercyFormationFloorReliefPerLevel = 5;

        /// <summary>Tuning: each positive Calculating level RAISES the formation floor (a cold ledger-keeper is less moved by a mere kindness). The mirror of GrudgePolicy.CalculatingFormationFloorPerLevel, which LOWERS the grudge floor.</summary>
        public const int CalculatingFormationFloorPerLevel = 5;

        /// <summary>
        ///   True when a candidate clears every anti-noise guardrail: attributable, and its magnitude
        ///   meets the trait-modulated floor. High Generosity/Mercy lowers the floor (feels kindnesses
        ///   readily); high Calculating raises it (barely moved by one).
        /// </summary>
        public static bool ShouldForm(AppreciationCandidateFacts facts)
        {
            if (facts == null || !facts.IsAttributable) return false;

            int floor = FormationFloor(facts.Traits);

            return facts.Magnitude >= floor;
        }

        private static int FormationFloor(TraitFacts traits)
        {
            if (traits == null) return MagnitudeFloor;

            int floor = MagnitudeFloor
                - traits.Generosity * GenerosityFormationFloorReliefPerLevel
                - traits.Mercy * MercyFormationFloorReliefPerLevel
                + traits.Calculating * CalculatingFormationFloorPerLevel;

            return floor < 0 ? 0 : floor;
        }

        // ── Decay (a kindness LINGERS) ───────────────────────────────────────

        /// <summary>
        ///   Tuning: magnitude points softened per in-game day at baseline. Deliberately HALF of
        ///   GrudgePolicy.BaseDecayPerDay (0.4): a kindness takes about twice as long to fade to nothing
        ///   as a grievance of the same starting magnitude takes to soften away, per the ROADMAP's "forms,
        ///   reinforces regard, and fades slowly" framing. Not a direct reference to GrudgePolicy (the two
        ///   namespaces stay decoupled); AppreciationPolicyTests locks the 1:2 ratio against the live
        ///   GrudgePolicy constant so a future change to either side is caught immediately.
        /// </summary>
        public const double BaseDecayPerDay = 0.2;

        /// <summary>Tuning: decay slowed per day, per positive Generosity level (a warm heart keeps a kindness longer).</summary>
        public const double GenerosityDecaySlowdownPerLevel = 0.05;

        /// <summary>Tuning: decay slowed per day, per positive Mercy level, on top of Generosity.</summary>
        public const double MercyDecaySlowdownPerLevel = 0.05;

        /// <summary>Tuning: the decay rate never drops below this floor — even the warmest holder's memory of a kindness still drifts eventually. No never-decay case exists here (unlike a grudge vendetta): an appreciation, by design, always eventually fades.</summary>
        public const double MinDecayPerDay = 0.05;

        /// <summary>
        ///   The appreciation's live intensity today: a live, idempotent read from
        ///   <see cref="Appreciation.EventDay" />, decaying linearly, slower with higher Generosity/Mercy
        ///   (down to <see cref="MinDecayPerDay" />). The stored <see cref="Appreciation.Magnitude" /> is
        ///   never mutated.
        /// </summary>
        public static int EffectiveMagnitude(Appreciation appreciation, int today, TraitFacts traits)
        {
            if (appreciation == null) return 0;

            int daysElapsed = today - appreciation.EventDay;
            if (daysElapsed <= 0) return Clamp(appreciation.Magnitude, 0, 100);

            double generosity = traits?.Generosity ?? 0;
            double mercy = traits?.Mercy ?? 0;

            double decayPerDay = BaseDecayPerDay
                - PositivePart(generosity) * GenerosityDecaySlowdownPerLevel
                - PositivePart(mercy) * MercyDecaySlowdownPerLevel;

            if (decayPerDay < MinDecayPerDay) decayPerDay = MinDecayPerDay;

            double effective = appreciation.Magnitude - decayPerDay * daysElapsed;

            return effective <= 0 ? 0 : (int) Math.Floor(effective);
        }

        // ── Per-NPC cap (anti-noise) ─────────────────────────────────────────

        /// <summary>Tuning: the maximum number of a single holder's live appreciations that ever surface at once.</summary>
        public const int PerNpcCap = 3;

        /// <summary>
        ///   The dominant appreciations among one holder's felt kindnesses: fully-faded entries (effective
        ///   magnitude zero) never surface; the rest are ranked by live effective magnitude and truncated
        ///   to <see cref="PerNpcCap" />, so the system never reads as a bookkeeper's ledger of favours.
        /// </summary>
        public static IEnumerable<Appreciation> SelectDominant(IEnumerable<Appreciation> appreciations, int today, TraitFacts traits)
        {
            if (appreciations == null) return Enumerable.Empty<Appreciation>();

            return appreciations
                .Where(a => a != null && EffectiveMagnitude(a, today, traits) > 0)
                .OrderByDescending(a => EffectiveMagnitude(a, today, traits))
                .Take(PerNpcCap)
                .ToList();
        }

        /// <summary>
        ///   The per-holder admission rule a live planting call (<c>AppreciationBehavior.TryPlant</c>)
        ///   enforces BEFORE forming a new appreciation — the mirror of
        ///   <c>GrudgePolicy.TryAdmit</c>. Below <see cref="PerNpcCap" /> live appreciations, the newcomer
        ///   is always admitted (<paramref name="toEvict" /> null). At the cap, a full house only yields to
        ///   a STRICTLY stronger newcomer: the single weakest live appreciation (by today's effective
        ///   magnitude) is named via <paramref name="toEvict" /> for the caller to remove; a newcomer no
        ///   stronger than the weakest is refused outright (it would just add noise, the exact anti-noise
        ///   guardrail the per-NPC cap exists for).
        /// </summary>
        public static bool TryAdmit(IReadOnlyList<Appreciation> liveAppreciations, int newcomerMagnitude, int today, TraitFacts traits, out Appreciation toEvict)
        {
            toEvict = null;

            if (liveAppreciations == null) return true;

            List<Appreciation> live = liveAppreciations.Where(a => a != null && EffectiveMagnitude(a, today, traits) > 0).ToList();

            if (live.Count < PerNpcCap) return true;

            Appreciation weakest = live.OrderBy(a => EffectiveMagnitude(a, today, traits)).First();
            int weakestMagnitude = EffectiveMagnitude(weakest, today, traits);

            if (newcomerMagnitude <= weakestMagnitude) return false;

            toEvict = weakest;

            return true;
        }

        // ── The cross-interaction payoff: a live grudge TARNISHES standing warmth ──

        /// <summary>Tuning: how much of the holder's live grudge magnitude bites into a standing appreciation's warmth. 1.0 = a grudge as strong as the appreciation roughly cancels it outright; a softer ratio would let warmth partially survive even a matching grudge.</summary>
        public const double TarnishRatio = 1.0;

        /// <summary>
        ///   A fresh, high-magnitude grudge SOURS a standing appreciation: the felt kindness is reduced in
        ///   proportion to how much the holder currently holds against the target, floored at zero (a
        ///   grudge never makes an appreciation negative — it only tarnishes warmth already there, it
        ///   cannot invent resentment out of a kindness). No grudge (<paramref name="activeGrudgeMagnitude" />
        ///   at or below zero) leaves the appreciation untouched. Kept PURE: takes the two live effective
        ///   magnitudes as plain ints, so the engine is the one that reads the holder's dominant grudge and
        ///   passes the number in — this method never touches a Grudge type, keeping Appreciations
        ///   decoupled from Grudges.
        /// </summary>
        public static int TarnishedMagnitude(int effectiveAppreciationMagnitude, int activeGrudgeMagnitude)
        {
            if (effectiveAppreciationMagnitude <= 0) return 0;
            if (activeGrudgeMagnitude <= 0) return Clamp(effectiveAppreciationMagnitude, 0, 100);

            int reduction = (int) Math.Round(activeGrudgeMagnitude * TarnishRatio);
            int result = effectiveAppreciationMagnitude - reduction;

            return result < 0 ? 0 : result;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static double PositivePart(double value) => value > 0 ? value : 0;

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;

            return value > max ? max : value;
        }
    }
}
