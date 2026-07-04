// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: what happens when the player offers a gesture (Amends/Gift/Deed)
// toward a grudge whose ResolutionKind GrudgePolicy already decided. See GrudgePolicyTests for the
// formation/rigidity/decay half of the pillar.

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
    [TestFixture]
    public sealed class GrudgeResolutionPolicyTests
    {
        private static TraitFacts PlainTraits() => new TraitFacts();

        private static GrudgeResolutionAttempt Attempt(
            int rigidity = 20,
            GrudgeResolutionKind kind = GrudgeResolutionKind.Amends,
            int effectiveMagnitude = 50,
            GrudgeGesture gesture = GrudgeGesture.Amends,
            int giftValue = 0,
            TraitFacts? traits = null)
            => new GrudgeResolutionAttempt
            {
                Rigidity = rigidity,
                ResolutionKind = kind,
                EffectiveMagnitude = effectiveMagnitude,
                Gesture = gesture,
                GiftValue = giftValue,
                Traits = traits ?? PlainTraits()
            };

        // ── Vendetta / unforgivable ──────────────────────────────────────────

        [Test]
        public void GIVEN_a_vendetta_rigidity_WHEN_evaluating_any_gesture_THEN_it_is_always_rejected()
        {
            GrudgeResolutionAttempt attempt = Attempt(
                rigidity: GrudgePolicy.VendettaRigidity,
                kind: GrudgeResolutionKind.Gift,
                gesture: GrudgeGesture.Gift,
                giftValue: 1000000);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "a vendetta cannot be bought or apologised away");
            result.MagnitudeReduction.Should().Be(0);
        }

        [Test]
        public void GIVEN_an_unforgivable_resolution_kind_WHEN_evaluating_a_gesture_THEN_it_is_rejected_even_at_low_rigidity()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 10, kind: GrudgeResolutionKind.Unforgivable, gesture: GrudgeGesture.Amends);

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
        }

        [Test]
        public void GIVEN_a_null_attempt_WHEN_evaluating_THEN_it_rejects()
        {
            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(null);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
            result.MagnitudeReduction.Should().Be(0);
        }

        [Test]
        public void GIVEN_a_grudge_already_fully_decayed_WHEN_evaluating_a_gesture_THEN_there_is_nothing_left_to_resolve()
        {
            GrudgeResolutionAttempt attempt = Attempt(effectiveMagnitude: 0, kind: GrudgeResolutionKind.Amends, gesture: GrudgeGesture.Amends);

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
        }

        // ── Gift bar ─────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_matching_gift_at_or_above_the_bar_WHEN_evaluating_THEN_it_resolves_the_grudge()
        {
            int bar = GrudgeResolutionPolicy.GiftValueRequired(effectiveMagnitude: 50, rigidity: 20, PlainTraits());

            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: bar);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved);
            result.MagnitudeReduction.Should().Be(50);
        }

        [Test]
        public void GIVEN_a_matching_gift_below_the_bar_WHEN_evaluating_THEN_it_softens_proportional_to_how_much_of_the_bar_it_covered()
        {
            int bar = GrudgeResolutionPolicy.GiftValueRequired(effectiveMagnitude: 50, rigidity: 20, PlainTraits());

            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: bar / 2);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
            result.MagnitudeReduction.Should().Be(15, "half the bar covered, capped at MaxGiftSoftenFraction of the remaining magnitude");
        }

        [Test]
        public void GIVEN_a_gift_covering_nearly_the_whole_bar_WHEN_evaluating_THEN_the_softening_is_still_capped_short_of_resolving()
        {
            int bar = GrudgeResolutionPolicy.GiftValueRequired(effectiveMagnitude: 50, rigidity: 20, PlainTraits());

            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: bar - 1);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "one gold short of the bar still does not close it");
            result.MagnitudeReduction.Should().Be(30).And.BeLessThan(50, "the reduction never exceeds the effective magnitude");
        }

        [Test]
        public void GIVEN_a_zero_value_gift_WHEN_evaluating_THEN_it_is_rejected()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: 0);

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
        }

        [Test]
        public void GIVEN_high_mercy_WHEN_computing_the_gift_bar_THEN_it_is_lower_than_a_plain_holders()
        {
            int plainBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());
            int mercifulBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, new TraitFacts { Mercy = 2 });

            mercifulBar.Should().BeLessThan(plainBar, "a forgiving holder asks less");
        }

        [Test]
        public void GIVEN_high_calculating_WHEN_computing_the_gift_bar_THEN_it_is_higher_than_a_plain_holders()
        {
            int plainBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());
            int calculatingBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, new TraitFacts { Calculating = 2 });

            calculatingBar.Should().BeGreaterThan(plainBar, "a calculating holder tallies the debt precisely and wants full payment");
        }

        [Test]
        public void GIVEN_an_extreme_mercy_value_WHEN_computing_the_gift_bar_THEN_the_price_multiplier_never_drops_below_the_tuned_floor()
        {
            int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 0, new TraitFacts { Mercy = 10 });

            bar.Should().Be(60, "the multiplier floors at MinGiftMultiplier (0.3) however merciful the holder");
        }

        [Test]
        public void GIVEN_a_calculating_holder_WHEN_evaluating_a_matching_gift_at_the_raised_bar_THEN_it_still_resolves()
        {
            var calculating = new TraitFacts { Calculating = 2 };
            int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, calculating);

            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: bar, traits: calculating);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved, "a calculating holder demands the exact matching gesture, but full payment of what it asks still closes the book");
        }

        [Test]
        public void GIVEN_high_mercy_WHEN_evaluating_the_same_below_bar_gift_THEN_it_softens_more_than_a_plain_holders()
        {
            GrudgeResolutionResult plain = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: 150, traits: PlainTraits()));

            GrudgeResolutionResult merciful = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 20, kind: GrudgeResolutionKind.Gift, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: 150, traits: new TraitFacts { Mercy = 2 }));

            merciful.MagnitudeReduction.Should().BeGreaterThan(plain.MagnitudeReduction, "the same gift covers more of a merciful holder's lower bar");
        }

        // ── Non-matching gestures ────────────────────────────────────────────

        [Test]
        public void GIVEN_a_non_matching_but_positive_gesture_WHEN_evaluating_THEN_it_only_softens_never_resolves()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Amends, effectiveMagnitude: 60, gesture: GrudgeGesture.Deed);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
            result.MagnitudeReduction.Should().Be(9);
        }

        [Test]
        public void GIVEN_a_non_matching_gesture_WHEN_compared_to_a_matching_one_THEN_it_softens_less()
        {
            GrudgeResolutionResult matching = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 90, kind: GrudgeResolutionKind.Deed, effectiveMagnitude: 60, gesture: GrudgeGesture.Deed));

            GrudgeResolutionResult nonMatching = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 90, kind: GrudgeResolutionKind.Deed, effectiveMagnitude: 60, gesture: GrudgeGesture.Amends));

            nonMatching.MagnitudeReduction.Should().BeLessThan(matching.MagnitudeReduction, "the wrong gesture never works as well as the right one");
        }

        [Test]
        public void GIVEN_a_calculating_holder_WHEN_evaluating_a_non_matching_gesture_THEN_it_is_rejected_outright()
        {
            GrudgeResolutionAttempt attempt = Attempt(
                rigidity: 20, kind: GrudgeResolutionKind.Amends, effectiveMagnitude: 60, gesture: GrudgeGesture.Deed,
                traits: new TraitFacts { Calculating = GrudgeResolutionPolicy.CalculatingDemandsMatchLevel });

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "a calculating holder demands the exact matching gesture");
        }

        [Test]
        public void GIVEN_a_low_mercy_holder_WHEN_evaluating_a_non_matching_gesture_THEN_it_is_rejected_outright()
        {
            GrudgeResolutionAttempt attempt = Attempt(
                rigidity: 20, kind: GrudgeResolutionKind.Amends, effectiveMagnitude: 60, gesture: GrudgeGesture.Deed,
                traits: new TraitFacts { Mercy = GrudgeResolutionPolicy.LowMercyRejectsNonMatchLevel });

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "an ungenerous holder will not credit the wrong gesture at all");
        }

        // ── Non-monetary effort bar (Amends / Deed) ──────────────────────────

        [Test]
        public void GIVEN_a_matching_gesture_below_the_resolve_cutoff_WHEN_evaluating_THEN_it_resolves_in_one_gesture()
        {
            GrudgeResolutionAttempt attempt = Attempt(
                rigidity: GrudgeResolutionPolicy.HighRigidityResolveCutoff - 1,
                kind: GrudgeResolutionKind.Amends,
                effectiveMagnitude: 73,
                gesture: GrudgeGesture.Amends);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved);
            result.MagnitudeReduction.Should().Be(73, "a Resolved verdict always hands back the full remaining magnitude");
        }

        [Test]
        public void GIVEN_a_very_rigid_matching_gesture_WHEN_evaluating_THEN_it_softens_rather_than_resolves_so_it_takes_several()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 90, kind: GrudgeResolutionKind.Deed, effectiveMagnitude: 60, gesture: GrudgeGesture.Deed);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "a rigidity this high needs more than one gesture");
            result.MagnitudeReduction.Should().Be(21);
        }

        [Test]
        public void GIVEN_high_honor_WHEN_evaluating_a_matching_amends_gesture_THEN_it_resolves_a_grudge_a_plain_holder_would_only_soften()
        {
            GrudgeResolutionResult plain = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 44, kind: GrudgeResolutionKind.Amends, effectiveMagnitude: 50, gesture: GrudgeGesture.Amends, traits: PlainTraits()));

            GrudgeResolutionResult honorable = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 44, kind: GrudgeResolutionKind.Amends, effectiveMagnitude: 50, gesture: GrudgeGesture.Amends, traits: new TraitFacts { Honor = 2 }));

            plain.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
            honorable.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved, "an honest apology lands hardest with a holder who values their own word");
        }

        [Test]
        public void GIVEN_high_honor_WHEN_evaluating_a_matching_deed_gesture_THEN_the_bonus_does_not_apply()
        {
            GrudgeResolutionResult honorable = GrudgeResolutionPolicy.Evaluate(
                Attempt(rigidity: 44, kind: GrudgeResolutionKind.Deed, effectiveMagnitude: 50, gesture: GrudgeGesture.Deed, traits: new TraitFacts { Honor = 2 }));

            honorable.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "the Honor bonus is specific to Amends, a real deed gets no such lift");
        }

        // ── Time-kind grudges ─────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_time_kind_grudge_WHEN_evaluating_a_gift_THEN_it_is_never_resolved_but_is_nudged_slightly()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Time, effectiveMagnitude: 50, gesture: GrudgeGesture.Gift, giftValue: 1000000);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "time heals, but a kindness speeds it a little");
            result.MagnitudeReduction.Should().Be(4);
        }

        [Test]
        public void GIVEN_a_time_kind_grudge_WHEN_evaluating_amends_THEN_it_is_also_nudged()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Time, effectiveMagnitude: 50, gesture: GrudgeGesture.Amends);

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
        }

        [Test]
        public void GIVEN_a_time_kind_grudge_WHEN_evaluating_a_deed_THEN_it_does_not_nudge_it_at_all()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 20, kind: GrudgeResolutionKind.Time, effectiveMagnitude: 50, gesture: GrudgeGesture.Deed);

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "only the calendar answers for a Time-kind grudge, not a favour");
        }

        [Test]
        public void GIVEN_a_calculating_holder_WHEN_evaluating_a_time_kind_grudge_THEN_even_a_generous_gesture_does_not_nudge_it()
        {
            GrudgeResolutionAttempt attempt = Attempt(
                rigidity: 20, kind: GrudgeResolutionKind.Time, effectiveMagnitude: 50, gesture: GrudgeGesture.Amends,
                traits: new TraitFacts { Calculating = GrudgeResolutionPolicy.CalculatingDemandsMatchLevel });

            GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "a calculating holder demands the real thing (time), not a kindness");
        }

        // ── Clamping ─────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_resolved_outcome_WHEN_evaluating_THEN_the_reduction_always_equals_the_full_effective_magnitude()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 5, kind: GrudgeResolutionKind.Amends, effectiveMagnitude: 37, gesture: GrudgeGesture.Amends);

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved);
            result.MagnitudeReduction.Should().Be(37);
        }

        [Test]
        public void GIVEN_any_softened_outcome_WHEN_evaluating_THEN_the_reduction_never_exceeds_the_effective_magnitude()
        {
            GrudgeResolutionAttempt attempt = Attempt(rigidity: 90, kind: GrudgeResolutionKind.Deed, effectiveMagnitude: 60, gesture: GrudgeGesture.Deed, traits: new TraitFacts { Mercy = 2 });

            GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

            result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
            result.MagnitudeReduction.Should().BeLessThanOrEqualTo(60);
        }
    }
}
