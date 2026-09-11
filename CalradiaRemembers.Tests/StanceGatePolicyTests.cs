// Code written by Gabriel Mailhot, 04/07/2026.
// Stance mechanical behaviour-gating, increment 1: protects the Fear-coercion thresholds ratified
// 2026-07-04 — a terrified captive yields softer terms but plants a HIDDEN grudge instead of open
// regard, Honor/Valor stiffen resolve, Calculating breaks sooner and resents it more, and an unafraid,
// contemptuous captive refuses outright. Gabriel reads these as the tuning's documentation.
//
// Also protects Trust/Respect/Affection (increments 2-4, ratified 2026-07-04/05): WillDelegateRisk
// gates GiveInfluenceVerb/LendTroopsVerb, CommandsRespect/CommandsRespectToJoinClan gate join_clan.
// Each threshold guards both the prompt-side ELIGIBILITY check (the LLM is never taught a verb it
// cannot deliver) and the bridge-side EXECUTION guard (re-reads live stance before committing).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceGatePolicyTests
    {
        // ── Yield ──────────────────────────────────────────────────────────────

        // Wired into ExecuteFreePrisoner (BannerlordGameStateBridge): a captive scared enough must both
        // soften the ransom AND plant a HIDDEN grudge (CoercedRelease), never open regard: the fiction
        // owns that coercion breeds secret resentment, not gratitude.
        [Test]
        public void GIVEN_a_terrified_captive_with_neutral_traits_WHEN_resolving_coercion_THEN_they_yield_softer_terms_with_a_hidden_grudge()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 40, Respect = 0, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
            result.RansomMultiplier.Should().Be(StanceGatePolicy.RansomSoftenMultiplier);
            result.HiddenGrudgeMagnitude.Should().Be(StanceGatePolicy.CoercedGrudgeMagnitude);
        }

        // YieldFearFloor is inclusive (>=): pins that fear sitting exactly on the tuning value already
        // breaks resolve, so a future retune can't silently flip this boundary case to Neutral.
        [Test]
        public void GIVEN_exactly_the_yield_fear_floor_WHEN_resolving_coercion_THEN_it_still_counts_as_afraid_enough()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = StanceGatePolicy.YieldFearFloor, Respect = 50, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
        }

        // The far side of the same boundary: one point under the floor must NOT yield, confirming the
        // policy compares by ordering (>=) and not merely by equality with the floor.
        [Test]
        public void GIVEN_one_fear_point_below_the_floor_WHEN_resolving_coercion_THEN_they_do_not_yield()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = StanceGatePolicy.YieldFearFloor - 1, Respect = 50, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().NotBe(CoercionOutcome.YieldsSofterTerms);
        }

        // ── Honor/Valor stiffen resolve ──────────────────────────────────────

        // Only POSITIVE Honor/Valor stiffen resolve (StanceGatePolicy's own doc): the same Fear that broke
        // a neutral captive must no longer suffice once the floor is raised, or a valorous lord would cave
        // to coercion exactly as easily as a coward.
        [Test]
        public void GIVEN_an_honourable_and_valorous_captive_WHEN_the_same_fear_no_longer_crosses_the_raised_floor_THEN_they_stay_neutral_not_yielding()
        {
            // The same fear (40) that broke a neutral-traited captive no longer suffices once
            // Honor/Valor have raised the effective floor.
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 40, Respect = 50, Honor = 2, Valor = 2, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        // Guards against an absolute-immunity bug: a raised floor makes a resolute captive HARDER to
        // break, not unbreakable, at Fear 100 even the stiffest resolve must still give way.
        [Test]
        public void GIVEN_an_honourable_and_valorous_captive_WHEN_fear_climbs_past_their_raised_floor_THEN_they_still_eventually_yield()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 100, Respect = 50, Honor = 2, Valor = 2, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
        }

        // ── Calculating breaks sooner and resents it more ────────────────────

        // Only POSITIVE Calculating eases the floor (a schemer breaks sooner): 30 Fear must stay Neutral
        // for a neutral-traited captive but already break a Calculating one, or the trait would have no
        // mechanical effect at all.
        [Test]
        public void GIVEN_a_calculating_captive_WHEN_resolving_coercion_THEN_they_yield_at_a_lower_fear_than_a_neutral_captive_would()
        {
            // 30 fear does not break a neutral captive (floor 40) but does break a calculating one
            // (floor lowered by 2 * CalculatingYieldPerLevel).
            CoercionResult neutralTraits = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 30, Respect = 50, Honor = 0, Valor = 0, Calculating = 0});
            CoercionResult calculating = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 30, Respect = 50, Honor = 0, Valor = 0, Calculating = 2});

            neutralTraits.Outcome.Should().Be(CoercionOutcome.Neutral);
            calculating.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
        }

        // "A schemer holds a grudge tighter" (StanceGatePolicy's own doc): a Calculating captive who yields
        // must resent it MORE, via CalculatingGrudgeBonusPerLevel. Get this wrong and the hidden grudge
        // planted on ExecuteFreePrisoner understates exactly the personality it is meant to capture.
        [Test]
        public void GIVEN_a_calculating_captive_WHEN_they_yield_THEN_the_hidden_grudge_carries_a_bigger_magnitude()
        {
            CoercionResult neutralTraits = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 40, Respect = 0, Honor = 0, Valor = 0, Calculating = 0});
            CoercionResult calculating = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 40, Respect = 0, Honor = 0, Valor = 0, Calculating = 2});

            calculating.HiddenGrudgeMagnitude.Should().BeGreaterThan(neutralTraits.HiddenGrudgeMagnitude);
            calculating.HiddenGrudgeMagnitude.Should().Be(
                StanceGatePolicy.CoercedGrudgeMagnitude + 2 * StanceGatePolicy.CalculatingGrudgeBonusPerLevel);
        }

        // Sign guard: only positive Calculating may ease the floor or inflate the grudge, a negative value
        // must be inert, never accidentally stiffen resolve or blunt the resentment the wrong way.
        [Test]
        public void GIVEN_a_negative_calculating_trait_WHEN_resolving_coercion_THEN_it_does_not_raise_the_floor_the_wrong_way()
        {
            // Only POSITIVE Calculating eases the floor; a negative value must not stiffen it either.
            CoercionResult neutralTraits = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 40, Respect = 0, Honor = 0, Valor = 0, Calculating = 0});
            CoercionResult negativeCalculating = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 40, Respect = 0, Honor = 0, Valor = 0, Calculating = -2});

            negativeCalculating.Outcome.Should().Be(neutralTraits.Outcome);
            negativeCalculating.HiddenGrudgeMagnitude.Should().Be(neutralTraits.HiddenGrudgeMagnitude);
        }

        // ── Refuse flat ───────────────────────────────────────────────────────

        // Wired into ExecuteFreePrisoner: RefusesFlat blocks the release outright (the bargain is declined,
        // the captive stays put). Get this wrong and either a genuinely fearless, contemptuous captive can
        // still be ransomed out for free, or a legitimately releasable one gets stuck refusing forever.
        [Test]
        public void GIVEN_an_unafraid_and_contemptuous_captive_WHEN_resolving_coercion_THEN_they_refuse_flat()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 5, Respect = 0, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.RefusesFlat);
            result.RansomMultiplier.Should().Be(1.0);
            result.HiddenGrudgeMagnitude.Should().Be(0);
        }

        // RefuseFearCeiling is exclusive (fear must be strictly BELOW it): pins that sitting exactly on the
        // ceiling already counts as "not unafraid enough", so the refusal path can't creep past its bound.
        [Test]
        public void GIVEN_fear_exactly_at_the_refuse_ceiling_WHEN_resolving_coercion_THEN_it_no_longer_qualifies_as_unafraid_enough_to_refuse()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = StanceGatePolicy.RefuseFearCeiling, Respect = 0, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        // Same exclusivity on the Respect side of the refuse-flat pair: sitting exactly on
        // RefuseRespectCeiling already counts as too esteeming to refuse outright.
        [Test]
        public void GIVEN_respect_exactly_at_the_refuse_ceiling_WHEN_resolving_coercion_THEN_it_no_longer_qualifies_as_contemptuous_enough_to_refuse()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 5, Respect = StanceGatePolicy.RefuseRespectCeiling, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        // RefusesFlat needs BOTH low Fear AND low Respect (an AND, not an OR): unafraid alone is not
        // contempt, a captive can be fearless yet still respect the player too much to flatly refuse.
        [Test]
        public void GIVEN_low_fear_but_meaningful_respect_WHEN_resolving_coercion_THEN_they_do_not_refuse_outright()
        {
            // Unafraid, but they still respect the player too much to flatly refuse.
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 5, Respect = 20, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        // ── Neutral middle ground ─────────────────────────────────────────────

        // The default path a coercion attempt takes most of the time: neither scared enough to yield nor
        // fearless-and-contemptuous enough to refuse. Pins that this case leaves the bargain's own terms
        // (ransom, grudge) completely untouched.
        [Test]
        public void GIVEN_a_mid_range_captive_WHEN_resolving_coercion_THEN_the_outcome_stays_neutral()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 25, Respect = 30, Honor = 0, Valor = 0, Calculating = 0});

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
            result.RansomMultiplier.Should().Be(1.0);
            result.HiddenGrudgeMagnitude.Should().Be(0);
        }

        // ── Effective floor clamp ─────────────────────────────────────────────

        // Defensive test on the clamp (Clamp(effectiveFloor, RefuseFearCeiling, 100)): an extreme raised
        // floor must not make ResolveCoercion misbehave (throw/overflow) at Fear's own maximum, only that
        // it degrades gracefully to Neutral or, at the ceiling, still yields.
        [Test]
        public void GIVEN_extreme_honor_and_valor_WHEN_resolving_coercion_THEN_the_effective_floor_never_exceeds_full_fear()
        {
            // Even a very high raised floor cannot be crossed by more than maximum Fear (100) — a
            // captive this resolute simply never yields to fear alone, but the policy must not
            // misbehave (throw, overflow) at the extreme.
            CoercionResult result = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = 100, Respect = 0, Honor = 2, Valor = 2, Calculating = 0});

            result.Outcome.Should().BeOneOf(CoercionOutcome.YieldsSofterTerms, CoercionOutcome.Neutral);
        }

        // ── Trust (DELEGATED RISK), increment 2 ──────────────────────────────

        // Feeds GiveInfluenceVerb/LendTroopsVerb (Trust-1): above the floor, an NPC with proven Trust lends
        // its OWN troops/influence to the player. Wrong here either blocks an earned favor or hands out a
        // resource-exposing verb to an NPC that never proved itself.
        [Test]
        public void GIVEN_trust_above_the_delegated_risk_floor_WHEN_checking_delegated_risk_THEN_the_npc_will_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = StanceGatePolicy.DelegatedRiskTrustFloor + 1, Relation = 0, Affection = 0}).Should().BeTrue();
        }

        // DelegatedRiskTrustFloor is inclusive (>=): pins the boundary so a future retune can't silently
        // flip the exact floor value to "not trusted enough".
        [Test]
        public void GIVEN_trust_exactly_at_the_delegated_risk_floor_WHEN_checking_delegated_risk_THEN_it_still_counts_as_trusted_enough()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = StanceGatePolicy.DelegatedRiskTrustFloor, Relation = 0, Affection = 0}).Should().BeTrue();
        }

        // The far side of the same boundary, with both other doors (relation, affection) also shut: one
        // point under Trust alone must not be enough.
        [Test]
        public void GIVEN_trust_one_point_below_the_floor_and_no_relation_nor_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = StanceGatePolicy.DelegatedRiskTrustFloor - 1, Relation = 0, Affection = 0}).Should().BeFalse();
        }

        // Default-deny baseline: an NPC with nothing proven, no established relation, and no personal
        // loyalty must never delegate risk: "prove it first" is the rule this whole gate exists to enforce.
        [Test]
        public void GIVEN_neutral_trust_and_no_relation_nor_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = 0, Relation = 0, Affection = 0}).Should().BeFalse();
        }

        // Guards that active distrust doesn't somehow satisfy the floor through an unsigned/absolute-value
        // bug: strongly negative Trust must read exactly like "not trusted", not "extreme trust".
        [Test]
        public void GIVEN_deeply_distrustful_trust_and_no_relation_nor_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = -40, Relation = 0, Affection = 0}).Should().BeFalse();
        }

        // The OR-relation soupape (Gabriel ratified 2026-07-04): a long-cultivated ally lends even with
        // ZERO CR-tracked Trust, so this gate never silently narrows an already-shipped feature. An unproven
        // stranger must still earn Trust the hard way.
        [Test]
        public void GIVEN_zero_trust_but_an_established_high_relation_WHEN_checking_delegated_risk_THEN_the_established_ally_still_lends()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = 0, Relation = StanceGatePolicy.DelegatedRiskRelationBypass, Affection = 0}).Should().BeTrue();
        }

        // The boundary of that same soupape: one point under DelegatedRiskRelationBypass, with no Trust or
        // Affection to fall back on, must still refuse.
        [Test]
        public void GIVEN_zero_trust_and_a_relation_one_point_below_the_bypass_and_no_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = 0, Relation = StanceGatePolicy.DelegatedRiskRelationBypass - 1, Affection = 0}).Should().BeFalse();
        }

        // ── Affection (PERSONAL LOYALTY despite cold clan standing), increment 4, ratified 2026-07-05 ──

        // Affection-1, the THIRD door: personal loyalty cuts through a COLD clan standing even with zero
        // proven Trust, a lord who personally adores the player takes the risk on them regardless of
        // politics. Without this door, personal devotion would count for nothing here.
        [Test]
        public void GIVEN_no_trust_and_a_cold_clan_standing_but_high_personal_affection_WHEN_checking_delegated_risk_THEN_personal_loyalty_carries_the_favour()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = 0, Relation = -20, Affection = StanceGatePolicy.AffectionLoyaltyFloor}).Should().BeTrue();
        }

        // The boundary of the Affection door: one point under AffectionLoyaltyFloor, with no other basis,
        // must still refuse: the "activator" is not a low bar that leaks the gate open by default.
        [Test]
        public void GIVEN_affection_one_point_below_the_loyalty_floor_and_no_other_basis_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = 0, Relation = 0, Affection = StanceGatePolicy.AffectionLoyaltyFloor - 1}).Should().BeFalse();
        }

        // ── Respect (POLITICAL WEIGHT), increment 3 ──────────────────────────

        // Feeds general Respect sites (political weight) via CommandsRespect: reserved for future lower-
        // gated sites (sway_opinion weight, threat credibility) beyond join_clan's own higher bypass below.
        [Test]
        public void GIVEN_respect_above_the_floor_WHEN_checking_commands_respect_THEN_the_player_s_weight_carries()
        {
            StanceGatePolicy.CommandsRespect(StanceGatePolicy.RespectFloor + 1, 0).Should().BeTrue();
        }

        // RespectFloor is inclusive (>=): pins the exact boundary against a silent flip.
        [Test]
        public void GIVEN_respect_exactly_at_the_floor_WHEN_checking_commands_respect_THEN_it_still_counts_as_esteemed_enough()
        {
            StanceGatePolicy.CommandsRespect(StanceGatePolicy.RespectFloor, 0).Should().BeTrue();
        }

        // The far side of that boundary, with no relation to fall back on.
        [Test]
        public void GIVEN_respect_one_point_below_the_floor_and_no_established_relation_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(StanceGatePolicy.RespectFloor - 1, 0).Should().BeFalse();
        }

        // Default-deny baseline for the general Respect gate.
        [Test]
        public void GIVEN_neutral_respect_and_no_established_relation_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(0, 0).Should().BeFalse();
        }

        // Guards against an unsigned/absolute-value bug: open contempt must read as "no weight", never as
        // extreme respect.
        [Test]
        public void GIVEN_deeply_contemptuous_respect_and_no_established_relation_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(-40, 0).Should().BeFalse();
        }

        // The general Respect soupape, symmetric with DelegatedRiskRelationBypass: an established ally
        // carries the player's weight even before any CR-tracked Respect deed.
        [Test]
        public void GIVEN_zero_respect_but_an_established_high_relation_WHEN_checking_commands_respect_THEN_the_established_ally_still_carries_the_player_s_weight()
        {
            StanceGatePolicy.CommandsRespect(0, StanceGatePolicy.RespectRelationBypass).Should().BeTrue();
        }

        // The boundary of that general soupape.
        [Test]
        public void GIVEN_zero_respect_and_a_relation_one_point_below_the_bypass_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(0, StanceGatePolicy.RespectRelationBypass - 1).Should().BeFalse();
        }

        // ── Respect at join_clan (site-specific HIGH bypass, ratified 2026-07-05) ──

        // The join_clan-specific bypass: the naive general soupape (30) was DEAD here because
        // LordRecruitment.MinRelation (60) already exceeds it and always fired, so Respect never actually
        // bound. A merely-eligible lord (relation 60-79) who respects the player must still be free to join.
        [Test]
        public void GIVEN_a_merely_eligible_lord_with_respect_above_the_floor_WHEN_checking_join_clan_THEN_the_lord_will_join()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(StanceGatePolicy.RespectFloor, 60).Should().BeTrue();
        }

        // The bug this increment fixed, pinned directly: a merely-eligible lord (relation in the 60-79 band)
        // WITHOUT Respect must now actually refuse, where the dead general bypass previously let him through.
        [Test]
        public void GIVEN_a_merely_eligible_lord_who_does_not_respect_the_player_WHEN_checking_join_clan_THEN_the_lord_refuses_despite_the_recruitment_relation()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(0, 60).Should().BeFalse();
        }

        // The boundary just under RecruitRespectRelationBypass: confirms the higher bar, not the general
        // one, is what governs join_clan.
        [Test]
        public void GIVEN_a_lord_just_below_the_recruit_bypass_with_no_respect_WHEN_checking_join_clan_THEN_the_lord_still_refuses()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(0, StanceGatePolicy.RecruitRespectRelationBypass - 1).Should().BeFalse();
        }

        // At RecruitRespectRelationBypass itself, a truly beloved lord still joins on relation alone, no
        // Respect required: the higher bypass narrows WHO needs to earn Respect, it doesn't remove the
        // relation-alone path entirely.
        [Test]
        public void GIVEN_an_adored_lord_at_the_recruit_bypass_with_no_respect_WHEN_checking_join_clan_THEN_the_lord_joins_on_relation_alone()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(0, StanceGatePolicy.RecruitRespectRelationBypass).Should().BeTrue();
        }
    }
}
