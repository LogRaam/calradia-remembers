// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0: the gate that decides whether a duel may even be OFFERED, before willingness is
// weighed. Its verdict is the bridge's law made pure: the LLM can have an NPC say "let us settle this with
// steel", but only an able peer, face to face, not bound by captivity or kinship or a recent bout, may be
// called out. Every wrong "yes" here is a duel the game cannot honour, a chat calling a commoner or a
// prisoner or someone across the map to a field that will never exist, and the whole feature reads as broken.
// The ORDER of the checks is itself load-bearing and tested here: the reason the bridge voices must be the
// truest one, so "he is dead" must win over "he is not here". The war-declaration line lives one layer up;
// this policy judges the duel alone.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelChallengePolicyTests
   {
      // The happy path exists so the rest of the pillar is reachable at all: a plain able peer, present, free,
      // unrelated, never dueled, must be challengeable. If this ever failed, no duel could be offered and every
      // other rule below would be moot.
      [Test]
      public void GIVEN_an_able_present_peer_WHEN_evaluating_THEN_the_challenge_is_eligible()
      {
         DuelChallengePolicy.Evaluate(Ready()).Should().Be(DuelEligibility.Eligible);
      }

      // The most fundamental impossibility must win over every situational one: you cannot duel the dead or the
      // bedridden, and that answer must be returned even when they are also, say, not present. If a lesser reason
      // masked this, the bridge could tell the player "he is not here" about a corpse.
      [Test]
      public void GIVEN_a_party_who_cannot_fight_WHEN_evaluating_THEN_incapable_outranks_every_other_reason()
      {
         DuelChallengeContext ctx = Ready();
         ctx.BothAbleToFight = false;
         ctx.SharePlace = false;       // a competing, lesser reason
         ctx.TargetIsAPeer = false;    // and another

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.Incapable);
      }

      // A null context is the safe refusal: an absent or malformed situation must never read as a green light to
      // start a fight. Treating it as incapable keeps a missing fact from ever spawning a duel by accident.
      [Test]
      public void GIVEN_no_context_at_all_WHEN_evaluating_THEN_it_is_treated_as_incapable()
      {
         DuelChallengePolicy.Evaluate(null).Should().Be(DuelEligibility.Incapable);
      }

      // Kin is not a field of honour: you do not duel yourself, your spouse, or your own child. Without this a
      // heated marital chat could open a real fight between a player and their own consort, which the whole
      // marriage system would read as a bug.
      [Test]
      public void GIVEN_the_target_is_self_or_family_WHEN_evaluating_THEN_it_is_refused_as_kin()
      {
         DuelChallengeContext ctx = Ready();
         ctx.IsSelfOrFamily = true;

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.SelfOrFamily);
      }

      // Captivity has its own scenes, and a prisoner cannot give real consent to a "fair" duel. This must outrank
      // the peer and presence checks (a held lord IS a present peer), or a captor could dress up an execution as
      // a duel, or a captive could challenge their way out of a cell.
      [Test]
      public void GIVEN_one_party_holds_the_other_prisoner_WHEN_evaluating_THEN_captivity_blocks_it()
      {
         DuelChallengeContext ctx = Ready();
         ctx.EitherHoldsOtherPrisoner = true;

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.InCaptivity);
      }

      // A duel is between people of standing. Calling a commoner or a servant to the field is a category error,
      // and it is also how a player could farm renown off helpless targets, so a non-peer is barred before
      // presence or cooldown are ever consulted.
      [Test]
      public void GIVEN_the_target_is_a_commoner_WHEN_evaluating_THEN_it_is_refused_as_not_a_peer()
      {
         DuelChallengeContext ctx = Ready();
         ctx.TargetIsAPeer = false;

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.NotAPeer);
      }

      // You cannot fight someone you are not standing before. This is exactly the case Gabriel flagged, an NPC
      // proposing a duel from a chat while not in town: the challenge must be refused as not-face-to-face rather
      // than silently scheduling a fight nobody can reach.
      [Test]
      public void GIVEN_the_two_are_not_in_the_same_place_WHEN_evaluating_THEN_it_is_not_face_to_face()
      {
         DuelChallengeContext ctx = Ready();
         ctx.SharePlace = false;

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.NotFaceToFace);
      }

      // The cooldown is the ONLY bar that simply passes with time, so it is checked last: a fresh duel between the
      // same two must wait, or the field becomes a daily treadmill a player farms for renown and regard swings.
      [Test]
      public void GIVEN_they_dueled_within_the_cooldown_WHEN_evaluating_THEN_it_is_too_soon()
      {
         DuelChallengeContext ctx = Ready();
         ctx.DaysSinceLastDuel = DuelChallengePolicy.CooldownDays - 1;

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.TooSoon);
      }

      // The boundary sits exactly at the cooldown: the day it elapses, a rematch is allowed again. Pinning the
      // edge stops an off-by-one from either blocking a legitimate rematch forever or letting one happen a day early.
      [Test]
      public void GIVEN_the_cooldown_has_just_elapsed_WHEN_evaluating_THEN_a_rematch_is_allowed()
      {
         DuelChallengeContext ctx = Ready();
         ctx.DaysSinceLastDuel = DuelChallengePolicy.CooldownDays;

         DuelChallengePolicy.Evaluate(ctx).Should().Be(DuelEligibility.Eligible);
      }

      // Never having dueled is not a cooldown: two strangers are free to fight at once. Without the null guard, a
      // first-ever challenge could be wrongly read as still cooling down and never open.
      [Test]
      public void GIVEN_they_have_never_dueled_WHEN_checking_the_cooldown_THEN_it_is_not_blocked()
      {
         DuelChallengePolicy.IsOnCooldown(null).Should().BeFalse();
      }

      private static DuelChallengeContext Ready() => new() {
         Challenger = DuelChallenger.Player,
         BothAbleToFight = true,
         IsSelfOrFamily = false,
         EitherHoldsOtherPrisoner = false,
         TargetIsAPeer = true,
         SharePlace = true,
         DaysSinceLastDuel = null
      };
   }
}
