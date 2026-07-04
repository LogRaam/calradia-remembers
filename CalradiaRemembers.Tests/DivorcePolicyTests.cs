// Code written by Gabriel Mailhot, 03/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorcePolicyTests
   {
      [Test]
      public void GIVEN_a_barely_unhappy_marriage_WHEN_asking_the_required_bond_THEN_the_full_trust_is_needed()
      {
         // Barely unhappy: only someone deeply trusted could talk them into so scandalous a step.
         DivorcePolicy.RequiredPlayerBond(DivorcePolicy.UnhappyThreshold).Should().Be(DivorcePolicy.BondThreshold);
      }

      [Test]
      public void GIVEN_a_default_culture_WHEN_scaling_the_scandal_magnitude_THEN_it_is_unchanged()
      {
         DivorcePolicy.ScaledScandalMagnitude("vlandia").Should().Be(DivorcePolicy.ScandalBaseMagnitude);
      }

      [Test]
      public void GIVEN_a_hero_still_in_their_origin_clan_WHEN_checking_for_a_clan_move_THEN_it_is_false()
      {
         DivorcePolicy.HasMovedClans("clan_a", "clan_a").Should().BeFalse();
      }

      // ── Clan-move detection ──────────────────────────────────────────────

      [Test]
      public void GIVEN_a_hero_whose_clan_differs_from_their_origin_WHEN_checking_for_a_clan_move_THEN_it_is_true()
      {
         DivorcePolicy.HasMovedClans("clan_a", "clan_b").Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_mildly_unhappy_stranger_WHEN_checking_eligibility_THEN_the_bond_gate_still_refuses()
      {
         var facts = new DivorceEligibilityFacts {
            NpcIsMarried = true,
            NpcRelationWithSpouse = DivorcePolicy.UnhappyThreshold,
            NpcRelationWithPlayer = 5
         };

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("bond");
      }

      [Test]
      public void GIVEN_a_missing_clan_id_WHEN_checking_for_a_clan_move_THEN_it_is_false()
      {
         DivorcePolicy.HasMovedClans(null, "clan_a").Should().BeFalse();
         DivorcePolicy.HasMovedClans("clan_a", null).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_null_culture_WHEN_pricing_the_scandal_THEN_it_uses_the_default_multiplier()
      {
         DivorcePolicy.CultureSeverityMultiplier(null).Should().Be(DivorcePolicy.DefaultScandalMultiplier);
      }

      [Test]
      public void GIVEN_a_plain_spouse_and_a_high_roll_WHEN_resisting_THEN_the_process_continues()
      {
         DivorcePolicy.RollResistance(0, 0, 0, 99).Should().Be(DivorceResistanceOutcome.Continue);
      }

      // ── Spousal resistance roll ──────────────────────────────────────────

      [Test]
      public void GIVEN_a_plain_spouse_and_a_low_roll_WHEN_resisting_THEN_the_process_aborts()
      {
         // Base abort chance is 3%: a roll of 0 always lands inside it.
         DivorcePolicy.RollResistance(0, 0, 0, 0).Should().Be(DivorceResistanceOutcome.Aborted);
      }

      [Test]
      public void GIVEN_a_plain_spouse_and_a_mid_roll_WHEN_resisting_THEN_the_process_is_delayed()
      {
         // Base abort 3% + base delay 10% = 13%: a roll of 5 clears the abort band and lands in delay.
         DivorcePolicy.RollResistance(0, 0, 0, 5).Should().Be(DivorceResistanceOutcome.Delayed);
      }

      [Test]
      public void GIVEN_a_process_already_active_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.ProcessAlreadyActive = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("already");
      }

      [Test]
      public void GIVEN_a_proud_calculating_spouse_WHEN_resisting_THEN_the_abort_chance_is_wider()
      {
         // Honor +2, Calculating +2: abort chance 3 + 2*2 + 2*2 = 11%. A roll of 10 aborts here but would
         // NOT abort a plain spouse (whose abort band is only 3%).
         DivorcePolicy.RollResistance(2, 2, 0, 10).Should().Be(DivorceResistanceOutcome.Aborted);
         DivorcePolicy.RollResistance(0, 0, 0, 10).Should().NotBe(DivorceResistanceOutcome.Aborted);
      }

      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_measuring_the_hits_THEN_the_full_penalties_apply()
      {
         DivorcePolicy.ExSpouseNpcHit(false).Should().Be(DivorcePolicy.ExSpouseNpcRelationHit);
         DivorcePolicy.ExSpousePlayerHit(false).Should().Be(DivorcePolicy.ExSpousePlayerRelationHit);
      }

      [Test]
      public void GIVEN_a_spouse_who_fought_the_estrangement_WHEN_reading_consent_THEN_they_were_repudiated()
      {
         DivorcePolicy.IsMutualConsent(3).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_spouse_who_never_resisted_WHEN_reading_consent_THEN_the_divorce_is_mutual()
      {
         // Engine relations are symmetric per pair, so acquiescence (no delay days) is the honest signal.
         DivorcePolicy.IsMutualConsent(0).Should().BeTrue();
      }

      // ── Deadline / delay math ────────────────────────────────────────────

      [Test]
      public void GIVEN_a_start_day_WHEN_computing_the_end_day_THEN_it_adds_the_process_duration()
      {
         DivorcePolicy.ComputeEndDay(100).Should().Be(100 + DivorcePolicy.ProcessDurationDays);
      }

      [Test]
      public void GIVEN_a_sturgian_culture_id_in_mixed_case_WHEN_pricing_the_scandal_THEN_it_still_matches()
      {
         DivorcePolicy.CultureSeverityMultiplier("Sturgia").Should().Be(DivorcePolicy.NordicScandalMultiplier);
      }

      // ── Culture-scaled scandal ───────────────────────────────────────────

      [Test]
      public void GIVEN_a_sturgian_culture_WHEN_pricing_the_scandal_THEN_it_is_halved()
      {
         DivorcePolicy.CultureSeverityMultiplier("sturgia").Should().Be(DivorcePolicy.NordicScandalMultiplier);
      }

      [Test]
      public void GIVEN_a_sturgian_culture_WHEN_scaling_the_scandal_magnitude_THEN_it_is_halved()
      {
         DivorcePolicy.ScaledScandalMagnitude("sturgia").Should().Be(DivorcePolicy.ScandalBaseMagnitude / 2);
      }

      [Test]
      public void GIVEN_a_wretched_marriage_WHEN_asking_the_required_bond_THEN_no_bond_is_needed_at_all()
      {
         // Deeply miserable: they seize the door once anyone opens it (Gabriel: a flat 30 was incoherent).
         DivorcePolicy.RequiredPlayerBond(DivorcePolicy.MiseryNeedingNoBond).Should().Be(0);
         DivorcePolicy.RequiredPlayerBond(-80).Should().Be(0);
      }

      [Test]
      public void GIVEN_an_end_day_WHEN_extending_it_for_a_delay_THEN_it_adds_the_delay_days()
      {
         DivorcePolicy.ExtendEndDay(115).Should().Be(115 + DivorcePolicy.DelayDaysPerDelay);
      }

      [Test]
      public void GIVEN_an_npc_courting_the_player_WHEN_checking_eligibility_THEN_the_bond_gate_is_waived()
      {
         // They leave the marriage FOR the player: love overrides the trust arithmetic entirely.
         var facts = new DivorceEligibilityFacts {
            NpcIsMarried = true,
            NpcRelationWithSpouse = DivorcePolicy.UnhappyThreshold,
            NpcRelationWithPlayer = 0,
            NpcIsRomanticallyBoundToPlayer = true
         };

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeTrue(reason);
      }

      [Test]
      public void GIVEN_an_unlisted_culture_WHEN_pricing_the_scandal_THEN_it_uses_the_default_multiplier()
      {
         DivorcePolicy.CultureSeverityMultiplier("vlandia").Should().Be(DivorcePolicy.DefaultScandalMultiplier);
      }

      [Test]
      public void GIVEN_either_party_is_a_prisoner_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.EitherPrisoner = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("captive");
      }

      [Test]
      public void GIVEN_either_party_is_pregnant_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.EitherPregnant = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("pregnan");
      }

      // ── Eligibility ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_every_gate_holds_WHEN_evaluating_eligibility_THEN_it_is_eligible()
      {
         DivorcePolicy.IsEligible(EligibleFacts(), out string reason).Should().BeTrue();
         reason.Should().BeNull();
      }

      [Test]
      public void GIVEN_misery_between_the_anchors_WHEN_asking_the_required_bond_THEN_it_slides_down_linearly()
      {
         int midMisery = (DivorcePolicy.UnhappyThreshold + DivorcePolicy.MiseryNeedingNoBond) / 2;
         int required = DivorcePolicy.RequiredPlayerBond(midMisery);
         required.Should().BeGreaterThan(0);
         required.Should().BeLessThan(DivorcePolicy.BondThreshold);
      }

      [Test]
      public void GIVEN_mutual_consent_WHEN_measuring_the_ex_spouses_hits_THEN_relief_stings_far_less_than_repudiation()
      {
         DivorcePolicy.ExSpouseNpcHit(true).Should().Be(DivorcePolicy.MutualExSpouseNpcRelationHit);
         DivorcePolicy.ExSpousePlayerHit(true).Should().Be(DivorcePolicy.MutualExSpousePlayerRelationHit);
         DivorcePolicy.ExSpouseNpcHit(true).Should().BeGreaterThan(DivorcePolicy.ExSpouseNpcHit(false));
         DivorcePolicy.ExSpousePlayerHit(true).Should().BeGreaterThan(DivorcePolicy.ExSpousePlayerHit(false));
      }

      [Test]
      public void GIVEN_negative_traits_WHEN_resisting_THEN_they_never_shrink_the_base_chance()
      {
         // A dishonorable, uncalculating spouse resists no LESS than the base chance.
         DivorcePolicy.RollResistance(-2, -2, 0, 2).Should().Be(DivorceResistanceOutcome.Aborted);
      }

      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorcePolicy.IsEligible(null, out string reason).Should().BeFalse();
         reason.Should().NotBeNullOrEmpty();
      }

      [Test]
      public void GIVEN_the_bond_with_the_player_falls_short_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.NpcRelationWithPlayer = DivorcePolicy.BondThreshold - 1;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("bond");
      }

      [Test]
      public void GIVEN_the_cumulative_delay_cap_is_reached_WHEN_resisting_THEN_an_abort_can_still_land()
      {
         // The cap only removes the Delayed option; Aborted stays available every day.
         DivorcePolicy.RollResistance(0, 0, DivorcePolicy.MaxCumulativeDelayDays, 0).Should().Be(DivorceResistanceOutcome.Aborted);
      }

      [Test]
      public void GIVEN_the_cumulative_delay_cap_is_reached_WHEN_resisting_THEN_no_further_delay_is_offered()
      {
         // At the cap, a roll that would normally land in the delay band instead falls through to Continue.
         DivorcePolicy.RollResistance(0, 0, DivorcePolicy.MaxCumulativeDelayDays, 5).Should().Be(DivorceResistanceOutcome.Continue);
      }

      [Test]
      public void GIVEN_the_leaving_spouse_governs_a_settlement_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.LeavingSpouseGoverns = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("governs");
      }

      [Test]
      public void GIVEN_the_leaving_spouse_heads_their_own_clan_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.LeavingSpouseIsClanLeader = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("heads their own clan");
      }

      [Test]
      public void GIVEN_the_leaving_spouse_leads_a_party_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.LeavingSpouseLeadsParty = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("leads a party");
      }

      [Test]
      public void GIVEN_the_marriage_is_not_unhappy_enough_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.NpcRelationWithSpouse = DivorcePolicy.UnhappyThreshold + 1;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("unhappy");
      }

      [Test]
      public void GIVEN_the_npc_is_married_to_the_player_WHEN_checking_eligibility_THEN_phase_one_refuses()
      {
         // The player's own marriage is out of scope: without this guard the romance waiver would let the
         // player's unhappy spouse be talked into end_marriage, dissolving the PLAYER's marriage through a
         // path never designed for it. The spouse DEMANDING a divorce from the player is its own feature.
         var facts = new DivorceEligibilityFacts {
            NpcIsMarried = true,
            SpouseIsPlayer = true,
            NpcRelationWithSpouse = -40,
            NpcIsRomanticallyBoundToPlayer = true
         };

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("YOU");
      }

      [Test]
      public void GIVEN_the_npc_is_not_married_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.NpcIsMarried = false;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("married");
      }

      #region private

      private static DivorceEligibilityFacts EligibleFacts()
         => new() {
            NpcIsMarried = true,
            NpcRelationWithSpouse = DivorcePolicy.UnhappyThreshold,
            NpcRelationWithPlayer = DivorcePolicy.BondThreshold,
            EitherPregnant = false,
            EitherPrisoner = false,
            LeavingSpouseIsClanLeader = false,
            LeavingSpouseLeadsParty = false,
            LeavingSpouseGoverns = false,
            ProcessAlreadyActive = false
         };

      #endregion
   }
}