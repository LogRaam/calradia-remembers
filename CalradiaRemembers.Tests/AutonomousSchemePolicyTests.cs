// Code written by Gabriel Mailhot, 07/08/2026.
// AutonomousSchemePolicy is the FRONT of the Living World feud chain: it decides which lords may plot against
// which rivals on their OWN initiative, and how often. It feeds SchemeStore's weekly launcher, whose plots then
// resolve through Inc 1's FeudRelationPolicy (souring real relations so vanilla can escalate to war on its own).
// These tests pin the eligibility/budget/cooldown DECISIONS only; they assert nothing about war, which is
// vanilla's downstream call. Get these wrong and the world either never plots (dead world), plots against people
// no lord would ever move on (the player, a friend, one's own blood, a ruler), breaking the "grounded" promise,
// or plot-storms (the cap/cooldown are the whole throttle between "alive" and "a flood").

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AutonomousSchemePolicyTests
   {
      // ── budget / anti-spam ────────────────────────────────────────────────

      // The global cap is the primary throttle keeping autonomous intrigue ambient rather than a plot-storm.
      // Below the cap a fresh plot may launch; at (or above) it, none may, or the world would fill with feuds and
      // the feature would read as noise instead of the occasional grounded rivalry the design calls for.
      [Test]
      public void GIVEN_room_under_the_cap_WHEN_checking_the_budget_THEN_a_launch_is_allowed()
      {
         AutonomousSchemePolicy.BudgetAllowsLaunch(AutonomousSchemePolicy.MaxActiveAutonomousSchemes - 1).Should().BeTrue();
      }

      // The cap boundary is exclusive: exactly MaxActiveAutonomousSchemes already afoot means no new one. Pins the
      // edge so a later refactor can't quietly turn "< cap" into "<= cap" and let one extra plot always slip in.
      [Test]
      public void GIVEN_the_cap_is_reached_WHEN_checking_the_budget_THEN_no_launch_is_allowed()
      {
         AutonomousSchemePolicy.BudgetAllowsLaunch(AutonomousSchemePolicy.MaxActiveAutonomousSchemes).Should().BeFalse();
         AutonomousSchemePolicy.BudgetAllowsLaunch(AutonomousSchemePolicy.MaxActiveAutonomousSchemes + 1).Should().BeFalse();
      }

      // A lord who has never launched an autonomous plot (null last-day) has no cooldown to serve: the very first
      // plot must be able to fire, or a fresh campaign would sit inert until some arbitrary later day.
      [Test]
      public void GIVEN_a_lord_who_never_plotted_WHEN_checking_the_cooldown_THEN_it_has_elapsed()
      {
         AutonomousSchemePolicy.InstigatorCooldownElapsed(null, 100).Should().BeTrue();
      }

      // The per-instigator cooldown is what stops ONE lord spamming plots. Just before the window elapses the same
      // schemer must be refused; at exactly the window it may plot again. Pins both sides of the boundary so a lord
      // can neither re-plot early (spam) nor be locked out forever (the world going quiet).
      [Test]
      public void GIVEN_a_lord_who_recently_plotted_WHEN_checking_the_cooldown_THEN_it_holds_until_the_window_elapses()
      {
         int last = 100;

         AutonomousSchemePolicy.InstigatorCooldownElapsed(last, last + AutonomousSchemePolicy.InstigatorCooldownDays - 1).Should().BeFalse();
         AutonomousSchemePolicy.InstigatorCooldownElapsed(last, last + AutonomousSchemePolicy.InstigatorCooldownDays).Should().BeTrue();
      }

      // ── instigator eligibility ────────────────────────────────────────────

      // A living, free lord of any house but the player's is the ordinary schemer. Pins the "yes" case as the
      // control against which every exclusion below is judged.
      [Test]
      public void GIVEN_a_free_living_lord_of_another_house_WHEN_checking_eligibility_THEN_they_may_instigate()
      {
         AutonomousSchemePolicy.IsEligibleInstigator(new AutonomousSchemePolicy.InstigatorFacts {IsAlive = true, IsLord = true, IsPrisoner = false, IsPlayer = false, IsPlayerClan = false})
                               .Should().BeTrue();
      }

      // The player is NEVER an autonomous instigator: they only ever plot through the existing player-driven path
      // (pledge_against / scheme_assist). If this leaked, the world would launch plots "as the player" behind their
      // back, and an exposed one could implicate them in a scheme they never chose: the exact thing the whole
      // design forbids.
      [Test]
      public void GIVEN_the_player_WHEN_checking_eligibility_THEN_they_are_never_an_autonomous_instigator()
      {
         AutonomousSchemePolicy.IsEligibleInstigator(new AutonomousSchemePolicy.InstigatorFacts {IsAlive = true, IsLord = true, IsPrisoner = false, IsPlayer = true, IsPlayerClan = false})
                               .Should().BeFalse();
      }

      // The player's own CLAN never plots on its own either: a companion or family lord acting autonomously would
      // be the player's house scheming without the player's say. Only OTHER houses generate independent intrigue.
      [Test]
      public void GIVEN_a_member_of_the_players_clan_WHEN_checking_eligibility_THEN_they_are_never_an_autonomous_instigator()
      {
         AutonomousSchemePolicy.IsEligibleInstigator(new AutonomousSchemePolicy.InstigatorFacts {IsAlive = true, IsLord = true, IsPrisoner = false, IsPlayer = false, IsPlayerClan = true})
                               .Should().BeFalse();
      }

      // A prisoner cannot orchestrate a court plot from a cell, and a non-lord commoner has no standing to; both
      // mirror ResolvePledgeRivalName's own gates. Without these, a captured or common hero could still spawn feuds.
      [Test]
      public void GIVEN_a_prisoner_or_a_non_lord_WHEN_checking_eligibility_THEN_they_may_not_instigate()
      {
         AutonomousSchemePolicy.IsEligibleInstigator(new AutonomousSchemePolicy.InstigatorFacts {IsAlive = true, IsLord = true, IsPrisoner = true, IsPlayer = false, IsPlayerClan = false}).Should().BeFalse();
         AutonomousSchemePolicy.IsEligibleInstigator(new AutonomousSchemePolicy.InstigatorFacts {IsAlive = true, IsLord = false, IsPrisoner = false, IsPlayer = false, IsPlayerClan = false}).Should().BeFalse();
      }

      // ── grounded target ───────────────────────────────────────────────────

      // The core "grounded" promise: a plot only springs from genuine enmity. A rival at or below the rivalry
      // threshold, not excluded, is a valid target; the threshold matches the game's own "bitter enemy" band so the
      // player could already read the rivalry elsewhere. This is the control for the exclusion tests below.
      [Test]
      public void GIVEN_a_bitter_rival_who_is_no_ones_kin_WHEN_checking_the_target_THEN_they_are_grounded()
      {
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = AutonomousSchemePolicy.RivalryThreshold, IsKin = false, IsPlayerSide = false, IsFactionLeader = false})
                               .Should().BeTrue();
      }

      // A friend (or merely a neutral acquaintance) is NEVER plotted against: only real enmity qualifies. Because
      // the threshold is negative, any positive relation is rejected outright, so "a friend is never targeted" falls
      // straight out of the grounding rule. Guards against a plot springing from a warm or indifferent bond.
      [Test]
      public void GIVEN_a_friend_or_a_neutral_lord_WHEN_checking_the_target_THEN_they_are_never_grounded()
      {
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = 40, IsKin = false, IsPlayerSide = false, IsFactionLeader = false}).Should().BeFalse();
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = 0, IsKin = false, IsPlayerSide = false, IsFactionLeader = false}).Should().BeFalse();
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = AutonomousSchemePolicy.RivalryThreshold + 1, IsKin = false, IsPlayerSide = false, IsFactionLeader = false}).Should().BeFalse();
      }

      // Close kin are never a target no matter how bitter the relation: nobody autonomously plots against their own
      // spouse, child, parent, or sibling on the world's initiative (mirrors SelectSwayTarget / SelectPledgeTarget).
      // Without this, a feuding family member could become the victim of a court scheme, which the design forbids.
      [Test]
      public void GIVEN_close_kin_however_hated_WHEN_checking_the_target_THEN_they_are_never_grounded()
      {
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = -80, IsKin = true, IsPlayerSide = false, IsFactionLeader = false}).Should().BeFalse();
      }

      // The player and the player's clan are never the VICTIM of an autonomous plot: hostility toward the player is
      // the player's own story to drive, not something the world spawns behind their back. Even a deeply hostile
      // relation must not make the player (or their house) a grounded target.
      [Test]
      public void GIVEN_the_player_side_however_hated_WHEN_checking_the_target_THEN_they_are_never_grounded()
      {
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = -80, IsKin = false, IsPlayerSide = true, IsFactionLeader = false}).Should().BeFalse();
      }

      // A faction leader is never a target: moving against a ruler is a war-scale act reserved for the leader-only
      // war-decision system, never a lord's private feud (the exact exclusion pledge_against applies). Guards the
      // feature from quietly turning into an assassination-of-kings generator.
      [Test]
      public void GIVEN_a_faction_leader_however_hated_WHEN_checking_the_target_THEN_they_are_never_grounded()
      {
         AutonomousSchemePolicy.IsGroundedTarget(new AutonomousSchemePolicy.SchemeTargetCandidate {Id = "rival", Relation = -80, IsKin = false, IsPlayerSide = false, IsFactionLeader = true}).Should().BeFalse();
      }

      // ── target selection ──────────────────────────────────────────────────

      // When several genuine rivals exist, the plot springs from the BITTEREST first: the deepest enmity is the most
      // believable cause. Pins the ordering so a retune can't quietly start plotting against the mildest rival.
      [Test]
      public void GIVEN_several_rivals_WHEN_selecting_a_target_THEN_the_deepest_enmity_is_chosen()
      {
         var candidates = new List<AutonomousSchemePolicy.SchemeTargetCandidate> {
            new() {Id = "mild", Relation = -30, IsKin = false, IsPlayerSide = false, IsFactionLeader = false},
            new() {Id = "worst", Relation = -70, IsKin = false, IsPlayerSide = false, IsFactionLeader = false},
            new() {Id = "middling", Relation = -45, IsKin = false, IsPlayerSide = false, IsFactionLeader = false}
         };

         AutonomousSchemePolicy.SelectAutonomousTarget(candidates).Should().Be("worst");
      }

      // Selection applies the SAME exclusions as the grounding rule: the single most-hated candidate here is the
      // player, kin, and a faction leader in turn, yet the pick must skip them for the lesser-but-eligible rival.
      // Without this, the "deepest enmity first" ordering could route a plot straight onto a forbidden target.
      [Test]
      public void GIVEN_the_bitterest_candidates_are_all_excluded_WHEN_selecting_a_target_THEN_the_eligible_rival_is_chosen()
      {
         var candidates = new List<AutonomousSchemePolicy.SchemeTargetCandidate> {
            new() {Id = "player", Relation = -90, IsKin = false, IsPlayerSide = true, IsFactionLeader = false},
            new() {Id = "kin", Relation = -85, IsKin = true, IsPlayerSide = false, IsFactionLeader = false},
            new() {Id = "king", Relation = -80, IsKin = false, IsPlayerSide = false, IsFactionLeader = true},
            new() {Id = "rival", Relation = -35, IsKin = false, IsPlayerSide = false, IsFactionLeader = false}
         };

         AutonomousSchemePolicy.SelectAutonomousTarget(candidates).Should().Be("rival");
      }

      // When no candidate clears the grounding bar (an amicable court, or every rival excluded), NO plot is picked:
      // the generator must return null rather than forcing the least-friendly face into a scheme with no real cause.
      [Test]
      public void GIVEN_no_genuine_rival_WHEN_selecting_a_target_THEN_nothing_is_chosen()
      {
         var candidates = new List<AutonomousSchemePolicy.SchemeTargetCandidate> {
            new() {Id = "friend", Relation = 25, IsKin = false, IsPlayerSide = false, IsFactionLeader = false},
            new() {Id = "neutral", Relation = 0, IsKin = false, IsPlayerSide = false, IsFactionLeader = false}
         };

         AutonomousSchemePolicy.SelectAutonomousTarget(candidates).Should().BeNull();
         AutonomousSchemePolicy.SelectAutonomousTarget(new List<AutonomousSchemePolicy.SchemeTargetCandidate>()).Should().BeNull();
      }
   }
}
