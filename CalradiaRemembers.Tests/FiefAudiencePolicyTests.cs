// Code written by Gabriel Mailhot, 20/08/2026.
// Player wish (Fakade, roadmap): everyone with no field party (ladies, notables, a garrisoned lord standing
// around in a fief) never got a turn under road-only InterceptionBehavior. This file protects the two rules
// that make the fief audience safe to ship: WHICH of InterceptionPolicy's triggers may fire off the road (get
// it wrong and a hostile-territory "you crossed my land" line comes out of a lord who never moved), and the
// per-NPC cooldown that keeps a busy town from opening a queue of "may I have a word" popups.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class FiefAudiencePolicyTests
   {
      // ── IsFiefValid / ChooseFiefValid ────────────────────────────────────────

      // The whole point of this filter: TerritorialPatrol is written around a lord riding out to challenge a
      // traveller CROSSING their land. An NPC standing in a fief never intercepted a crossing, so the SAME
      // facts that would ride a lord out on the road must never voice that line from someone who never moved.
      [Test]
      public void GIVEN_facts_that_would_choose_TerritorialPatrol_WHEN_narrowed_to_fief_THEN_it_is_excluded()
      {
         InterceptionPolicy.Choose(TerritorialFacts()).Should().Be(InterceptionTrigger.TerritorialPatrol); // sanity: the road trigger still fires
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.TerritorialPatrol).Should().BeFalse();
         FiefAudiencePolicy.ChooseFiefValid(TerritorialFacts()).Should().Be(InterceptionTrigger.None);
      }

      // The other half: HeardOfDeed reads just as well from someone who simply asks for a word where they
      // already stand as it does from a lord who rode out to say it, so it must survive the narrowing intact.
      [Test]
      public void GIVEN_facts_that_would_choose_HeardOfDeed_WHEN_narrowed_to_fief_THEN_it_survives()
      {
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.HeardOfDeed).Should().BeTrue();
         FiefAudiencePolicy.ChooseFiefValid(HeardOfDeedFacts()).Should().Be(InterceptionTrigger.HeardOfDeed);
      }

      // Even when BOTH conditions hold at once (InterceptionPolicy.Choose would rank TerritorialPatrol first),
      // the fief-valid narrowing must still exclude it rather than surface the road-only trigger just because
      // it happened to win the underlying priority order.
      [Test]
      public void GIVEN_both_triggers_would_qualify_WHEN_narrowed_to_fief_THEN_the_road_only_one_never_wins()
      {
         var facts = new InterceptionFacts {
            FactionHostileToPlayer = true,
            PlayerInOwnTerritory = true,
            PlayerNotoriety = InterceptionPolicy.NotorietyThreshold,
            HeardRecentPlayerDeed = true
         };

         InterceptionPolicy.Choose(facts).Should().Be(InterceptionTrigger.TerritorialPatrol); // sanity: priority order unchanged
         FiefAudiencePolicy.ChooseFiefValid(facts).Should().Be(InterceptionTrigger.None);
      }

      // Nothing applying at all must resolve to None off the road exactly as it does on it.
      [Test]
      public void GIVEN_nothing_applies_WHEN_narrowed_to_fief_THEN_the_answer_is_none()
         => FiefAudiencePolicy.ChooseFiefValid(new InterceptionFacts()).Should().Be(InterceptionTrigger.None);

      // A grievance is place-independent (this file's own IsFiefValid doc): a partyless NPC standing in a
      // fief has just as much standing to confront the player over a live grudge as a lord who rode out
      // would, so it must survive the narrowing intact, exactly like HeardOfDeed does.
      [Test]
      public void GIVEN_facts_that_would_choose_Grudge_WHEN_narrowed_to_fief_THEN_it_survives()
      {
         InterceptionPolicy.Choose(GrudgeFacts()).Should().Be(InterceptionTrigger.Grudge); // sanity: the road trigger still fires
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.Grudge).Should().BeTrue();
         FiefAudiencePolicy.ChooseFiefValid(GrudgeFacts()).Should().Be(InterceptionTrigger.Grudge);
      }

      // The mirror case for the warm ledger: a felt kindness is just as place-independent as a grievance, so
      // AppreciationOwed must also survive the narrowing.
      [Test]
      public void GIVEN_facts_that_would_choose_AppreciationOwed_WHEN_narrowed_to_fief_THEN_it_survives()
      {
         InterceptionPolicy.Choose(AppreciationFacts()).Should().Be(InterceptionTrigger.AppreciationOwed); // sanity
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.AppreciationOwed).Should().BeTrue();
         FiefAudiencePolicy.ChooseFiefValid(AppreciationFacts()).Should().Be(InterceptionTrigger.AppreciationOwed);
      }

      // A lord raising a nemesis plaguing his region is just as plausible standing in his own fief as riding
      // out to say it (IsFiefValid's own doc), so NemesisBounty must survive the narrowing intact too.
      [Test]
      public void GIVEN_facts_that_would_choose_NemesisBounty_WHEN_narrowed_to_fief_THEN_it_survives()
      {
         InterceptionPolicy.Choose(NemesisBountyFacts()).Should().Be(InterceptionTrigger.NemesisBounty); // sanity
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.NemesisBounty).Should().BeTrue();
         FiefAudiencePolicy.ChooseFiefValid(NemesisBountyFacts()).Should().Be(InterceptionTrigger.NemesisBounty);
      }

      // ── IsOnCooldown ──────────────────────────────────────────────────────────

      // A candidate never offered before must never read as on cooldown, or nobody standing in a fief could
      // ever receive a first offer.
      [Test]
      public void GIVEN_a_candidate_never_offered_WHEN_checking_cooldown_THEN_it_is_not_on_cooldown()
         => FiefAudiencePolicy.IsOnCooldown(FiefAudiencePolicy.NeverOffered, today: 100, InterceptionPacing.Occasionally)
                               .Should().BeFalse();

      // The rate-limit itself: offered yesterday, under the shipped Occasionally cooldown (30 days), must still
      // withhold today -- this is the guard against the SAME NPC opening a second popup right after the first.
      [Test]
      public void GIVEN_offered_yesterday_WHEN_checking_cooldown_under_the_default_pacing_THEN_it_is_on_cooldown()
         => FiefAudiencePolicy.IsOnCooldown(lastOfferedDay: 99, today: 100, InterceptionPacing.Occasionally)
                               .Should().BeTrue();

      // Once the SAME cooldown window InterceptionPacingRules defines has fully elapsed, the same NPC may be
      // offered again -- pins that this reuses that dial rather than a silently different number.
      [Test]
      public void GIVEN_the_cooldown_window_has_fully_elapsed_WHEN_checking_cooldown_THEN_it_is_no_longer_on_cooldown()
      {
         int lastOffered = 100;
         int today = lastOffered + InterceptionPacingRules.CooldownDays(InterceptionPacing.Occasionally);

         FiefAudiencePolicy.IsOnCooldown(lastOffered, today, InterceptionPacing.Occasionally).Should().BeFalse();
      }

      // Reuses InterceptionPacingRules.CooldownDays rather than a second, independently-tuned number: the same
      // pacing choice the player already made for road interceptions ("Lords Approaching You") governs the
      // fief cooldown too, so the two features feel like one dial, not two the player has to reconcile.
      [Test]
      public void GIVEN_the_Never_pacing_WHEN_checking_cooldown_THEN_it_mirrors_InterceptionPacingRules_exactly()
      {
         int lastOffered = 10;
         int today = lastOffered + InterceptionPacingRules.CooldownDays(InterceptionPacing.Never) - 1;

         FiefAudiencePolicy.IsOnCooldown(lastOffered, today, InterceptionPacing.Never).Should().BeTrue();
      }

      // ── SelectCandidate ───────────────────────────────────────────────────────

      // "Keep the strongest standing-in-a-fief-valid trigger" -- a candidate whose Trigger already narrowed to
      // None (nothing fief-valid found for them) must never be handed back as a winner.
      [Test]
      public void GIVEN_a_candidate_with_no_fief_valid_trigger_WHEN_selecting_THEN_it_is_never_chosen()
      {
         var candidates = new List<FiefAudienceCandidate> {
            new FiefAudienceCandidate("lord_a", InterceptionTrigger.None),
            new FiefAudienceCandidate("lord_b", InterceptionTrigger.HeardOfDeed)
         };

         FiefAudiencePolicy.SelectCandidate(candidates)?.HeroId.Should().Be("lord_b");
      }

      // An empty (or entirely None) roster must yield no offer -- entering an empty fief must never surface a
      // popup out of thin air.
      [Test]
      public void GIVEN_no_candidate_qualifies_WHEN_selecting_THEN_the_answer_is_null()
         => FiefAudiencePolicy.SelectCandidate(new List<FiefAudienceCandidate>()).Should().BeNull();

      // "Entering a busy town must not queue audiences": with several qualifying NPCs, exactly ONE winner must
      // come back, and it must be the SAME one every time regardless of scan order -- otherwise the SAME
      // settlement state could open a different popup on different session launches.
      [Test]
      public void GIVEN_several_qualifying_candidates_WHEN_selecting_THEN_exactly_one_winner_comes_back_deterministically()
      {
         var order1 = new List<FiefAudienceCandidate> {
            new FiefAudienceCandidate("lord_z", InterceptionTrigger.HeardOfDeed),
            new FiefAudienceCandidate("lord_a", InterceptionTrigger.HeardOfDeed)
         };
         var order2 = new List<FiefAudienceCandidate> {
            new FiefAudienceCandidate("lord_a", InterceptionTrigger.HeardOfDeed),
            new FiefAudienceCandidate("lord_z", InterceptionTrigger.HeardOfDeed)
         };

         FiefAudienceCandidate fromOrder1 = FiefAudiencePolicy.SelectCandidate(order1);
         FiefAudienceCandidate fromOrder2 = FiefAudiencePolicy.SelectCandidate(order2);

         fromOrder1.HeroId.Should().Be(fromOrder2.HeroId);
         fromOrder1.HeroId.Should().Be("lord_a"); // pins the actual tie-break: ordinal StringId order
      }

      // A null list is a candidate-free scan, not a caller error: must degrade to "no offer", never throw.
      [Test]
      public void GIVEN_a_null_candidate_list_WHEN_selecting_THEN_it_is_a_safe_null()
         => FiefAudiencePolicy.SelectCandidate(null).Should().BeNull();

      // ── FiefAudienceCandidate.Detail ──────────────────────────────────────────

      // SelectCandidate's tie-break reads HeroId only; Detail must ride along untouched on the winner so the
      // caller (FiefAudienceBehavior) can still voice the specific grievance/kindness the candidate carried.
      [Test]
      public void GIVEN_a_winning_candidate_with_a_detail_WHEN_selecting_THEN_the_detail_survives()
      {
         var candidates = new List<FiefAudienceCandidate> {
            new FiefAudienceCandidate("lord_a", InterceptionTrigger.Grudge, "you delivered me to my enemy")
         };

         FiefAudiencePolicy.SelectCandidate(candidates)?.Detail.Should().Be("you delivered me to my enemy");
      }

      #region private

      private static InterceptionFacts TerritorialFacts()
         => new() {
            FactionHostileToPlayer = true,
            PlayerInOwnTerritory = true,
            PlayerNotoriety = InterceptionPolicy.NotorietyThreshold
         };

      private static InterceptionFacts HeardOfDeedFacts()
         => new() {HeardRecentPlayerDeed = true};

      private static InterceptionFacts GrudgeFacts()
         => new() {HoldsLiveGrudgeAgainstPlayer = true};

      private static InterceptionFacts AppreciationFacts()
         => new() {HoldsSignificantAppreciation = true};

      private static InterceptionFacts NemesisBountyFacts()
         => new() {HasNemesisBountyToOffer = true};

      #endregion
   }
}
