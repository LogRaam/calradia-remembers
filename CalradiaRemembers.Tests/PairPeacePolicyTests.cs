// Code written by Gabriel Mailhot, 07/11/2026.
// PairPeacePolicy decides which wrongs one character's peace with another lays to rest. kurushimi00 (Nexus, 06/11/2026):
// Ira and Adram, enemies since a tourney 8 years ago, made peace in the player's conversations, and the encyclopedia
// still listed them as enemies "from the tourney". Gabriel (07/11/2026) ruled for personal grudges, one per direction:
// each pardons in their own voice, and the past stays written while only its weight goes out. That a slain kinsman is
// never pardoned in a conversation was proposed in the design discussion and built under that go. If this is wrong, a mediated peace changes nothing again, one character's pardon speaks for
// the other, or a lord forgives the man who had his brother executed because a model wrote a warm line.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PairPeacePolicyTests
   {
      private const int Today = 3000;

      private static PairRegardTrace Trace(int weight, string line) => new() {Weight = weight, Line = line, KnownToPlayer = true};

      // STAKE (kurushimi00): the tourney rivalry is exactly what a mediated peace should lay to rest.
      [Test]
      public void GIVEN_a_tourney_rivalry_eight_years_old_WHEN_its_holder_made_peace_since_THEN_it_weighs_nothing()
      {
         PairPeacePolicy.Cancels(new PairWrongFacts {Kind = PairWrongKind.SharedPastGrievance, Day = Today - 8 * 84, PeaceDay = Today})
                        .Should().BeTrue();
      }

      // STAKE (design discussion, 06/11, built under Gabriel's go): a brother slain or executed is not undone by a scene's warm words.
      [Test]
      public void GIVEN_a_kinsman_slain_or_executed_WHEN_his_kin_made_peace_THEN_the_killing_still_weighs()
      {
         PairPeacePolicy.Cancels(new PairWrongFacts {Kind = PairWrongKind.KinKilled, Day = Today - 10, PeaceDay = Today}).Should().BeFalse();
         PairPeacePolicy.Cancels(new PairWrongFacts {Kind = PairWrongKind.KinExecuted, Day = Today - 10, PeaceDay = Today}).Should().BeFalse();
      }

      // STAKE: a grudge its holder said only a deed (or nothing) could close stays; one made up in words goes.
      [Test]
      public void GIVEN_grudges_by_what_closes_them_WHEN_their_holder_made_peace_THEN_only_the_ones_words_can_close_go()
      {
         PairPeacePolicy.Pardonable(PairWrongKind.Grudge, GrudgeResolutionKind.Amends).Should().BeTrue();
         PairPeacePolicy.Pardonable(PairWrongKind.Grudge, GrudgeResolutionKind.Time).Should().BeTrue();
         PairPeacePolicy.Pardonable(PairWrongKind.Grudge, GrudgeResolutionKind.Deed).Should().BeFalse();
         PairPeacePolicy.Pardonable(PairWrongKind.Grudge, GrudgeResolutionKind.Unforgivable).Should().BeFalse();
      }

      // STAKE: a peace covers what came before it; a new wrong after it counts again, or a pardon would be a lifetime pass.
      [Test]
      public void GIVEN_a_wrong_done_after_the_peace_WHEN_weighed_THEN_it_counts()
      {
         PairPeacePolicy.Cancels(new PairWrongFacts {Kind = PairWrongKind.BeatenInBattle, Day = Today + 5, PeaceDay = Today}).Should().BeFalse();
      }

      // STAKE: without a peace nothing is cancelled.
      [Test]
      public void GIVEN_no_peace_made_WHEN_a_wrong_is_weighed_THEN_it_counts()
      {
         PairPeacePolicy.Cancels(new PairWrongFacts {Kind = PairWrongKind.SharedPastGrievance, Day = Today - 100}).Should().BeFalse();
      }

      // STAKE (kurushimi00): once Ira has made peace and holds nothing else, she is no enemy of Adram, and the page must
      // not fall back on the game's own list to call them enemies again.
      [Test]
      public void GIVEN_a_holder_who_made_peace_and_holds_nothing_since_WHEN_regard_is_read_THEN_they_are_reconciled_not_enemies()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {MadePeace = true});

         r.Verdict.Should().Be(PairRegardVerdict.Neither);
         r.Reconciled.Should().BeTrue();
      }

      // STAKE (Gabriel's ruling): the pardon is personal. Adram, who never made peace, still holds the rivalry: a truce
      // only one of them has kept.
      [Test]
      public void GIVEN_only_one_side_made_peace_WHEN_the_other_side_is_read_THEN_the_other_still_holds_the_rivalry()
      {
         PairRegard adram = PairRegardPolicy.Assess(new PairRegardFacts {Traces = new[] {Trace(-20, "met in the lists, and neither has forgotten it")}});

         adram.Verdict.Should().Be(PairRegardVerdict.Enemy);
         adram.Reconciled.Should().BeFalse();
      }

      // STAKE: a peace does not cover a slain kinsman, so the holder who made peace is still an enemy and not "reconciled".
      [Test]
      public void GIVEN_a_peace_made_over_a_slain_kinsman_WHEN_regard_is_read_THEN_the_holder_is_still_an_enemy()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {MadePeace = true, Traces = new[] {Trace(PairRegardPolicy.KinExecuted(), "had your brother executed")}});

         r.Verdict.Should().Be(PairRegardVerdict.Enemy);
         r.Reconciled.Should().BeFalse();
      }

      // STAKE: a model repeating the act every line must not stack peace memories.
      [Test]
      public void GIVEN_a_peace_made_a_moment_ago_WHEN_made_again_THEN_it_is_a_repeat()
      {
         PairPeacePolicy.IsRepeat(Today - 1, Today).Should().BeTrue();
         PairPeacePolicy.IsRepeat(Today - PairPeacePolicy.RepeatDays, Today).Should().BeFalse();
         PairPeacePolicy.IsRepeat(null, Today).Should().BeFalse();
      }
   }
}
