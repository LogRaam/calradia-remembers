// Code written by Gabriel Mailhot, 02/07/2026.
// This is the "hollow promise" fix (CHANGELOG v1.20.5): before this pillar a lord could agree in chat to
// lend troops or influence and nothing would actually happen. CourtActionPolicy is the pure math the bridge
// re-validates at execution time ("the prompt is advice, the bridge is law") for every court verb
// (give_influence, lend_troops, sway_opinion, pledge_against, release_prisoner, plus the give_troops/
// spend_influence reciprocals). A wrong number here either breaks a promise the LLM just made to the player,
// lets an NPC give away resources (their own troops, their clan's influence) no real lord would part with,
// or lets a verb reach a target it must never touch (an NPC's own close kin, or a faction leader whose fate
// is reserved for the leader-only war-decision system, never a conversation).

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CourtActionPolicyTests
   {
      // ── give_influence ────────────────────────────────────────────────────

      // Without this floor a lord with no bond, or one who is actively hostile, would still lend the
      // player political influence, contradicting "granted only when it makes sense for that lord and
      // their character" (CHANGELOG v1.20.5).
      [Test]
      public void GIVEN_zero_or_negative_relation_WHEN_computing_the_influence_grant_THEN_it_is_zero()
      {
         CourtActionPolicy.InfluenceGrant(new VanillaRelation(0)).Should().Be(0);
         CourtActionPolicy.InfluenceGrant(new VanillaRelation(-10)).Should().Be(0);
      }

      // Pins the exact formula (base + relation/divisor) so the number the LLM is taught to promise in
      // ExtraActionTeachings can never silently drift from the number the bridge actually grants.
      [Test]
      public void GIVEN_a_modest_relation_WHEN_computing_the_influence_grant_THEN_it_scales_with_relation()
      {
         //Arrange: base 5 + relation/10.
         CourtActionPolicy.InfluenceGrant(new VanillaRelation(10)).Should().Be(6);
         CourtActionPolicy.InfluenceGrant(new VanillaRelation(30)).Should().Be(8);
      }

      // Without the hard cap, an extremely deep bond could funnel unbounded influence to the player each
      // time the cooldown clears, breaking the deliberate scarcity of the political-influence economy.
      [Test]
      public void GIVEN_a_very_deep_relation_WHEN_computing_the_influence_grant_THEN_it_is_capped()
      {
         CourtActionPolicy.InfluenceGrant(new VanillaRelation(200)).Should().Be(CourtActionPolicy.InfluenceMaxGrant);
      }

      // Pins the ordinary "yes" case as a control for the buffer-multiplier boundary tests below.
      [Test]
      public void GIVEN_a_clan_with_far_more_influence_than_the_grant_WHEN_checking_affordability_THEN_it_can_afford_it()
      {
         CourtActionPolicy.CanAffordInfluenceGrant(100f, 10).Should().BeTrue();
      }

      // Guards "a lord does not beggar their own house to do the player a favour" (XML doc on
      // CanAffordInfluenceGrant): without the buffer requirement a lord could hand over influence down to
      // a sliver, leaving their own clan powerless at court.
      [Test]
      public void GIVEN_a_clan_with_barely_more_influence_than_the_grant_WHEN_checking_affordability_THEN_it_cannot_afford_it()
      {
         //Arrange: the buffer multiplier requires several times the grant in reserve.
         CourtActionPolicy.CanAffordInfluenceGrant(12f, 10).Should().BeFalse();
      }

      // Pins the inclusive (>=) edge of the affordability boundary so a later refactor can't silently
      // flip it to exclusive and start blocking grants that should succeed.
      [Test]
      public void GIVEN_exactly_the_buffer_multiple_WHEN_checking_affordability_THEN_the_threshold_is_inclusive()
      {
         CourtActionPolicy.CanAffordInfluenceGrant(10 * CourtActionPolicy.InfluenceBufferMultiplier, 10).Should().BeTrue();
      }

      // ── lend_troops ───────────────────────────────────────────────────────

      // Mirrors give_influence's floor: without it an NPC with no bond, or open hostility, would still
      // hand their own soldiers to the player, the same implausible-promise failure this pillar exists
      // to prevent.
      [Test]
      public void GIVEN_zero_or_negative_relation_WHEN_computing_the_troop_loan_THEN_it_is_zero()
      {
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 0, NpcTotalRegularTroops = 50, PlayerRoom = 20}).Should().Be(0);
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = -5, NpcTotalRegularTroops = 50, PlayerRoom = 20}).Should().Be(0);
      }

      // Pins the desired-count formula (relation / divisor) in the unconstrained case, isolating it from
      // the retain-floor and player-room caps exercised by the tests below.
      [Test]
      public void GIVEN_a_generous_relation_and_ample_room_WHEN_computing_the_troop_loan_THEN_it_scales_with_relation()
      {
         //Arrange: relation 25 / 5 = 5 desired, well within the party's spare and the player's room.
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 25, NpcTotalRegularTroops = 200, PlayerRoom = 50}).Should().Be(5);
      }

      // Hard cap keeps even the deepest bond from lending away a whole army in one exchange; without it
      // a single favour could strip an ally's fighting force wholesale.
      [Test]
      public void GIVEN_an_extremely_deep_relation_WHEN_computing_the_troop_loan_THEN_it_is_capped()
      {
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 1000, NpcTotalRegularTroops = 1000, PlayerRoom = 1000}).Should().Be(CourtActionPolicy.TroopLoanMaxCount);
      }

      // This is the self-preservation floor: without it a lord could be talked into gutting their own
      // party defenceless just to satisfy the player's request, something no real party leader would do.
      [Test]
      public void GIVEN_a_small_party_WHEN_computing_the_troop_loan_THEN_the_npc_never_drops_below_60_percent_strength()
      {
         //Arrange: 10 troops, 60% retained = 6, so at most 4 are spare regardless of relation.
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 1000, NpcTotalRegularTroops = 10, PlayerRoom = 50}).Should().Be(4);
      }

      // Pins the player-room cap: without it the count could exceed what the player's party can legally
      // hold, a number the LLM would have just promised in chat with no way for the bridge to honour it.
      [Test]
      public void GIVEN_the_player_party_nearly_full_WHEN_computing_the_troop_loan_THEN_it_never_exceeds_the_players_room()
      {
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 1000, NpcTotalRegularTroops = 1000, PlayerRoom = 2}).Should().Be(2);
      }

      // Exercises the retain-floor's rounding (ceiling) on a small roster: pins that a party already at
      // or below the 60% floor lends nothing at all, never a negative or silently-clamped count.
      [Test]
      public void GIVEN_no_spare_troops_WHEN_computing_the_troop_loan_THEN_it_is_zero()
      {
         //Arrange: the party is already below the 60% retain floor once rounded (5 troops, must keep 3).
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 50, NpcTotalRegularTroops = 5, PlayerRoom = 10}).Should().Be(2);
         CourtActionPolicy.TroopLoanCount(new CourtActionPolicy.TroopLoanFacts {Relation = 50, NpcTotalRegularTroops = 1, PlayerRoom = 10}).Should().Be(0);
      }

      // ── give_troops (reciprocal of lend_troops) ─────────────────────────

      // Baseline "yes" case for the weakened check that gates whether give_troops is ever offered to
      // the player for this NPC at all.
      [Test]
      public void GIVEN_a_party_below_the_weakened_fraction_of_a_large_enough_size_limit_WHEN_checking_if_weakened_THEN_it_is()
      {
         //Arrange: size limit 40, healthy 15 (under 50%).
         CourtActionPolicy.IsPartyWeakened(15, 40).Should().BeTrue();
      }

      // Without this, a party that is merely at normal strength could still read as "weakened" and
      // prompt the player to reinforce an NPC who does not need it.
      [Test]
      public void GIVEN_a_party_at_or_above_the_weakened_fraction_WHEN_checking_if_weakened_THEN_it_is_not()
      {
         CourtActionPolicy.IsPartyWeakened(25, 40).Should().BeFalse();
      }

      // Without GiveTroopsMinPartySizeLimit, a small elite escort (fielded deliberately under-strength)
      // would misread as "weakened" and prompt the player to reinforce a party that was never meant to
      // grow, an implausible gift the NPC has no reason to want.
      [Test]
      public void GIVEN_a_size_limit_below_the_minimum_worth_reinforcing_WHEN_checking_if_weakened_THEN_a_tiny_escort_never_counts()
      {
         //Arrange: even a nearly-empty 5-troop escort (limit 10) is not "under-strength", it is just small.
         CourtActionPolicy.IsPartyWeakened(1, 10).Should().BeFalse();
      }

      // GiveTroopsMinRelation is 0, far below lend_troops' own-initiative threshold: this reciprocal
      // direction is charity toward the needy, not a bond-driven favour (XML doc on GiveTroopsMinRelation).
      // But a genuinely hostile NPC (negative relation) must still refuse the player's troops outright.
      [Test]
      public void GIVEN_relation_below_the_charity_floor_WHEN_computing_the_troop_gift_THEN_it_is_zero()
      {
         CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = -5, PlayerHealthyTroops = 200, NpcRoom = 20}).Should().Be(0);
      }

      // Pins the desired-count formula in the unconstrained case, isolating it from the player-floor and
      // NPC-room caps exercised by the tests below.
      [Test]
      public void GIVEN_ample_spare_troops_and_room_WHEN_computing_the_troop_gift_THEN_it_scales_with_relation()
      {
         //Arrange: relation 30 / 6 + 1 = 6 desired, well within the player's spare and the NPC's room.
         CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = 30, PlayerHealthyTroops = 300, NpcRoom = 50}).Should().Be(6);
      }

      // Hard cap (GiveTroopsMaxCount) keeps a single reinforcement gift bounded regardless of how deep
      // the bond runs, mirroring the lend_troops cap in the opposite direction.
      [Test]
      public void GIVEN_an_extremely_deep_relation_WHEN_computing_the_troop_gift_THEN_it_is_capped()
      {
         CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = 1000, PlayerHealthyTroops = 1000, NpcRoom = 1000}).Should().Be(CourtActionPolicy.GiveTroopsMaxCount);
      }

      // Protects the PLAYER's own safety net (mirrors TroopLoanMinRetainFraction in the other direction):
      // without it a generous player could be talked down toward zero fighting strength trying to help
      // an ally.
      [Test]
      public void GIVEN_the_player_has_barely_more_than_the_floor_WHEN_computing_the_troop_gift_THEN_it_never_dips_the_player_below_the_floor()
      {
         //Arrange: 20 troops, must keep max(15, 70% of 20=14) = 15, so at most 5 are spare regardless of relation.
         CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = 1000, PlayerHealthyTroops = 20, NpcRoom = 50}).Should().Be(5);
      }

      // Once the player is already at or under GiveTroopsPlayerFloor, the gift must be zero outright,
      // not merely reduced, so the player can never be talked below their own safety floor.
      [Test]
      public void GIVEN_the_player_at_or_under_the_floor_WHEN_computing_the_troop_gift_THEN_it_is_zero()
      {
         CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = 50, PlayerHealthyTroops = 15, NpcRoom = 10}).Should().Be(0);
      }

      // Pins the NPC-room cap: without it the gift could exceed the receiving party's own size limit,
      // a promise the bridge would then be unable to honour in full.
      [Test]
      public void GIVEN_the_npc_party_nearly_full_WHEN_computing_the_troop_gift_THEN_it_never_exceeds_the_npcs_room()
      {
         CourtActionPolicy.TroopGiftCount(new CourtActionPolicy.TroopGiftFacts {Relation = 1000, PlayerHealthyTroops = 1000, NpcRoom = 2}).Should().Be(2);
      }

      // ── spend_influence (reciprocal of give_influence) ──────────────────

      // Baseline "yes" case for the need gate that decides whether spend_influence is ever offered for
      // this lord's clan at all.
      [Test]
      public void GIVEN_a_clan_influence_below_the_need_threshold_WHEN_checking_if_a_clan_needs_influence_THEN_it_does()
      {
         CourtActionPolicy.ClanNeedsInfluence(CourtActionPolicy.SpendInfluenceNpcNeedThreshold - 1).Should().BeTrue();
      }

      // Without this gate the player could "back" a lord's clan at court even when that clan is already
      // influence-rich, an implausible spend the LLM should never be taught to offer.
      [Test]
      public void GIVEN_a_clan_influence_at_or_above_the_need_threshold_WHEN_checking_if_a_clan_needs_influence_THEN_it_does_not()
      {
         CourtActionPolicy.ClanNeedsInfluence(CourtActionPolicy.SpendInfluenceNpcNeedThreshold).Should().BeFalse();
      }

      // ── sway_opinion ──────────────────────────────────────────────────────

      // Pins sign and magnitude of the relation delta actually dealt to the named third party; get the
      // sign wrong and sway_opinion would reward the target instead of punishing them, while the chat
      // still narrates the opposite.
      [Test]
      public void GIVEN_swaying_against_a_target_WHEN_computing_the_delta_THEN_it_is_negative()
      {
         CourtActionPolicy.SwayDelta(true).Should().Be(CourtActionPolicy.SwayAgainstDelta).And.BeNegative();
      }

      // Same stake as the AGAINST case, opposite sign: pins that swaying FOR a target actually helps
      // them rather than accidentally applying the AGAINST delta.
      [Test]
      public void GIVEN_swaying_for_a_target_WHEN_computing_the_delta_THEN_it_is_positive()
      {
         CourtActionPolicy.SwayDelta(false).Should().Be(CourtActionPolicy.SwayForDelta).And.BePositive();
      }

      // ── sway_opinion target selection ────────────────────────────────────

      // Hard narrative invariant ("nobody turns against their own spouse, child, parent, or sibling on
      // request", XML doc on SelectSwayTarget): without this exclusion sway_opinion could have an NPC
      // turn on their own family at the player's mere word.
      [Test]
      public void GIVEN_a_pool_with_close_kin_and_a_stranger_WHEN_selecting_a_sway_target_THEN_kin_is_never_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("spouse", relation: -50, isCloseKin: true),
            new("stranger", relation: 10, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("stranger");
      }

      // Pins that when every candidate is excluded, the picker degrades to "no target" rather than
      // falling back to picking a kin anyway.
      [Test]
      public void GIVEN_only_close_kin_in_the_pool_WHEN_selecting_a_sway_target_THEN_none_is_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("child", relation: -80, isCloseKin: true),
            new("sibling", relation: -20, isCloseKin: true)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().BeNull();
      }

      // Without this, the picker could target someone the NPC actually gets along with, breaking the
      // "existing rift makes a turn believable" plausibility rule the whole verb rests on.
      [Test]
      public void GIVEN_several_non_kin_candidates_WHEN_selecting_a_sway_target_THEN_the_most_negative_relation_wins()
      {
         //Arrange: an existing rift makes the turn believable; a friend or ally would not credibly turn.
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("friend", relation: 40, isCloseKin: false),
            new("rival", relation: -25, isCloseKin: false),
            new("acquaintance", relation: 0, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("rival");
      }

      // Pins the fallback branch (XML doc: "falls back to the least-friendly remaining candidate when
      // every relation is positive") so the picker never returns null just because nobody is disliked yet.
      [Test]
      public void GIVEN_only_positive_relation_non_kin_candidates_WHEN_selecting_a_sway_target_THEN_the_least_friendly_still_wins()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("closeAlly", relation: 60, isCloseKin: false),
            new("mildAcquaintance", relation: 15, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("mildAcquaintance");
      }

      // Guards the degenerate no-candidates case: the picker must fail closed (null), never throw or
      // return a stale/default id.
      [Test]
      public void GIVEN_an_empty_pool_WHEN_selecting_a_sway_target_THEN_none_is_chosen()
      {
         CourtActionPolicy.SelectSwayTarget(new List<CourtActionPolicy.SwayCandidate>()).Should().BeNull();
      }

      // Ties directly to the ROADMAP's war-declaration constraint: "war declaration = a leader-only
      // strategic decision, NEVER a conversation or pledge trigger". Without this exclusion, sway_opinion
      // (and pledge_against, tested alongside it below) could be used to move against a ruler through a
      // side channel the war-decision system was deliberately kept apart from.
      [Test]
      public void GIVEN_the_worst_regarded_candidate_is_a_faction_leader_WHEN_selecting_a_sway_target_THEN_the_ruler_is_skipped()
      {
         // Moving against a ruler is a war-scale act, the leader's own strategic decision, never a personal
         // rivalry a lord settles at another's word, so a faction leader is never a sway or pledge target.
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("empress", relation: -80, isCloseKin: false, isFactionLeader: true),
            new("rival", relation: -25, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("rival");
         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("rival");
      }

      // ── pledge_against target selection ──────────────────────────────────

      // Baseline "yes" case: a pledge that launches a real scheme (SchemeStore.ForcePlot) must actually
      // fire when the cause is genuine, or the "he promised YOU" accountability layer never engages.
      [Test]
      public void GIVEN_a_bitterly_disliked_non_kin_lord_WHEN_selecting_a_pledge_target_THEN_they_are_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("rival", relation: -40, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("rival");
      }

      // Mirrors sway_opinion's kin exclusion, but the stakes are higher here: pledge_against launches a
      // real scheme (slander or sabotage) against the target, so letting it reach an NPC's own blood
      // would be a scheme, not just a comment, aimed at their family.
      [Test]
      public void GIVEN_close_kin_held_in_the_same_enmity_WHEN_selecting_a_pledge_target_THEN_kin_is_never_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("estrangedSibling", relation: -60, isCloseKin: true),
            new("rival", relation: -25, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("rival");
      }

      // "A pledge needs a real cause, not merely the least friendly face in an amicable pool" (XML doc on
      // SelectPledgeTarget): without PledgeMinAnimosity a lord could pledge, and the bridge would launch
      // a real scheme, against someone they merely find mildly annoying.
      [Test]
      public void GIVEN_no_candidate_meets_the_animosity_threshold_WHEN_selecting_a_pledge_target_THEN_none_is_chosen()
      {
         //Arrange: a mild dislike is not a genuine cause to move against someone.
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("acquaintance", relation: -5, isCloseKin: false),
            new("friend", relation: 20, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().BeNull();
      }

      // Among several genuine rivals, the deepest enmity must win so the scheme the pledge launches
      // targets whoever the lord has the strongest, most dramatically justified cause against.
      [Test]
      public void GIVEN_several_eligible_rivals_WHEN_selecting_a_pledge_target_THEN_the_deepest_enmity_wins()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("oldGrudge", relation: -25, isCloseKin: false),
            new("bitterEnemy", relation: -70, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("bitterEnemy");
      }

      // Guards the degenerate no-candidates case, mirroring the sway_opinion empty-pool test: fail
      // closed (null), never throw.
      [Test]
      public void GIVEN_an_empty_pool_WHEN_selecting_a_pledge_target_THEN_none_is_chosen()
      {
         CourtActionPolicy.SelectPledgeTarget(new List<CourtActionPolicy.SwayCandidate>()).Should().BeNull();
      }

      // ── shared cooldown math ─────────────────────────────────────────────

      // Null "never recorded" must count as elapsed so a verb is available the FIRST time it is ever
      // offered to a given NPC; treating null as "not yet elapsed" would permanently lock every
      // cooldown-gated court verb out on a fresh profile.
      [Test]
      public void GIVEN_no_prior_recorded_day_WHEN_checking_the_cooldown_THEN_it_has_elapsed()
      {
         CourtActionPolicy.CooldownElapsed(null, 100, 7).Should().BeTrue();
      }

      // Without this, an NPC could be asked for the same favour (or swayed against the same target)
      // again and again with no in-fiction pause, undermining every per-verb cooldown listed in
      // CourtActionPolicy (7-14 days depending on the verb).
      [Test]
      public void GIVEN_the_cooldown_window_not_yet_passed_WHEN_checking_it_THEN_it_has_not_elapsed()
      {
         CourtActionPolicy.CooldownElapsed(100, 103, 7).Should().BeFalse();
      }

      // Pins the ">=" boundary so a verb becomes available again exactly ON the day the cooldown
      // completes, not one day later; this is the shared clock every court verb above reads through
      // CooldownElapsed, so a drift here silently retunes every one of them at once.
      [Test]
      public void GIVEN_exactly_the_cooldown_length_WHEN_checking_it_THEN_the_threshold_is_inclusive()
      {
         CourtActionPolicy.CooldownElapsed(100, 107, 7).Should().BeTrue();
      }

      // Pins the far side of the window (well beyond the threshold) stays elapsed, guarding against an
      // off-by-window regression that would only show up once save data accumulates real day gaps.
      [Test]
      public void GIVEN_well_past_the_cooldown_window_WHEN_checking_it_THEN_it_has_elapsed()
      {
         CourtActionPolicy.CooldownElapsed(100, 200, 7).Should().BeTrue();
      }

      // ── player household exclusion (v1.34.2 hardening, prisoner/gold audit finding 4) ──

      // STAKE: the held-prisoner pool (sell_prisoner / release_prisoner) excluded only the player, not the
      // player's CLAN. A captured brother or child of the player's own house could then be "bought" back into
      // the player's own cage or handled as merchandise; a member of your own house is never stock to trade.
      [Test]
      public void GIVEN_a_member_of_the_players_own_clan_WHEN_testing_the_household_gate_THEN_they_are_excluded()
      {
         CourtActionPolicy.IsPlayerHousehold(isMainHero: false, sharesPlayerClan: true).Should().BeTrue();
      }

      // STAKE: the original exclusion (the player themself) must survive the widening to the whole clan, or the
      // player could be listed as their own merchandise.
      [Test]
      public void GIVEN_the_player_themself_WHEN_testing_the_household_gate_THEN_they_are_excluded()
      {
         CourtActionPolicy.IsPlayerHousehold(isMainHero: true, sharesPlayerClan: false).Should().BeTrue();
      }

      // STAKE: the gate must stay narrow: an ordinary captured lord of another house IS legitimate merchandise,
      // so widening the exclusion must not accidentally swallow every prisoner and empty the trade pool.
      [Test]
      public void GIVEN_an_unrelated_captured_lord_WHEN_testing_the_household_gate_THEN_they_remain_tradeable()
      {
         CourtActionPolicy.IsPlayerHousehold(isMainHero: false, sharesPlayerClan: false).Should().BeFalse();
      }
   }
}
