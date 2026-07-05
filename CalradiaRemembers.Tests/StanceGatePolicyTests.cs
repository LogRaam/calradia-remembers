// Code written by Gabriel Mailhot, 04/07/2026.
// Stance mechanical behaviour-gating, increment 1: protects the Fear-coercion thresholds ratified
// 2026-07-04 — a terrified captive yields softer terms but plants a HIDDEN grudge instead of open
// regard, Honor/Valor stiffen resolve, Calculating breaks sooner and resents it more, and an unafraid,
// contemptuous captive refuses outright. Gabriel reads these as the tuning's documentation.

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

        [Test]
        public void GIVEN_a_terrified_captive_with_neutral_traits_WHEN_resolving_coercion_THEN_they_yield_softer_terms_with_a_hidden_grudge()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 40, respect: 0, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
            result.RansomMultiplier.Should().Be(StanceGatePolicy.RansomSoftenMultiplier);
            result.HiddenGrudgeMagnitude.Should().Be(StanceGatePolicy.CoercedGrudgeMagnitude);
        }

        [Test]
        public void GIVEN_exactly_the_yield_fear_floor_WHEN_resolving_coercion_THEN_it_still_counts_as_afraid_enough()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: StanceGatePolicy.YieldFearFloor, respect: 50, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
        }

        [Test]
        public void GIVEN_one_fear_point_below_the_floor_WHEN_resolving_coercion_THEN_they_do_not_yield()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: StanceGatePolicy.YieldFearFloor - 1, respect: 50, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().NotBe(CoercionOutcome.YieldsSofterTerms);
        }

        // ── Honor/Valor stiffen resolve ──────────────────────────────────────

        [Test]
        public void GIVEN_an_honourable_and_valorous_captive_WHEN_the_same_fear_no_longer_crosses_the_raised_floor_THEN_they_stay_neutral_not_yielding()
        {
            // The same fear (40) that broke a neutral-traited captive no longer suffices once
            // Honor/Valor have raised the effective floor.
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 40, respect: 50, honor: 2, valor: 2, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        [Test]
        public void GIVEN_an_honourable_and_valorous_captive_WHEN_fear_climbs_past_their_raised_floor_THEN_they_still_eventually_yield()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 100, respect: 50, honor: 2, valor: 2, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
        }

        // ── Calculating breaks sooner and resents it more ────────────────────

        [Test]
        public void GIVEN_a_calculating_captive_WHEN_resolving_coercion_THEN_they_yield_at_a_lower_fear_than_a_neutral_captive_would()
        {
            // 30 fear does not break a neutral captive (floor 40) but does break a calculating one
            // (floor lowered by 2 * CalculatingYieldPerLevel).
            CoercionResult neutralTraits = StanceGatePolicy.ResolveCoercion(
                fear: 30, respect: 50, honor: 0, valor: 0, calculating: 0);
            CoercionResult calculating = StanceGatePolicy.ResolveCoercion(
                fear: 30, respect: 50, honor: 0, valor: 0, calculating: 2);

            neutralTraits.Outcome.Should().Be(CoercionOutcome.Neutral);
            calculating.Outcome.Should().Be(CoercionOutcome.YieldsSofterTerms);
        }

        [Test]
        public void GIVEN_a_calculating_captive_WHEN_they_yield_THEN_the_hidden_grudge_carries_a_bigger_magnitude()
        {
            CoercionResult neutralTraits = StanceGatePolicy.ResolveCoercion(
                fear: 40, respect: 0, honor: 0, valor: 0, calculating: 0);
            CoercionResult calculating = StanceGatePolicy.ResolveCoercion(
                fear: 40, respect: 0, honor: 0, valor: 0, calculating: 2);

            calculating.HiddenGrudgeMagnitude.Should().BeGreaterThan(neutralTraits.HiddenGrudgeMagnitude);
            calculating.HiddenGrudgeMagnitude.Should().Be(
                StanceGatePolicy.CoercedGrudgeMagnitude + 2 * StanceGatePolicy.CalculatingGrudgeBonusPerLevel);
        }

        [Test]
        public void GIVEN_a_negative_calculating_trait_WHEN_resolving_coercion_THEN_it_does_not_raise_the_floor_the_wrong_way()
        {
            // Only POSITIVE Calculating eases the floor; a negative value must not stiffen it either.
            CoercionResult neutralTraits = StanceGatePolicy.ResolveCoercion(
                fear: 40, respect: 0, honor: 0, valor: 0, calculating: 0);
            CoercionResult negativeCalculating = StanceGatePolicy.ResolveCoercion(
                fear: 40, respect: 0, honor: 0, valor: 0, calculating: -2);

            negativeCalculating.Outcome.Should().Be(neutralTraits.Outcome);
            negativeCalculating.HiddenGrudgeMagnitude.Should().Be(neutralTraits.HiddenGrudgeMagnitude);
        }

        // ── Refuse flat ───────────────────────────────────────────────────────

        [Test]
        public void GIVEN_an_unafraid_and_contemptuous_captive_WHEN_resolving_coercion_THEN_they_refuse_flat()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 5, respect: 0, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.RefusesFlat);
            result.RansomMultiplier.Should().Be(1.0);
            result.HiddenGrudgeMagnitude.Should().Be(0);
        }

        [Test]
        public void GIVEN_fear_exactly_at_the_refuse_ceiling_WHEN_resolving_coercion_THEN_it_no_longer_qualifies_as_unafraid_enough_to_refuse()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: StanceGatePolicy.RefuseFearCeiling, respect: 0, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        [Test]
        public void GIVEN_respect_exactly_at_the_refuse_ceiling_WHEN_resolving_coercion_THEN_it_no_longer_qualifies_as_contemptuous_enough_to_refuse()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 5, respect: StanceGatePolicy.RefuseRespectCeiling, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        [Test]
        public void GIVEN_low_fear_but_meaningful_respect_WHEN_resolving_coercion_THEN_they_do_not_refuse_outright()
        {
            // Unafraid, but they still respect the player too much to flatly refuse.
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 5, respect: 20, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
        }

        // ── Neutral middle ground ─────────────────────────────────────────────

        [Test]
        public void GIVEN_a_mid_range_captive_WHEN_resolving_coercion_THEN_the_outcome_stays_neutral()
        {
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 25, respect: 30, honor: 0, valor: 0, calculating: 0);

            result.Outcome.Should().Be(CoercionOutcome.Neutral);
            result.RansomMultiplier.Should().Be(1.0);
            result.HiddenGrudgeMagnitude.Should().Be(0);
        }

        // ── Effective floor clamp ─────────────────────────────────────────────

        [Test]
        public void GIVEN_extreme_honor_and_valor_WHEN_resolving_coercion_THEN_the_effective_floor_never_exceeds_full_fear()
        {
            // Even a very high raised floor cannot be crossed by more than maximum Fear (100) — a
            // captive this resolute simply never yields to fear alone, but the policy must not
            // misbehave (throw, overflow) at the extreme.
            CoercionResult result = StanceGatePolicy.ResolveCoercion(
                fear: 100, respect: 0, honor: 2, valor: 2, calculating: 0);

            result.Outcome.Should().BeOneOf(CoercionOutcome.YieldsSofterTerms, CoercionOutcome.Neutral);
        }

        // ── Trust (DELEGATED RISK), increment 2 ──────────────────────────────

        [Test]
        public void GIVEN_trust_above_the_delegated_risk_floor_WHEN_checking_delegated_risk_THEN_the_npc_will_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(StanceGatePolicy.DelegatedRiskTrustFloor + 1, 0, 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_trust_exactly_at_the_delegated_risk_floor_WHEN_checking_delegated_risk_THEN_it_still_counts_as_trusted_enough()
        {
            StanceGatePolicy.WillDelegateRisk(StanceGatePolicy.DelegatedRiskTrustFloor, 0, 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_trust_one_point_below_the_floor_and_no_relation_nor_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(StanceGatePolicy.DelegatedRiskTrustFloor - 1, 0, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_neutral_trust_and_no_relation_nor_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(0, 0, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_deeply_distrustful_trust_and_no_relation_nor_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(-40, 0, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_zero_trust_but_an_established_high_relation_WHEN_checking_delegated_risk_THEN_the_established_ally_still_lends()
        {
            StanceGatePolicy.WillDelegateRisk(0, StanceGatePolicy.DelegatedRiskRelationBypass, 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_zero_trust_and_a_relation_one_point_below_the_bypass_and_no_loyalty_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(0, StanceGatePolicy.DelegatedRiskRelationBypass - 1, 0).Should().BeFalse();
        }

        // ── Affection (PERSONAL LOYALTY despite cold clan standing), increment 4, ratified 2026-07-05 ──

        [Test]
        public void GIVEN_no_trust_and_a_cold_clan_standing_but_high_personal_affection_WHEN_checking_delegated_risk_THEN_personal_loyalty_carries_the_favour()
        {
            StanceGatePolicy.WillDelegateRisk(0, -20, StanceGatePolicy.AffectionLoyaltyFloor).Should().BeTrue();
        }

        [Test]
        public void GIVEN_affection_one_point_below_the_loyalty_floor_and_no_other_basis_WHEN_checking_delegated_risk_THEN_the_npc_will_not_delegate_risk()
        {
            StanceGatePolicy.WillDelegateRisk(0, 0, StanceGatePolicy.AffectionLoyaltyFloor - 1).Should().BeFalse();
        }

        // ── Respect (POLITICAL WEIGHT), increment 3 ──────────────────────────

        [Test]
        public void GIVEN_respect_above_the_floor_WHEN_checking_commands_respect_THEN_the_player_s_weight_carries()
        {
            StanceGatePolicy.CommandsRespect(StanceGatePolicy.RespectFloor + 1, 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_respect_exactly_at_the_floor_WHEN_checking_commands_respect_THEN_it_still_counts_as_esteemed_enough()
        {
            StanceGatePolicy.CommandsRespect(StanceGatePolicy.RespectFloor, 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_respect_one_point_below_the_floor_and_no_established_relation_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(StanceGatePolicy.RespectFloor - 1, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_neutral_respect_and_no_established_relation_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(0, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_deeply_contemptuous_respect_and_no_established_relation_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(-40, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_zero_respect_but_an_established_high_relation_WHEN_checking_commands_respect_THEN_the_established_ally_still_carries_the_player_s_weight()
        {
            StanceGatePolicy.CommandsRespect(0, StanceGatePolicy.RespectRelationBypass).Should().BeTrue();
        }

        [Test]
        public void GIVEN_zero_respect_and_a_relation_one_point_below_the_bypass_WHEN_checking_commands_respect_THEN_the_player_s_weight_does_not_carry()
        {
            StanceGatePolicy.CommandsRespect(0, StanceGatePolicy.RespectRelationBypass - 1).Should().BeFalse();
        }

        // ── Respect at join_clan (site-specific HIGH bypass, ratified 2026-07-05) ──

        [Test]
        public void GIVEN_a_merely_eligible_lord_with_respect_above_the_floor_WHEN_checking_join_clan_THEN_the_lord_will_join()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(StanceGatePolicy.RespectFloor, 60).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_merely_eligible_lord_who_does_not_respect_the_player_WHEN_checking_join_clan_THEN_the_lord_refuses_despite_the_recruitment_relation()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(0, 60).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_lord_just_below_the_recruit_bypass_with_no_respect_WHEN_checking_join_clan_THEN_the_lord_still_refuses()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(0, StanceGatePolicy.RecruitRespectRelationBypass - 1).Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_adored_lord_at_the_recruit_bypass_with_no_respect_WHEN_checking_join_clan_THEN_the_lord_joins_on_relation_alone()
        {
            StanceGatePolicy.CommandsRespectToJoinClan(0, StanceGatePolicy.RecruitRespectRelationBypass).Should().BeTrue();
        }
    }
}
