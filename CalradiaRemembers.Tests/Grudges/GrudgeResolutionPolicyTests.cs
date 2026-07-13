// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: decides whether a player's offered gesture (Amends, Gift or Deed) toward a
// held grudge Resolves it, Softens it, or is Rejected. The verdict feeds GrudgeResolutionService.TryResolveDominant,
// which mutates the live Grudge, bumps the holder's personal regard, and records a chronicle "made peace" line.
// Get a verdict wrong here and a player either pays a price and is cheated of the mended-grudge payoff the whole
// pillar exists for, or reconciles too cheaply and the "regard sets how hard resolution is" master rule
// (ROADMAP, Grudges pillar) stops meaning anything. Vendetta and Time-kind grudges are the hard edges: gold or
// apology must never buy either, only the calendar (or nothing at all) answers for them.

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
      // Calculating raises the price (GiftValueRequired), but a holder who is paid the raised price in full must
      // still close the book. Without this, "Calculating" would drift into a de facto vendetta (never resolvable),
      // contradicting the ROADMAP's "resolution wants payment or power": payment in full is meant to be sufficient,
      // not merely demanded.
      [Test]
      public void GIVEN_a_calculating_holder_WHEN_evaluating_a_matching_gift_at_the_raised_bar_THEN_it_still_resolves()
      {
         var calculating = new TraitFacts {Calculating = 2};
         int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, calculating);

         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, bar, calculating);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved, "a calculating holder demands the exact matching gesture, but full payment of what it asks still closes the book");
      }

      // Pins CalculatingDemandsMatchLevel: a Calculating holder must get zero credit for the wrong gesture, not
      // partial softening. Without the outright reject, offering any convenient gesture would still nudge
      // progress, defeating the "demands the exact matching gesture" design for this trait.
      [Test]
      public void GIVEN_a_calculating_holder_WHEN_evaluating_a_non_matching_gesture_THEN_it_is_rejected_outright()
      {
         GrudgeResolutionAttempt attempt = Attempt(
            20, GrudgeResolutionKind.Amends, 60, GrudgeGesture.Deed,
            traits: new TraitFacts {Calculating = GrudgeResolutionPolicy.CalculatingDemandsMatchLevel});

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "a calculating holder demands the exact matching gesture");
      }

      // Without this, EvaluateTimeKind's generous-gesture nudge would let a Calculating holder be bought off a
      // Time-kind grudge, contradicting "only the calendar answers for a Time-kind grudge" (TimeNudgeFraction's
      // own doc): the CalculatingDemandsMatchLevel gate must override the Time-kind branch's own nudge, not lose to it.
      [Test]
      public void GIVEN_a_calculating_holder_WHEN_evaluating_a_time_kind_grudge_THEN_even_a_generous_gesture_does_not_nudge_it()
      {
         GrudgeResolutionAttempt attempt = Attempt(
            20, GrudgeResolutionKind.Time, 50, GrudgeGesture.Amends,
            traits: new TraitFacts {Calculating = GrudgeResolutionPolicy.CalculatingDemandsMatchLevel});

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "a calculating holder demands the real thing (time), not a kindness");
      }

      // Pins two edges at once: the bar is a hard threshold (one gold short never resolves, however close), and
      // MaxGiftSoftenFraction caps softening even as coverage approaches 100%. Without the cap, a near-bar gift
      // would asymptotically approach a full resolve, letting the player nearly reconcile for slightly less than
      // the calculated price and hollowing out the point of having a bar at all.
      [Test]
      public void GIVEN_a_gift_covering_nearly_the_whole_bar_WHEN_evaluating_THEN_the_softening_is_still_capped_short_of_resolving()
      {
         int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());

         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, bar - 1);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "one gold short of the bar still does not close it");
         result.MagnitudeReduction.Should().Be(30).And.BeLessThan(50, "the reduction never exceeds the effective magnitude");
      }

      // A zero effective magnitude means GrudgePolicy's own decay already reads the grudge as mended (it no longer
      // surfaces). Without this reject, a stray make_amends attempt could still trigger
      // GrudgeResolutionService's relation bump and "made peace" chronicle line for a grievance that, by the
      // pillar's own decay rule, effectively no longer exists.
      [Test]
      public void GIVEN_a_grudge_already_fully_decayed_WHEN_evaluating_a_gesture_THEN_there_is_nothing_left_to_resolve()
      {
         GrudgeResolutionAttempt attempt = Attempt(effectiveMagnitude: 0, kind: GrudgeResolutionKind.Amends, gesture: GrudgeGesture.Amends);

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
      }

      // Pins LowMercyRejectsNonMatchLevel, the stinginess mirror of the Calculating gate above: "an ungenerous
      // holder will not credit the wrong gesture at all" (IsNonMatchingGestureRejected's own doc). Without it,
      // a low-Mercy holder would still soften on any gesture, same exploit as the Calculating case.
      [Test]
      public void GIVEN_a_low_mercy_holder_WHEN_evaluating_a_non_matching_gesture_THEN_it_is_rejected_outright()
      {
         GrudgeResolutionAttempt attempt = Attempt(
            20, GrudgeResolutionKind.Amends, 60, GrudgeGesture.Deed,
            traits: new TraitFacts {Mercy = GrudgeResolutionPolicy.LowMercyRejectsNonMatchLevel});

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "an ungenerous holder will not credit the wrong gesture at all");
      }

      // ── Non-monetary effort bar (Amends / Deed) ──────────────────────────

      // Pins the HighRigidityResolveCutoff boundary (one point below it still one-shots). This is the baseline
      // "reconcilable" case the whole pillar promises: a low-rigidity grudge (regard was decent) must actually be
      // mendable in a single correct gesture, or the "regard sets how hard resolution is" master rule has no floor.
      [Test]
      public void GIVEN_a_matching_gesture_below_the_resolve_cutoff_WHEN_evaluating_THEN_it_resolves_in_one_gesture()
      {
         GrudgeResolutionAttempt attempt = Attempt(
            GrudgeResolutionPolicy.HighRigidityResolveCutoff - 1,
            GrudgeResolutionKind.Amends,
            73,
            GrudgeGesture.Amends);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved);
         result.MagnitudeReduction.Should().Be(73, "a Resolved verdict always hands back the full remaining magnitude");
      }

      // ── Gift bar ─────────────────────────────────────────────────────────

      // The gift path's own resolve boundary (giftValue >= bar) and full-magnitude payout. This is what
      // GrudgeResolutionService reads to fire the relation bump and the "made peace" chronicle line, so it must
      // trigger exactly when the player paid the calculated price, not before and not short of it.
      [Test]
      public void GIVEN_a_matching_gift_at_or_above_the_bar_WHEN_evaluating_THEN_it_resolves_the_grudge()
      {
         int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());

         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, bar);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved);
         result.MagnitudeReduction.Should().Be(50);
      }

      // Pins the coverage * MaxGiftSoftenFraction formula: a player who pays half the price gets proportional
      // credit, not nothing. Without proportional softening, any below-bar gift would be wasted effort, punishing
      // the iterative "several gestures wear the grudge down" reconciliation the pillar is built around.
      [Test]
      public void GIVEN_a_matching_gift_below_the_bar_WHEN_evaluating_THEN_it_softens_proportional_to_how_much_of_the_bar_it_covered()
      {
         int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());

         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, bar / 2);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
         result.MagnitudeReduction.Should().Be(15, "half the bar covered, capped at MaxGiftSoftenFraction of the remaining magnitude");
      }

      // ── Non-matching gestures ────────────────────────────────────────────

      // Core "matching is the strong path" rule: a wrong-but-well-intentioned gesture must never fully resolve.
      // Without this ceiling, a player could always pick whichever gesture is cheapest instead of the one the
      // grudge's ResolutionKind actually calls for, making GrudgePolicy.ResolutionKindFor's per-source mapping
      // (Deed for DeliveredPrisoner, Gift for BastardBlackmail, etc.) meaningless.
      [Test]
      public void GIVEN_a_non_matching_but_positive_gesture_WHEN_evaluating_THEN_it_only_softens_never_resolves()
      {
         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Amends, 60, GrudgeGesture.Deed);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
         result.MagnitudeReduction.Should().Be(9);
      }

      // Pins NonMatchingSoftenFraction < MatchingSoftenFraction. Even at the softening level, the wrong gesture
      // must earn strictly less credit than the right one, or picking the correct ResolutionKind gesture (per the
      // source catalog) would carry no gameplay weight beyond the outright-reject cases above.
      [Test]
      public void GIVEN_a_non_matching_gesture_WHEN_compared_to_a_matching_one_THEN_it_softens_less()
      {
         GrudgeResolutionResult matching = GrudgeResolutionPolicy.Evaluate(
            Attempt(90, GrudgeResolutionKind.Deed, 60, GrudgeGesture.Deed));

         GrudgeResolutionResult nonMatching = GrudgeResolutionPolicy.Evaluate(
            Attempt(90, GrudgeResolutionKind.Deed, 60, GrudgeGesture.Amends));

         nonMatching.MagnitudeReduction.Should().BeLessThan(matching.MagnitudeReduction, "the wrong gesture never works as well as the right one");
      }

      // Evaluate is a public pure entrypoint, not only reached through GrudgeResolutionService's own null-checked
      // path; pins the safe default (Rejected, zero reduction) so any future caller cannot crash the "never
      // throws" flow the rest of the mod's grudge/relation code relies on.
      [Test]
      public void GIVEN_a_null_attempt_WHEN_evaluating_THEN_it_rejects()
      {
         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(null);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
         result.MagnitudeReduction.Should().Be(0);
      }

      // ── Clamping ─────────────────────────────────────────────────────────

      // Pins the documented class invariant on GrudgeResolutionResult (Resolved always hands back the FULL
      // remaining magnitude). GrudgeResolutionService relies on this to decide it can call GrudgePolicy.Resolve
      // directly rather than subtracting a partial amount; a mismatch here would silently leave a "resolved"
      // grudge's stored magnitude non-zero for any caller that trusted MagnitudeReduction instead.
      [Test]
      public void GIVEN_a_resolved_outcome_WHEN_evaluating_THEN_the_reduction_always_equals_the_full_effective_magnitude()
      {
         GrudgeResolutionAttempt attempt = Attempt(5, GrudgeResolutionKind.Amends, 37, GrudgeGesture.Amends);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved);
         result.MagnitudeReduction.Should().Be(37);
      }

      // Pins TimeNudgeFraction's own doc: "a Deed never nudges a Time-kind grudge, its holder is waiting on the
      // calendar, not a favour." Without this exclusion, a Deed (usually the highest-effort gesture) would feel
      // like it should count toward anything, breaking the deliberate design that only Gift/Amends (a kindness)
      // and time itself touch a Time-kind grudge.
      [Test]
      public void GIVEN_a_time_kind_grudge_WHEN_evaluating_a_deed_THEN_it_does_not_nudge_it_at_all()
      {
         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Time, 50, GrudgeGesture.Deed);

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "only the calendar answers for a Time-kind grudge, not a favour");
      }

      // ── Time-kind grudges ─────────────────────────────────────────────────

      // Uses an absurdly large gift (1,000,000) specifically to prove gold can never fully resolve a Time-kind
      // grudge, only nudge it: this guards sources like Jealousy/SwayExposed's hard tier and UnblessedMarriage,
      // which the design deliberately makes NOT buyable, only decay (or a small kindness) closes them.
      [Test]
      public void GIVEN_a_time_kind_grudge_WHEN_evaluating_a_gift_THEN_it_is_never_resolved_but_is_nudged_slightly()
      {
         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Time, 50, GrudgeGesture.Gift, 1000000);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "time heals, but a kindness speeds it a little");
         result.MagnitudeReduction.Should().Be(4);
      }

      // Complements the Deed test above: pins that Amends (a kindness), like Gift, qualifies for the Time-kind
      // nudge while Deed does not, confirming the gesture-type gate is "any kindness nudges it" rather than
      // "only a gift nudges it."
      [Test]
      public void GIVEN_a_time_kind_grudge_WHEN_evaluating_amends_THEN_it_is_also_nudged()
      {
         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Time, 50, GrudgeGesture.Amends);

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
      }

      // ── Vendetta / unforgivable ──────────────────────────────────────────

      // Pins the VendettaRigidity (100) boundary: "a vendetta cannot be bought or apologised away." This is the
      // hard end of the ROADMAP's ExecutedKin design (a PERMANENT grudge, infinite rigidity); even an absurd
      // giftValue (1,000,000) must not slip through, or a vendetta stops being permanent.
      [Test]
      public void GIVEN_a_vendetta_rigidity_WHEN_evaluating_any_gesture_THEN_it_is_always_rejected()
      {
         GrudgeResolutionAttempt attempt = Attempt(
            GrudgePolicy.VendettaRigidity,
            GrudgeResolutionKind.Gift,
            gesture: GrudgeGesture.Gift,
            giftValue: 1000000);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Rejected, "a vendetta cannot be bought or apologised away");
         result.MagnitudeReduction.Should().Be(0);
      }

      // This is the master rule from the ROADMAP made concrete: "one who dislikes them AND holds a grudge is far
      // harder to win over." At rigidity 90 (well above HighRigidityResolveCutoff, no trait help), even the
      // correct matching gesture must not one-shot resolve, or low regard would stop costing the player anything
      // extra to reconcile.
      [Test]
      public void GIVEN_a_very_rigid_matching_gesture_WHEN_evaluating_THEN_it_softens_rather_than_resolves_so_it_takes_several()
      {
         GrudgeResolutionAttempt attempt = Attempt(90, GrudgeResolutionKind.Deed, 60, GrudgeGesture.Deed);

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "a rigidity this high needs more than one gesture");
         result.MagnitudeReduction.Should().Be(21);
      }

      // Defensive floor on EvaluateGift's early-out: offering nothing must never count as an attempted gesture,
      // closing off a trivial exploit where a 0-value "gift" action still registers as an effort against the grudge.
      [Test]
      public void GIVEN_a_zero_value_gift_WHEN_evaluating_THEN_it_is_rejected()
      {
         GrudgeResolutionAttempt attempt = Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, 0);

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
      }

      // Pins MinGiftMultiplier (0.3): even an absurdly merciful holder (Mercy=10) must not have their gift bar
      // driven to near-zero. Without the floor, extreme trait rolls could let a grudge be resolved with a token
      // payment, cheapening the reconciliation gesture into meaninglessness regardless of how forgiving the holder is.
      [Test]
      public void GIVEN_an_extreme_mercy_value_WHEN_computing_the_gift_bar_THEN_the_price_multiplier_never_drops_below_the_tuned_floor()
      {
         int bar = GrudgeResolutionPolicy.GiftValueRequired(50, 0, new TraitFacts {Mercy = 10});

         bar.Should().Be(60, "the multiplier floors at MinGiftMultiplier (0.3) however merciful the holder");
      }

      // GrudgePolicy.ResolutionKindFor maps ExecutedKin (and anything that reached VendettaRigidity) to
      // Unforgivable independent of the grudge's numeric rigidity. Pins that the KIND alone vetoes resolution here
      // (rigidity=10, well below any cutoff): guards against a future rigidity recalculation (decay, trait shift)
      // accidentally reopening a grudge that was deliberately marked eternally unforgivable.
      [Test]
      public void GIVEN_an_unforgivable_resolution_kind_WHEN_evaluating_a_gesture_THEN_it_is_rejected_even_at_low_rigidity()
      {
         GrudgeResolutionAttempt attempt = Attempt(10, GrudgeResolutionKind.Unforgivable, gesture: GrudgeGesture.Amends);

         GrudgeResolutionPolicy.Evaluate(attempt).Outcome.Should().Be(GrudgeResolutionOutcome.Rejected);
      }

      // Pins the other half of GrudgeResolutionResult's documented contract (reduction always within
      // [0, EffectiveMagnitude]). GrudgeResolutionService subtracts MagnitudeReduction straight from the stored
      // Magnitude; without this guarantee an over-large reduction could drive it negative before the caller's own
      // Math.Max(0, ...) catches it, an extra safety net this test proves is unnecessary.
      [Test]
      public void GIVEN_any_softened_outcome_WHEN_evaluating_THEN_the_reduction_never_exceeds_the_effective_magnitude()
      {
         GrudgeResolutionAttempt attempt = Attempt(90, GrudgeResolutionKind.Deed, 60, GrudgeGesture.Deed, traits: new TraitFacts {Mercy = 2});

         GrudgeResolutionResult result = GrudgeResolutionPolicy.Evaluate(attempt);

         result.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
         result.MagnitudeReduction.Should().BeLessThanOrEqualTo(60);
      }

      // Pins CalculatingGiftPremiumPerLevel's direction: per the ROADMAP's trait-modulated susceptibility section,
      // "high Calculating = forms easily, never decays, resolution wants payment or power." A calculating holder
      // tallying the debt precisely must charge MORE, not less, or the trait would read backwards in play.
      [Test]
      public void GIVEN_high_calculating_WHEN_computing_the_gift_bar_THEN_it_is_higher_than_a_plain_holders()
      {
         int plainBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());
         int calculatingBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, new TraitFacts {Calculating = 2});

         calculatingBar.Should().BeGreaterThan(plainBar, "a calculating holder tallies the debt precisely and wants full payment");
      }

      // Pins HonorAmendsCutoffBonusPerLevel against the ROADMAP: "high Honor = grudges broken WORD above all...
      // yet honours amends rituals." The bonus must be strong enough to flip a real case from Softened to
      // Resolved (not just move the cutoff number in isolation), or the trait would never be felt at the table.
      [Test]
      public void GIVEN_high_honor_WHEN_evaluating_a_matching_amends_gesture_THEN_it_resolves_a_grudge_a_plain_holder_would_only_soften()
      {
         GrudgeResolutionResult plain = GrudgeResolutionPolicy.Evaluate(
            Attempt(44, GrudgeResolutionKind.Amends, 50, GrudgeGesture.Amends, traits: PlainTraits()));

         GrudgeResolutionResult honorable = GrudgeResolutionPolicy.Evaluate(
            Attempt(44, GrudgeResolutionKind.Amends, 50, GrudgeGesture.Amends, traits: new TraitFacts {Honor = 2}));

         plain.Outcome.Should().Be(GrudgeResolutionOutcome.Softened);
         honorable.Outcome.Should().Be(GrudgeResolutionOutcome.Resolved, "an honest apology lands hardest with a holder who values their own word");
      }

      // Guards that the Honor bonus is scoped to the Amends gesture only (EvaluateNonMonetary only adds
      // honorBonus when Gesture == Amends). Without this scoping, Honor would inflate the cutoff for Deed too,
      // diluting the ROADMAP's specific "amends rituals" framing into a blanket buff the design never intended.
      [Test]
      public void GIVEN_high_honor_WHEN_evaluating_a_matching_deed_gesture_THEN_the_bonus_does_not_apply()
      {
         GrudgeResolutionResult honorable = GrudgeResolutionPolicy.Evaluate(
            Attempt(44, GrudgeResolutionKind.Deed, 50, GrudgeGesture.Deed, traits: new TraitFacts {Honor = 2}));

         honorable.Outcome.Should().Be(GrudgeResolutionOutcome.Softened, "the Honor bonus is specific to Amends, a real deed gets no such lift");
      }

      // Pins MercyGiftDiscountPerLevel's direction, matching the ROADMAP: "high Mercy = forgiving (high
      // threshold, fast decay, a sincere amend suffices)." A forgiving holder must ask for LESS, the mirror image
      // of the Calculating test above.
      [Test]
      public void GIVEN_high_mercy_WHEN_computing_the_gift_bar_THEN_it_is_lower_than_a_plain_holders()
      {
         int plainBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, PlainTraits());
         int mercifulBar = GrudgeResolutionPolicy.GiftValueRequired(50, 20, new TraitFacts {Mercy = 2});

         mercifulBar.Should().BeLessThan(plainBar, "a forgiving holder asks less");
      }

      // Complements the bar-only test above: guards that Mercy's discount actually changes what the SAME gift
      // achieves in Evaluate, not just the abstract GiftValueRequired number. A forgiving holder's lower bar must
      // be felt in-session as more softening for an identical offer, or the trait would be invisible to the player.
      [Test]
      public void GIVEN_high_mercy_WHEN_evaluating_the_same_below_bar_gift_THEN_it_softens_more_than_a_plain_holders()
      {
         GrudgeResolutionResult plain = GrudgeResolutionPolicy.Evaluate(
            Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, 150, PlainTraits()));

         GrudgeResolutionResult merciful = GrudgeResolutionPolicy.Evaluate(
            Attempt(20, GrudgeResolutionKind.Gift, 50, GrudgeGesture.Gift, 150, new TraitFacts {Mercy = 2}));

         merciful.MagnitudeReduction.Should().BeGreaterThan(plain.MagnitudeReduction, "the same gift covers more of a merciful holder's lower bar");
      }

      #region private

      private static GrudgeResolutionAttempt Attempt(
         int rigidity = 20,
         GrudgeResolutionKind kind = GrudgeResolutionKind.Amends,
         int effectiveMagnitude = 50,
         GrudgeGesture gesture = GrudgeGesture.Amends,
         int giftValue = 0,
         TraitFacts? traits = null)
         => new() {
            Rigidity = rigidity,
            ResolutionKind = kind,
            EffectiveMagnitude = effectiveMagnitude,
            Gesture = gesture,
            GiftValue = giftValue,
            Traits = traits ?? PlainTraits()
         };

      private static TraitFacts PlainTraits() => new();

      #endregion
   }
}