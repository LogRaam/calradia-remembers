// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: the missing counterpart to GrudgePolicy's formation/rigidity/decay
// rules. GrudgePolicy.ResolutionKindFor already decides WHAT a grudge wants (Amends/Gift/Deed/Time/
// Unforgivable); this file decides what happens the moment the player actually OFFERS a gesture. Pure:
// facts in, a verdict + a magnitude-reduction amount out. The engine (a later, flagged increment) is the
// only place that detects a real gift/deed/amends and calls GrudgePolicy.Resolve or subtracts the
// reduction from Grudge.Magnitude.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   Everything <see cref="GrudgeResolutionPolicy.Evaluate" /> needs about one offered gesture: the
    ///   target grudge's current rigidity and resolution kind, its live effective magnitude (already
    ///   computed by the caller via <see cref="GrudgePolicy.EffectiveMagnitude" />, never recomputed
    ///   here), which gesture was offered, and the holder's traits.
    /// </summary>
    public sealed class GrudgeResolutionAttempt
    {
        /// <summary>The grudge's stored <see cref="Grudge.Rigidity" />, 0..100.</summary>
        public int Rigidity { get; set; }

        /// <summary>The grudge's <see cref="Grudge.ResolutionKind" />, the path it offers.</summary>
        public GrudgeResolutionKind ResolutionKind { get; set; }

        /// <summary>The grudge's live intensity today, already decayed (<see cref="GrudgePolicy.EffectiveMagnitude" />).</summary>
        public int EffectiveMagnitude { get; set; }

        /// <summary>Which gesture the player offered.</summary>
        public GrudgeGesture Gesture { get; set; }

        /// <summary>Gold/goods value of the gesture when <see cref="Gesture" /> is <see cref="GrudgeGesture.Gift" />; ignored (and should be left 0) otherwise.</summary>
        public int GiftValue { get; set; }

        /// <summary>The holder's trait levels.</summary>
        public TraitFacts Traits { get; set; }
    }

    /// <summary>
    ///   The verdict on one offered gesture: <see cref="GrudgeResolutionOutcome.Resolved" /> hands back
    ///   the FULL remaining <see cref="GrudgeResolutionAttempt.EffectiveMagnitude" /> as
    ///   <see cref="MagnitudeReduction" /> (so the caller can subtract it from the stored magnitude, or
    ///   just call <see cref="GrudgePolicy.Resolve" /> directly); <see cref="GrudgeResolutionOutcome.Softened" />
    ///   hands back a partial amount to subtract; <see cref="GrudgeResolutionOutcome.Rejected" /> hands
    ///   back zero.
    /// </summary>
    public sealed class GrudgeResolutionResult
    {
        /// <summary>The verdict.</summary>
        public GrudgeResolutionOutcome Outcome { get; set; }

        /// <summary>How many points to subtract from the grudge's stored magnitude. Always within [0, EffectiveMagnitude].</summary>
        public int MagnitudeReduction { get; set; }
    }

    /// <summary>
    ///   Pure rules deciding what happens when the player offers a <see cref="GrudgeGesture" /> toward a
    ///   grudge. Every constant here is a tuning knob, exactly like <see cref="GrudgePolicy" />'s.
    /// </summary>
    public static class GrudgeResolutionPolicy
    {
        // ── Gift bar ─────────────────────────────────────────────────────────

        /// <summary>Tuning: gold required per point of effective magnitude, at neutral rigidity and traits.</summary>
        public const double BaseGiftPerMagnitude = 4.0;

        /// <summary>Tuning: how much each rigidity point raises the gift price multiplier (rigidity 99, the highest non-vendetta value, triples the base price).</summary>
        public const double RigidityGiftPriceFactor = 0.02;

        /// <summary>Tuning: each positive Mercy level lowers the gift price multiplier (a forgiving holder asks less).</summary>
        public const double MercyGiftDiscountPerLevel = 0.15;

        /// <summary>Tuning: each positive Calculating level raises the gift price multiplier (tallies the debt precisely, demands full payment).</summary>
        public const double CalculatingGiftPremiumPerLevel = 0.2;

        /// <summary>Tuning: the gift price multiplier never drops below this floor, however merciful the holder.</summary>
        public const double MinGiftMultiplier = 0.3;

        /// <summary>Tuning: a below-bar gift never softens more than this fraction of the remaining effective magnitude, however close it comes.</summary>
        public const double MaxGiftSoftenFraction = 0.6;

        /// <summary>Tuning: a gift offered toward a grudge that does NOT want a gift (non-matching) counts at only this fraction of its face value against the bar.</summary>
        public const double NonMatchingGiftEffectivenessFraction = 0.5;

        // ── Non-monetary (Amends / Deed) effort bar ─────────────────────────

        /// <summary>Tuning: below this rigidity, one matching Amends or Deed gesture fully resolves the grudge; at/above it, the same gesture only softens (a very rigid grudge needs more than one gesture).</summary>
        public const int HighRigidityResolveCutoff = 40;

        /// <summary>Tuning: each positive Mercy level raises the one-shot-resolve cutoff (more grudges become winnable in a single gesture).</summary>
        public const int MercyResolveCutoffBonusPerLevel = 10;

        /// <summary>Tuning: each positive Calculating level lowers the one-shot-resolve cutoff (harder to close in one gesture, wants more).</summary>
        public const int CalculatingResolveCutoffPenaltyPerLevel = 10;

        /// <summary>Tuning: each positive Honor level raises the cutoff specifically for an Amends gesture (an honest apology lands hardest with a holder who values their own word).</summary>
        public const int HonorAmendsCutoffBonusPerLevel = 8;

        /// <summary>Tuning: fraction of remaining effective magnitude a matching-but-too-rigid Amends/Deed gesture softens (repeated gestures eventually wear the grudge down).</summary>
        public const double MatchingSoftenFraction = 0.35;

        /// <summary>Tuning: fraction of remaining effective magnitude a non-matching (but still positive) Amends/Deed/Gift gesture softens. Always smaller than <see cref="MatchingSoftenFraction" />: the wrong gesture never works as well as the right one.</summary>
        public const double NonMatchingSoftenFraction = 0.15;

        /// <summary>Tuning: extra softening fraction per positive Mercy level, applied to both the matching and non-matching non-monetary steps, and to the Time-kind nudge.</summary>
        public const double MercySoftenBoostPerLevel = 0.1;

        // ── Trait gates ──────────────────────────────────────────────────────

        /// <summary>Tuning: at/above this Calculating level, the holder demands the EXACT matching gesture; any non-matching gesture is rejected outright rather than merely softened, and even a generous kindness cannot nudge a Time-kind grudge.</summary>
        public const int CalculatingDemandsMatchLevel = 2;

        /// <summary>Tuning: at/below this Mercy level, a non-matching gesture is rejected outright rather than softened (an ungenerous holder will not credit the wrong gesture at all).</summary>
        public const int LowMercyRejectsNonMatchLevel = -1;

        // ── Time-kind nudge ──────────────────────────────────────────────────

        /// <summary>Tuning: fraction of remaining effective magnitude a generous Gift or Amends gesture nudges off a Time-kind grudge. "Time heals, but a kindness speeds it a little." A Deed never nudges a Time-kind grudge (its holder is waiting on the calendar, not a favour).</summary>
        public const double TimeNudgeFraction = 0.08;

        /// <summary>
        ///   The gold value required for a matching <see cref="GrudgeGesture.Gift" /> to fully resolve a
        ///   grudge with the given effective magnitude and rigidity, under these traits. Exposed so tests
        ///   (and any future UI wanting to preview "how much would this take") can assert the bar
        ///   directly, without reverse-engineering it from <see cref="Evaluate" />'s verdicts.
        /// </summary>
        public static int GiftValueRequired(int effectiveMagnitude, int rigidity, TraitFacts traits)
        {
            int magnitude = Clamp(effectiveMagnitude, 0, 100);
            int rigid = Clamp(rigidity, 0, 100);

            double mercy = traits?.Mercy ?? 0;
            double calculating = traits?.Calculating ?? 0;

            double multiplier = 1.0 + rigid * RigidityGiftPriceFactor;
            multiplier -= PositivePart(mercy) * MercyGiftDiscountPerLevel;
            multiplier += PositivePart(calculating) * CalculatingGiftPremiumPerLevel;

            if (multiplier < MinGiftMultiplier) multiplier = MinGiftMultiplier;

            return (int) Math.Ceiling(BaseGiftPerMagnitude * magnitude * multiplier);
        }

        /// <summary>
        ///   The verdict on one offered gesture. Vendetta/Unforgivable grudges always reject (no gesture
        ///   can buy or apologise a vendetta away); a Time-kind grudge is handled by its own rule (never
        ///   fully resolvable by a gesture, but a generous Gift or Amends nudges it); otherwise a
        ///   MATCHING gesture (Amends-to-Amends, Gift-to-Gift, Deed-to-Deed) is the strong path and can
        ///   fully resolve the grudge once it clears the effort bar, while a non-matching gesture only
        ///   ever softens (and less). High Calculating demands the exact matching gesture; low Mercy
        ///   rejects a non-matching one outright; high Mercy lowers every bar and speeds softening; high
        ///   Honor favours Amends specifically.
        /// </summary>
        public static GrudgeResolutionResult Evaluate(GrudgeResolutionAttempt attempt)
        {
            if (attempt == null) return Rejected();

            int effectiveMagnitude = Clamp(attempt.EffectiveMagnitude, 0, 100);
            if (effectiveMagnitude <= 0) return Rejected();

            if (attempt.ResolutionKind == GrudgeResolutionKind.Unforgivable || attempt.Rigidity >= GrudgePolicy.VendettaRigidity)
                return Rejected();

            TraitFacts traits = attempt.Traits;

            if (attempt.ResolutionKind == GrudgeResolutionKind.Time)
                return EvaluateTimeKind(attempt.Gesture, effectiveMagnitude, traits);

            bool matches = GestureMatchesKind(attempt.Gesture, attempt.ResolutionKind);

            if (!matches && IsNonMatchingGestureRejected(traits))
                return Rejected();

            return attempt.Gesture == GrudgeGesture.Gift
                ? EvaluateGift(attempt, effectiveMagnitude, traits, matches)
                : EvaluateNonMonetary(attempt, effectiveMagnitude, traits, matches);
        }

        private static bool GestureMatchesKind(GrudgeGesture gesture, GrudgeResolutionKind kind)
        {
            switch (gesture)
            {
                case GrudgeGesture.Amends: return kind == GrudgeResolutionKind.Amends;
                case GrudgeGesture.Gift:   return kind == GrudgeResolutionKind.Gift;
                case GrudgeGesture.Deed:   return kind == GrudgeResolutionKind.Deed;
                default:                   return false;
            }
        }

        private static bool IsNonMatchingGestureRejected(TraitFacts traits)
        {
            if (traits == null) return false;

            return traits.Calculating >= CalculatingDemandsMatchLevel || traits.Mercy <= LowMercyRejectsNonMatchLevel;
        }

        private static GrudgeResolutionResult EvaluateGift(GrudgeResolutionAttempt attempt, int effectiveMagnitude, TraitFacts traits, bool matches)
        {
            int bar = GiftValueRequired(effectiveMagnitude, attempt.Rigidity, traits);
            int giftValue = attempt.GiftValue < 0 ? 0 : attempt.GiftValue;

            if (giftValue <= 0) return Rejected();

            if (matches && giftValue >= bar) return Resolved(effectiveMagnitude);

            double coverage = bar <= 0 ? 1.0 : (double) giftValue / bar;
            if (coverage > 1.0) coverage = 1.0;
            if (!matches) coverage *= NonMatchingGiftEffectivenessFraction;

            int reduction = Clamp((int) Math.Round(effectiveMagnitude * coverage * MaxGiftSoftenFraction), 0, effectiveMagnitude);

            return reduction <= 0 ? Rejected() : Softened(reduction);
        }

        private static GrudgeResolutionResult EvaluateNonMonetary(GrudgeResolutionAttempt attempt, int effectiveMagnitude, TraitFacts traits, bool matches)
        {
            double mercy = traits?.Mercy ?? 0;
            double calculating = traits?.Calculating ?? 0;
            double honor = traits?.Honor ?? 0;

            double honorBonus = attempt.Gesture == GrudgeGesture.Amends ? PositivePart(honor) * HonorAmendsCutoffBonusPerLevel : 0;

            double cutoff = HighRigidityResolveCutoff
                + PositivePart(mercy) * MercyResolveCutoffBonusPerLevel
                - PositivePart(calculating) * CalculatingResolveCutoffPenaltyPerLevel
                + honorBonus;

            if (cutoff < 0) cutoff = 0;

            if (matches && attempt.Rigidity < cutoff) return Resolved(effectiveMagnitude);

            double stepFraction = (matches ? MatchingSoftenFraction : NonMatchingSoftenFraction) + PositivePart(mercy) * MercySoftenBoostPerLevel;
            int reduction = Clamp((int) Math.Round(effectiveMagnitude * stepFraction), 0, effectiveMagnitude);

            return reduction <= 0 ? Rejected() : Softened(reduction);
        }

        private static GrudgeResolutionResult EvaluateTimeKind(GrudgeGesture gesture, int effectiveMagnitude, TraitFacts traits)
        {
            if (gesture == GrudgeGesture.Deed) return Rejected();

            double calculating = traits?.Calculating ?? 0;
            if (calculating >= CalculatingDemandsMatchLevel) return Rejected();

            double mercy = traits?.Mercy ?? 0;
            double fraction = TimeNudgeFraction + PositivePart(mercy) * MercySoftenBoostPerLevel;

            int reduction = Clamp((int) Math.Round(effectiveMagnitude * fraction), 0, effectiveMagnitude);

            return reduction <= 0 ? Rejected() : Softened(reduction);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static GrudgeResolutionResult Rejected() => new GrudgeResolutionResult { Outcome = GrudgeResolutionOutcome.Rejected, MagnitudeReduction = 0 };

        private static GrudgeResolutionResult Resolved(int effectiveMagnitude) => new GrudgeResolutionResult { Outcome = GrudgeResolutionOutcome.Resolved, MagnitudeReduction = effectiveMagnitude };

        private static GrudgeResolutionResult Softened(int reduction) => new GrudgeResolutionResult { Outcome = GrudgeResolutionOutcome.Softened, MagnitudeReduction = reduction };

        private static double PositivePart(double value) => value > 0 ? value : 0;

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;

            return value > max ? max : value;
        }
    }
}
