// Code written by Gabriel Mailhot, 03/07/2026.
// Divorce, Phase 1: an NPC can be PERSUADED to end their OWN marriage to another NPC. This file is the whole
// rulebook: eligibility gates, the sliding trust requirement, the daily spousal-resistance roll, process
// timers, culture-scaled scandal, and the signed consequence magnitudes (also reused by the player's own
// Phase 2a/2b divorce).
//
// WHY IT MATTERS: born from a real empty-promise bug, a player "convinced" an NPC to leave their spouse and
// nothing happened. The fix, ratified by Gabriel 2026-07-03, treats marriage as an ALLIANCE: ending one is a
// multi-day estrangement both families fight, never "talk once and it is done". Get a gate wrong and a happy
// couple can be talked apart for free, or a wretched one stays gated behind trust the redesign was meant to
// waive. Get the resistance or mutual-consent math wrong and the wrong side eats the grudge storm.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorcePolicyTests
   {
      // Pins the top anchor of the sliding bond requirement: right at UnhappyThreshold (the same bar that makes
      // the marriage eligible at all), the NPC needs the player's FULL trust, per Gabriel's 2026-07-03 call that
      // a flat 30 was incoherent (an NPC divorces for themselves first, not merely because a friend asked).
      [Test]
      public void GIVEN_a_barely_unhappy_marriage_WHEN_asking_the_required_bond_THEN_the_full_trust_is_needed()
      {
         // Barely unhappy: only someone deeply trusted could talk them into so scandalous a step.
         DivorcePolicy.RequiredPlayerBond(new VanillaRelation(DivorcePolicy.UnhappyThreshold)).Should().Be(DivorcePolicy.BondThreshold);
      }

      // Confirms the Nordic discount is an allowlist, not a default: a culture absent from NordicCultureIds
      // (here Vlandia) must scandal at full strength, or the culture-scaled world event would silently
      // underweight consequences for every culture nobody thought to list.
      [Test]
      public void GIVEN_a_default_culture_WHEN_scaling_the_scandal_magnitude_THEN_it_is_unchanged()
      {
         DivorcePolicy.ScaledScandalMagnitude("vlandia").Should().Be(DivorcePolicy.ScandalBaseMagnitude);
      }

      // Baseline for identifying the "leaving spouse", the one who must relocate clans to formalize the split
      // and so gets checked against the clan-leader/party/settlement refusal gates. A hero who never left their
      // origin clan must never be flagged as needing to relocate.
      [Test]
      public void GIVEN_a_hero_still_in_their_origin_clan_WHEN_checking_for_a_clan_move_THEN_it_is_false()
      {
         DivorcePolicy.HasMovedClans("clan_a", "clan_a").Should().BeFalse();
      }

      // ── Clan-move detection ──────────────────────────────────────────────

      // The flip side of the baseline above: this is what actually SELECTS which spouse is "leaving" and so
      // gets checked for clan leadership, party command, or settlement governance before Phase 1 will proceed.
      [Test]
      public void GIVEN_a_hero_whose_clan_differs_from_their_origin_WHEN_checking_for_a_clan_move_THEN_it_is_true()
      {
         DivorcePolicy.HasMovedClans("clan_a", "clan_b").Should().BeTrue();
      }

      // A barely-unhappy marriage still needs the full BondThreshold: a near-stranger (relation 5) asking an
      // NPC to blow up their marriage must be refused, or the trust gate would mean nothing for the exact case
      // it matters most.
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

      // Fail-safe for the v1.21.1 hotfix: on some game builds/older saves the origin-clan property is
      // unavailable, and reading it used to crash conversations outright ("method not found"). Null args here
      // model that exact missing-property case; HasMovedClans must degrade to "no move" rather than throw, so
      // divorce keeps working via the clan-leader heuristic fallback instead of taking the whole chat down.
      [Test]
      public void GIVEN_a_missing_clan_id_WHEN_checking_for_a_clan_move_THEN_it_is_false()
      {
         DivorcePolicy.HasMovedClans(null, "clan_a").Should().BeFalse();
         DivorcePolicy.HasMovedClans("clan_a", null).Should().BeFalse();
      }

      // Fail-safe: an unset or unrecognized culture id must price the scandal at the default multiplier, never
      // crash or silently apply the Nordic discount it was never shown evidence for.
      [Test]
      public void GIVEN_a_null_culture_WHEN_pricing_the_scandal_THEN_it_uses_the_default_multiplier()
      {
         DivorcePolicy.CultureSeverityMultiplier(null).Should().Be(DivorcePolicy.DefaultScandalMultiplier);
      }

      // The default day: most rolls should land here (base abort 3% + base delay 10% leaves 87% Continue). This
      // is the "nothing happened today" outcome that makes the estrangement a real multi-day PROCESS (never
      // "talk once and it is done") rather than every day being a coin-flip toward Delayed or Aborted.
      [Test]
      public void GIVEN_a_plain_spouse_and_a_high_roll_WHEN_resisting_THEN_the_process_continues()
      {
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 0, SpouseCalculating = 0, CumulativeDelayDays = 0, Roll0To99 = 99}).Should().Be(DivorceResistanceOutcome.Continue);
      }

      // ── Spousal resistance roll ──────────────────────────────────────────

      // Realizes "both families oppose it fiercely": even a plain spouse's resistance can kill the whole
      // process outright on any given day, not just delay it. Without a real Abort path, the multi-day
      // estrangement would only ever slow down, never actually fail.
      [Test]
      public void GIVEN_a_plain_spouse_and_a_low_roll_WHEN_resisting_THEN_the_process_aborts()
      {
         // Base abort chance is 3%: a roll of 0 always lands inside it.
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 0, SpouseCalculating = 0, CumulativeDelayDays = 0, Roll0To99 = 0}).Should().Be(DivorceResistanceOutcome.Aborted);
      }

      // The middle outcome band: resistance that neither kills the process nor lets it pass unchallenged,
      // pushing the end day back instead. Pins that Delayed sits strictly between Aborted and Continue in the
      // roll order.
      [Test]
      public void GIVEN_a_plain_spouse_and_a_mid_roll_WHEN_resisting_THEN_the_process_is_delayed()
      {
         // Base abort 3% + base delay 10% = 13%: a roll of 5 clears the abort band and lands in delay.
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 0, SpouseCalculating = 0, CumulativeDelayDays = 0, Roll0To99 = 5}).Should().Be(DivorceResistanceOutcome.Delayed);
      }

      // One estrangement per NPC at a time: without this gate, re-triggering end_marriage mid-process could
      // start a second overlapping process (duplicate end day, doubled consequences on completion) instead of
      // resuming the one already under way.
      [Test]
      public void GIVEN_a_process_already_active_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.ProcessAlreadyActive = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("already");
      }

      // Honor and Calculating are the traits the design names explicitly ("a daily roll... can Delay... or
      // Abort"): a proud, calculating spouse must resist harder than a plain one, or the trait system would
      // have no bearing on the one mechanic built specifically to let personality matter here.
      [Test]
      public void GIVEN_a_proud_calculating_spouse_WHEN_resisting_THEN_the_abort_chance_is_wider()
      {
         // Honor +2, Calculating +2: abort chance 3 + 2*2 + 2*2 = 11%. A roll of 10 aborts here but would
         // NOT abort a plain spouse (whose abort band is only 3%).
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 2, SpouseCalculating = 2, CumulativeDelayDays = 0, Roll0To99 = 10}).Should().Be(DivorceResistanceOutcome.Aborted);
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 0, SpouseCalculating = 0, CumulativeDelayDays = 0, Roll0To99 = 10}).Should().NotBe(DivorceResistanceOutcome.Aborted);
      }

      // The harsh tier: "a spouse set aside against their will turns on you and the one who left them" (shipped
      // design). Pins that repudiation reads the un-softened constants, since ExSpouseNpcHit/PlayerHit branch
      // on this same boolean the mutual-consent case below shares.
      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_measuring_the_hits_THEN_the_full_penalties_apply()
      {
         DivorcePolicy.ExSpouseNpcHit(false).Should().Be(DivorcePolicy.ExSpouseNpcRelationHit);
         DivorcePolicy.ExSpousePlayerHit(false).Should().Be(DivorcePolicy.ExSpousePlayerRelationHit);
      }

      // Engine relations are SYMMETRIC per pair, so relation alone cannot tell us the spouse was unhappy too;
      // the one honest signal available is whether they ever resisted. Any resistance at all, however brief,
      // reads as repudiation, not willingness, so the full grudge-storm consequences apply.
      [Test]
      public void GIVEN_a_spouse_who_fought_the_estrangement_WHEN_reading_consent_THEN_they_were_repudiated()
      {
         DivorcePolicy.IsMutualConsent(3, spouseRegardForPlayer: 20).Should().BeFalse();
      }

      // The other half of that same signal: zero accumulated delay days from a spouse who does not resent the
      // player is read as acquiescence. Get this backwards and a spouse who quietly let the marriage end would be
      // repudiated (full grudge storm) for a split they never fought.
      [Test]
      public void GIVEN_a_spouse_who_never_resisted_and_bears_no_grudge_WHEN_reading_consent_THEN_the_divorce_is_mutual()
      {
         DivorcePolicy.IsMutualConsent(0, spouseRegardForPlayer: 20).Should().BeTrue();
      }

      // Romance audit minor: resistance is a probabilistic roll, so a genuinely HATEFUL spouse can happen to roll
      // zero delays. Without the regard gate they would earn the amicable, low-grudge "mutual consent" outcome
      // they never deserved (~21% of hateful spouses). A spouse who resents the player is never a mutual split.
      [Test]
      public void GIVEN_a_hateful_spouse_who_happened_not_to_resist_WHEN_reading_consent_THEN_it_is_not_mutual()
      {
         DivorcePolicy.IsMutualConsent(0, spouseRegardForPlayer: -1).Should().BeFalse();
      }

      // ── Deadline / delay math ────────────────────────────────────────────

      // Pins that the estrangement's length is exactly ProcessDurationDays (15) after the start day: the whole
      // point of a multi-day process, instead of an instant flip, depends on this arithmetic landing exactly
      // where tuned.
      [Test]
      public void GIVEN_a_start_day_WHEN_computing_the_end_day_THEN_it_adds_the_process_duration()
      {
         DivorcePolicy.ComputeEndDay(100).Should().Be(100 + DivorcePolicy.ProcessDurationDays);
      }

      // NordicCultureIds is declared case-insensitive on purpose: culture StringIds reaching this method are
      // not guaranteed to already be lowercase, so a differently-cased "Sturgia" must still get the Nordic
      // discount instead of silently falling through to the default multiplier.
      [Test]
      public void GIVEN_a_sturgian_culture_id_in_mixed_case_WHEN_pricing_the_scandal_THEN_it_still_matches()
      {
         DivorcePolicy.CultureSeverityMultiplier("Sturgia").Should().Be(DivorcePolicy.NordicScandalMultiplier);
      }

      // ── Culture-scaled scandal ───────────────────────────────────────────

      // The Nordic/Sturgian discount itself: these cultures "treat dissolution more freely" by design, so the
      // same divorce must scandal at half severity here versus a default culture.
      [Test]
      public void GIVEN_a_sturgian_culture_WHEN_pricing_the_scandal_THEN_it_is_halved()
      {
         DivorcePolicy.CultureSeverityMultiplier("sturgia").Should().Be(DivorcePolicy.NordicScandalMultiplier);
      }

      // The discount has to actually reach the world-event magnitude, not just the multiplier in isolation:
      // this is what the recorded scandal event's magnitude actually sees.
      [Test]
      public void GIVEN_a_sturgian_culture_WHEN_scaling_the_scandal_magnitude_THEN_it_is_halved()
      {
         DivorcePolicy.ScaledScandalMagnitude("sturgia").Should().Be(DivorcePolicy.ScandalBaseMagnitude / 2);
      }

      // The bottom anchor of the same redesign as the barely-unhappy test above: at or below
      // MiseryNeedingNoBond the requirement bottoms out at zero. Without this floor a wretched marriage would
      // still gate on trust the NPC no longer needs, contradicting the whole reason the flat threshold was
      // replaced.
      [Test]
      public void GIVEN_a_wretched_marriage_WHEN_asking_the_required_bond_THEN_no_bond_is_needed_at_all()
      {
         // Deeply miserable: they seize the door once anyone opens it (Gabriel: a flat 30 was incoherent).
         DivorcePolicy.RequiredPlayerBond(new VanillaRelation(DivorcePolicy.MiseryNeedingNoBond)).Should().Be(0);
         DivorcePolicy.RequiredPlayerBond(new VanillaRelation(-80)).Should().Be(0);
      }

      // Pins the other half of the resistance math: a Delayed roll must push the end day back by exactly
      // DelayDaysPerDelay, or the "push it back, capped" resistance design would drift from its tuned constant.
      [Test]
      public void GIVEN_an_end_day_WHEN_extending_it_for_a_delay_THEN_it_adds_the_delay_days()
      {
         DivorcePolicy.ExtendEndDay(115).Should().Be(115 + DivorcePolicy.DelayDaysPerDelay);
      }

      // The romance waiver: an NPC leaving FOR the player needs no persuading, so the trust gate must not block
      // them even at zero relation. This same waiver is exactly what the SpouseIsPlayer guard elsewhere in this
      // file exists to keep from misfiring onto the player's own marriage.
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

      // Confirms NordicCultureIds is an allowlist covering only the listed cultures: a real, named, non-Nordic
      // culture (Vlandia) must price at the default, not fall through to some other unintended multiplier.
      [Test]
      public void GIVEN_an_unlisted_culture_WHEN_pricing_the_scandal_THEN_it_uses_the_default_multiplier()
      {
         DivorcePolicy.CultureSeverityMultiplier("vlandia").Should().Be(DivorcePolicy.DefaultScandalMultiplier);
      }

      // One of Phase 1's explicit gates: captivity refuses the whole process outright rather than trying to
      // reconcile a scandalous multi-day estrangement with a party who cannot even act freely.
      [Test]
      public void GIVEN_either_party_is_a_prisoner_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.EitherPrisoner = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("captive");
      }

      // The other explicit gate alongside captivity: a pregnancy in play refuses the process, keeping the
      // marriage and birth systems from ever having to reconcile a dissolved marriage against an in-flight
      // pregnancy.
      [Test]
      public void GIVEN_either_party_is_pregnant_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.EitherPregnant = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("pregnan");
      }

      // ── Eligibility ──────────────────────────────────────────────────────

      // The positive control: every negative-gate test below flips exactly ONE flag against this same eligible
      // baseline. Without this test passing, a gate accidentally too strict against the OTHER fields' defaults
      // could hide behind tests that only ever check its own flag.
      [Test]
      public void GIVEN_every_gate_holds_WHEN_evaluating_eligibility_THEN_it_is_eligible()
      {
         DivorcePolicy.IsEligible(EligibleFacts(), out string reason).Should().BeTrue();
         reason.Should().BeNull();
      }

      // The actual redesign this whole method exists for (Gabriel, 2026-07-03: a flat 30 was incoherent):
      // between the two anchors the requirement must fall strictly between 0 and BondThreshold, not jump
      // between them, or the "slides with misery" framing would be nominal only.
      [Test]
      public void GIVEN_misery_between_the_anchors_WHEN_asking_the_required_bond_THEN_it_slides_down_linearly()
      {
         int midMisery = (DivorcePolicy.UnhappyThreshold + DivorcePolicy.MiseryNeedingNoBond) / 2;
         int required = DivorcePolicy.RequiredPlayerBond(new VanillaRelation(midMisery));
         required.Should().BeGreaterThan(0);
         required.Should().BeLessThan(DivorcePolicy.BondThreshold);
      }

      // Matches the shipped design almost verbatim: "a spouse set aside against their will turns on you...
      // while a divorce both sides wanted stings far less." Pins that mutual consent is not merely a different
      // outcome but a STRICTLY softer one on both axes, toward the NPC and toward the player.
      [Test]
      public void GIVEN_mutual_consent_WHEN_measuring_the_ex_spouses_hits_THEN_relief_stings_far_less_than_repudiation()
      {
         DivorcePolicy.ExSpouseNpcHit(true).Should().Be(DivorcePolicy.MutualExSpouseNpcRelationHit);
         DivorcePolicy.ExSpousePlayerHit(true).Should().Be(DivorcePolicy.MutualExSpousePlayerRelationHit);
         DivorcePolicy.ExSpouseNpcHit(true).Should().BeGreaterThan(DivorcePolicy.ExSpouseNpcHit(false));
         DivorcePolicy.ExSpousePlayerHit(true).Should().BeGreaterThan(DivorcePolicy.ExSpousePlayerHit(false));
      }

      // PositivePart floors negative trait levels at zero: a dishonorable, uncalculating spouse must still
      // resist at the same baseline as anyone else, not less. Without the floor, a player could pick a
      // low-Honor spouse to make the abort roll easier to clear than the tuned base rate.
      [Test]
      public void GIVEN_negative_traits_WHEN_resisting_THEN_they_never_shrink_the_base_chance()
      {
         // A dishonorable, uncalculating spouse resists no LESS than the base chance.
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = -2, SpouseCalculating = -2, CumulativeDelayDays = 0, Roll0To99 = 2}).Should().Be(DivorceResistanceOutcome.Aborted);
      }

      // Fail SAFE like the sibling PlayerDivorceConsentPolicy: missing facts must never be read as an eligible
      // divorce. A bad lookup silently granting eligibility is the kind of bug nobody notices until the
      // marriage is already gone.
      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorcePolicy.IsEligible(null, out string reason).Should().BeFalse();
         reason.Should().NotBeNullOrEmpty();
      }

      // Direct floor check: one point under BondThreshold must still refuse, or the trust gate would be
      // advisory rather than a hard boundary.
      [Test]
      public void GIVEN_the_bond_with_the_player_falls_short_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.NpcRelationWithPlayer = DivorcePolicy.BondThreshold - 1;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("bond");
      }

      // The cap (MaxCumulativeDelayDays, so "a process cannot stall forever") only removes the Delayed option;
      // it must never also make the process un-abortable, or a spouse who had stalled it to the cap would
      // become immune to failing outright for the rest of the process.
      [Test]
      public void GIVEN_the_cumulative_delay_cap_is_reached_WHEN_resisting_THEN_an_abort_can_still_land()
      {
         // The cap only removes the Delayed option; Aborted stays available every day.
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 0, SpouseCalculating = 0, CumulativeDelayDays = DivorcePolicy.MaxCumulativeDelayDays, Roll0To99 = 0}).Should().Be(DivorceResistanceOutcome.Aborted);
      }

      // The cap's actual effect: once MaxCumulativeDelayDays is reached, a roll that would normally land in the
      // delay band must fall through to Continue instead, or the "cannot stall forever" cap would be
      // decorative.
      [Test]
      public void GIVEN_the_cumulative_delay_cap_is_reached_WHEN_resisting_THEN_no_further_delay_is_offered()
      {
         // At the cap, a roll that would normally land in the delay band instead falls through to Continue.
         DivorcePolicy.RollResistance(new DivorcePolicy.ResistanceRollFacts {SpouseHonor = 0, SpouseCalculating = 0, CumulativeDelayDays = DivorcePolicy.MaxCumulativeDelayDays, Roll0To99 = 5}).Should().Be(DivorceResistanceOutcome.Continue);
      }

      // Phase 1 REFUSES rather than reassigning these roles (design decision, not an oversight): a governing
      // spouse cannot simply be relocated by end_marriage, so the whole process must refuse instead of leaving
      // a settlement without its governor.
      [Test]
      public void GIVEN_the_leaving_spouse_governs_a_settlement_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.LeavingSpouseGoverns = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("governs");
      }

      // Same refuse-not-reassign design as the settlement gate: a clan LEADER cannot be the one who relocates
      // into a spouse's clan, so this gate has to block the process rather than quietly break clan leadership.
      [Test]
      public void GIVEN_the_leaving_spouse_heads_their_own_clan_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.LeavingSpouseIsClanLeader = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("heads their own clan");
      }

      // Same family of gate: a spouse actively leading a party (or army) cannot be yanked into a clan move
      // mid-campaign, so Phase 1 refuses rather than risk leaving an army leaderless.
      [Test]
      public void GIVEN_the_leaving_spouse_leads_a_party_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.LeavingSpouseLeadsParty = true;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("leads a party");
      }

      // The core premise of the whole feature: only a genuinely unhappy marriage (relation at or below
      // UnhappyThreshold) is even a candidate. Without this gate, a player could talk any content couple into
      // divorcing, exactly the empty-promise-turned-real-mechanic problem this feature was built to fix,
      // just aimed at the wrong marriages.
      [Test]
      public void GIVEN_the_marriage_is_not_unhappy_enough_WHEN_evaluating_eligibility_THEN_it_refuses()
      {
         DivorceEligibilityFacts facts = EligibleFacts();
         facts.NpcRelationWithSpouse = DivorcePolicy.UnhappyThreshold + 1;

         DivorcePolicy.IsEligible(facts, out string reason).Should().BeFalse();
         reason.Should().Contain("unhappy");
      }

      // See the in-body comment below: this guard exists specifically because the romance waiver (above) would
      // otherwise let a player's own unhappy spouse be talked into end_marriage, a path never designed for the
      // player's own marriage (that is Phase 2's feature).
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

      // The most basic gate: there is nothing to end_marriage against if the NPC has no living spouse at all.
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