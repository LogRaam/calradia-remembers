// Code written by Gabriel Mailhot, 20/06/2026. Rewritten 05/09/2026 for the progressive-regard model.
// This is CalradiaRemembers.Logic's core anti-exploit rule: the LLM proposes a relation delta after every
// exchange, and RelationGate is the only thing standing between that proposal and the number
// BannerlordGameStateBridge actually applies to Hero relation. Get it wrong and players can talk their way to max
// relation, the exact failure this mod exists to avoid.
//
// The model (player request 2026-09-05, replacing the flat one-gain-per-8h routine throttle): earning a point of
// personal regard RAISES the bar. Tier 0 grants a +1 for ONE genuine warm moment; tier 1 needs TWO moments; tier 2
// needs THREE; and so on. Each tier relaxes by one only after RegardDecayDays in-game days, so a long real
// conversation can earn several points (its moments accrue while game time is frozen), a drive-by earns one then
// hits the wall, and the wall cannot be farmed by skipping 8 hours and returning. Intimacy stays a separate,
// guarded +3 (its own 24h cooldown); negatives stay uncapped except a floor, deeper for a verified captive. These
// tests are the only protection this silently-breaking rule has.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RelationGateTests
   {
      private const double Day = 24.0;
      private const double DecayDays = 7.0; // the shipped default per-tier decay period

      // ── The magnitude clamp (unchanged pillar) ──────────────────────────

      // The exploit the gate exists to close: an LLM asked to play along proposes double-digit gains from ordinary
      // flattery. A routine grant is ALWAYS +1 (never the proposed size), so trust is gained slowly whatever the
      // model asks for. The first warm moment on a fresh NPC lands it and raises the bar to tier 1.
      [Test]
      public void GIVEN_a_generous_positive_proposal_on_a_fresh_npc_WHEN_resolving_THEN_a_routine_gain_is_capped_to_one_and_the_tier_rises()
      {
         NpcProfile p = Profile();

         int granted = RelationGate.Resolve(10, p, null, 100.0, false, false);

         granted.Should().Be(1, "a routine gain is +1 no matter how large the proposal");
         p.RegardResistanceTier.Should().Be(1, "granting a point raises the bar for the next one");
         p.RegardWarmthAccrued.Should().Be(0);
         p.RegardResistanceStampHour.Should().Be(100.0, "the tier's decay window is anchored at the moment it rose");
      }

      // ── The progression itself (the whole point of the redesign) ─────────

      // A/B/C, evaluated within a single conversation (game time is frozen, so nothing decays mid-talk): tier 0 takes
      // one moment, tier 1 two, tier 2 three. A long, genuinely warm sitting earns several points; a shallow one
      // earns one and stalls. Without this rising cost, +1-per-message would let a patient player climb without limit.
      [Test]
      public void GIVEN_repeated_warm_moments_in_one_conversation_WHEN_resolving_THEN_each_tier_costs_one_more_moment()
      {
         NpcProfile p = Profile();

         RelationGate.Resolve(1, p, null, 100.0, false, false).Should().Be(1, "tier 0: one moment lands a point");
         RelationGate.Resolve(1, p, null, 100.0, false, false).Should().Be(0, "tier 1: the first of two moments only banks");
         p.RegardWarmthAccrued.Should().Be(1);
         RelationGate.Resolve(1, p, null, 100.0, false, false).Should().Be(1, "tier 1: the second moment lands the point");
         p.RegardResistanceTier.Should().Be(2);
         RelationGate.Resolve(1, p, null, 100.0, false, false).Should().Be(0, "tier 2: needs three");
         RelationGate.Resolve(1, p, null, 100.0, false, false).Should().Be(0);
         RelationGate.Resolve(1, p, null, 100.0, false, false).Should().Be(1, "tier 2: the third moment lands it");
         p.RegardResistanceTier.Should().Be(3);
      }

      // The exposed curve helper the UI and the above rely on: tier N demands N+1 moments, and a nonsensical
      // negative tier never asks for fewer than one.
      [Test]
      public void GIVEN_a_tier_WHEN_asking_the_moments_required_THEN_it_is_that_tier_plus_one()
      {
         RelationGate.MomentsRequiredForTier(0).Should().Be(1);
         RelationGate.MomentsRequiredForTier(1).Should().Be(2);
         RelationGate.MomentsRequiredForTier(4).Should().Be(5);
         RelationGate.MomentsRequiredForTier(-3).Should().Be(1);
      }

      // ── The anti-farm guarantee ──────────────────────────────────────────

      // The exact cheese the redesign kills: earn a point, leave, skip a few hours, come back for another. At tier 1
      // the bar is TWO moments and it has barely relaxed after 8 hours, so the drive-by return banks a moment and
      // grants nothing. Time-skipping no longer prints regard.
      [Test]
      public void GIVEN_a_single_moment_a_few_hours_after_the_tier_rose_WHEN_resolving_THEN_it_is_still_gated()
      {
         NpcProfile p = Profile(tier: 1, stampHour: 100.0);

         RelationGate.Resolve(1, p, null, 108.0, false, false).Should().Be(0, "8h is nowhere near a decay step");
         p.RegardResistanceTier.Should().Be(1, "the tier did not relax");
      }

      // ── The decay (what makes neglected warmth cool) ─────────────────────

      // Left alone long enough, a bar relaxes: two full decay windows walk a tier-2 resistance all the way back to 0,
      // so a returning player's first warm moment lands a point again (and re-raises the bar to 1). This is what keeps
      // the wall from being permanent, and what "warmth settles over time" means mechanically.
      [Test]
      public void GIVEN_two_full_decay_windows_of_absence_WHEN_resolving_THEN_the_resistance_has_cooled_and_a_moment_lands()
      {
         NpcProfile p = Profile(tier: 2, stampHour: 100.0);

         int granted = RelationGate.Resolve(1, p, null, 100.0 + 2 * DecayDays * Day, false, false);

         granted.Should().Be(1, "the bar decayed to 0, so one moment suffices again");
         p.RegardResistanceTier.Should().Be(1, "and the fresh point raised it back to 1");
      }

      // Partial time is not enough: just under one decay window later, the tier is unchanged, so the moment still
      // banks. Pins that decay is measured in whole windows, not a smooth trickle that would soften the anti-farm.
      [Test]
      public void GIVEN_almost_but_not_quite_a_decay_window_WHEN_resolving_THEN_the_tier_has_not_relaxed_yet()
      {
         NpcProfile p = Profile(tier: 1, stampHour: 100.0);

         RelationGate.Resolve(1, p, null, 100.0 + (DecayDays * Day - 1.0), false, false).Should().Be(0);
         p.RegardResistanceTier.Should().Be(1);
      }

      // The player's dial at zero, REVERSED on 2026-09-10, and the reversal is the point of the test.
      //
      // It used to mean "cooling disabled", so a tier froze wherever it stood and warmth became permanently
      // dearer. That is the exact opposite of what the v2.5.1 changelog told players the control does: "set it to
      // 0 to turn the cooldown off". Nobody was ever bitten, because the MCM control clamped its own minimum to 1
      // and the value was unreachable; the promise had simply never been kept. FloatingMammal asked for the zero
      // he had been promised (Nexus, 2026-09-10: "The changelog said it was supposed to be able to be set to 0 so
      // there is no cooldown"), which is what sent anyone to read this code at all.
      //
      // Zero now means what the label says: the cooldown is off, the resistance never accumulates, and a warm
      // exchange is worth the same on the hundredth conversation as on the first. It is not unlimited, because a
      // tier-0 grant still costs one banked moment, so the ceiling is +1 per warm exchange: the flat behaviour
      // the progressive model replaced, offered back to a player who wants it. When a control and its changelog
      // disagree, the changelog is the promise the player actually read.
      // The behaviour that matters is what a player feels across a conversation, not the bookkeeping between two
      // calls: the tier is still bumped as part of granting, and relaxed again the next time it is consulted.
      // So this asserts the thing worth asserting, that warmth never becomes dearer however long it goes on.
      [Test]
      public void GIVEN_the_cooldown_turned_off_WHEN_resolving_THEN_warmth_never_becomes_dearer()
      {
         NpcProfile p = Profile(tier: 1, stampHour: 100.0);

         for (var exchange = 0; exchange < 6; exchange++)
            RelationGate.Resolve(1, p, null, 100.0 + exchange, false, false, RelationWarmth.Normal, 0.0)
                        .Should().Be(1, "every warm exchange is worth the same when the cooldown is off");
      }

      // And it is off immediately, not after a wait: that is the whole difference between "no cooldown" and "a
      // very short one", and it is what a player who set 0 will notice on their very next line. A deep bond that
      // had climbed to tier 4 deepens at once rather than demanding five moments first.
      [Test]
      public void GIVEN_the_cooldown_turned_off_WHEN_resolving_at_once_THEN_a_deep_bond_still_deepens()
      {
         NpcProfile p = Profile(tier: 4, stampHour: 100.0);

         RelationGate.Resolve(1, p, null, 100.0, false, false, RelationWarmth.Normal, 0.0).Should().Be(1);
      }

      // The default is untouched, which is the promise to everyone who never opens the menu: at 7 days a deep
      // bond still resists, exactly as it did before zero meant anything.
      [Test]
      public void GIVEN_the_default_cooldown_WHEN_a_deep_bond_resolves_THEN_it_still_resists_as_it_always_did()
      {
         NpcProfile p = Profile(tier: 4, stampHour: 100.0);

         RelationGate.Resolve(1, p, null, 100.0, false, false, RelationWarmth.Normal, DecayDays).Should().Be(0);
      }

      // But off is not a licence: one warm exchange still buys at most one point, so a player cannot walk in and
      // out of a conversation to run a bond up in a single afternoon.
      [Test]
      public void GIVEN_the_cooldown_turned_off_WHEN_a_single_exchange_resolves_THEN_it_is_still_only_one_point()
      {
         NpcProfile p = Profile(tier: 0, stampHour: null);

         RelationGate.Resolve(5, p, null, 100.0, false, false, RelationWarmth.Normal, 0.0).Should().Be(1);
      }

      // ── The unchanged pillars ────────────────────────────────────────────

      // Negatives are the mirror pillar: trust lost quickly, so no throttle and no discount, an insult costs its full
      // value the instant it happens, unlike a compliment which must clear the rising bar.
      [Test]
      public void GIVEN_a_moderate_insult_WHEN_resolving_THEN_the_full_negative_applies_immediately()
         => RelationGate.Resolve(-7, Profile(), null, 100.5, false, false).Should().Be(-7);

      // Fail-safe: an unknown/not-yet-tracked NPC (null profile) must return 0, never throw, since this runs on every
      // single proposal.
      [Test]
      public void GIVEN_a_null_profile_WHEN_resolving_THEN_nothing_moves()
         => RelationGate.Resolve(5, null, null, 100.0, false, false).Should().Be(0);

      // A zero proposal is a no-op: it must not accrue a moment or move the tier, or an empty exchange would nudge
      // the progression on its own.
      [Test]
      public void GIVEN_a_zero_delta_WHEN_resolving_THEN_nothing_moves_and_no_moment_accrues()
      {
         NpcProfile p = Profile();

         RelationGate.Resolve(0, p, null, 100.0, false, false).Should().Be(0);
         p.RegardWarmthAccrued.Should().Be(0);
      }

      // The ordinary floor at -15: one ugly exchange can sour things badly but not annihilate the relation; the
      // deeper collapse is reserved for a verified captive.
      [Test]
      public void GIVEN_a_savage_insult_in_ordinary_conversation_WHEN_resolving_THEN_it_stops_at_the_conversational_floor()
         => RelationGate.Resolve(-40, Profile(), null, 100.0, false, false).Should().Be(-15);

      // The same cruelty lands in full against a captive: the deeper floor is gated on the ENGINE's captive status,
      // never on anything the LLM narrates.
      [Test]
      public void GIVEN_the_same_cruelty_toward_a_captive_WHEN_resolving_THEN_the_deeper_captive_floor_is_unlocked()
         => RelationGate.Resolve(-40, Profile(), null, 100.0, true, false).Should().Be(-40);

      // Even the captive floor caps a wild proposal at -100 rather than passing the raw magnitude through.
      [Test]
      public void GIVEN_an_extreme_proposal_against_a_captive_WHEN_resolving_THEN_it_still_stops_at_the_captive_floor()
         => RelationGate.Resolve(-500, Profile(), null, 100.0, true, false).Should().Be(-100);

      // ── Intimacy: the guarded, separate +3 (unchanged, outside the progression) ──

      // An allowed intimacy beat bypasses the progressive bar entirely for a one-off +3, and stamps BOTH clocks so it
      // opens no loophole for a second immediate bonus. It does NOT touch the routine tier.
      [Test]
      public void GIVEN_an_allowed_intimacy_moment_WHEN_resolving_THEN_it_grants_three_and_leaves_the_routine_tier_untouched()
      {
         NpcProfile p = Profile();

         int granted = RelationGate.Resolve(10, p, NotableEventType.Intimacy, 100.0, false, intimacyAllowed: true);

         granted.Should().Be(3);
         p.LastIntimacyGainHour.Should().Be(100.0, "a granted intimacy bonus stamps its own dedicated cooldown");
         p.RegardResistanceTier.Should().Be(0, "the intimacy path never moves the routine progression");
      }

      // C1: a disallowed intimacy tag (adult off, no romance, incompatible pair) is demoted to an ordinary warm
      // moment, never the +3. On a fresh NPC that lands a routine +1, so the +3 can never be reached without vouch.
      [Test]
      public void GIVEN_a_disallowed_intimacy_tag_on_a_fresh_npc_WHEN_resolving_THEN_it_lands_only_the_routine_point()
      {
         NpcProfile p = Profile();

         RelationGate.Resolve(10, p, NotableEventType.Intimacy, 100.0, false, intimacyAllowed: false).Should().Be(1);
         p.LastIntimacyGainHour.Should().BeNull("a demoted tag never stamps the intimacy clock");
      }

      // C1 against the progression: a demoted intimacy tag at a raised tier is just one warm moment, so it banks
      // toward the (tier + 1) requirement rather than jumping the bar, and still never stamps the intimacy clock.
      [Test]
      public void GIVEN_a_disallowed_intimacy_tag_at_a_raised_tier_WHEN_resolving_THEN_it_only_banks_a_moment()
      {
         NpcProfile p = Profile(tier: 1, stampHour: 100.0);

         RelationGate.Resolve(10, p, NotableEventType.Intimacy, 100.0, false, intimacyAllowed: false).Should().Be(0);
         p.RegardWarmthAccrued.Should().Be(1);
         p.LastIntimacyGainHour.Should().BeNull();
      }

      // C1's dedicated 24h window: a second allowed intimacy inside it is demoted to routine (here it banks against a
      // raised tier), so a genuine scene grants +3 at most once a day, never every turn.
      [Test]
      public void GIVEN_a_second_allowed_intimacy_within_its_own_cooldown_WHEN_resolving_THEN_it_is_demoted_to_a_routine_moment()
      {
         NpcProfile p = Profile(tier: 1, stampHour: 100.0, lastIntimacyHour: 100.0);

         RelationGate.Resolve(10, p, NotableEventType.Intimacy, 105.0, false, intimacyAllowed: true).Should().Be(0);
         p.RegardWarmthAccrued.Should().Be(1, "demoted to a routine moment, banked against the tier-1 bar");
      }

      // Once the 24h intimacy window has elapsed, an allowed beat lands the full +3 again: the cooldown is a real
      // window, not a one-time gate.
      [Test]
      public void GIVEN_an_allowed_intimacy_after_its_own_cooldown_WHEN_resolving_THEN_it_grants_three_again()
      {
         NpcProfile p = Profile(lastIntimacyHour: 100.0);

         RelationGate.Resolve(10, p, NotableEventType.Intimacy, 125.0, false, intimacyAllowed: true).Should().Be(3);
      }

      #region private

      private static NpcProfile Profile(int tier = 0, double? stampHour = null, double? lastIntimacyHour = null)
         => new() {
            Id = "test_npc",
            Name = "Test Npc",
            Clan = "Test Clan",
            Faction = "Test Faction",
            RegardResistanceTier = tier,
            RegardResistanceStampHour = stampHour,
            LastIntimacyGainHour = lastIntimacyHour
         };

      #endregion
   }
}
