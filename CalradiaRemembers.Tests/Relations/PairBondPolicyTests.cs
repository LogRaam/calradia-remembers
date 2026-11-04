// Code written by Gabriel Mailhot, 04/11/2026.
// PairBondPolicy decides how two NPCs stand with each other, personally, and whether CR believes it. tashmetu (Nexus,
// 04/11/2026): the game draws thousands of friendships and enmities at the start "between realms with nothing to do with
// each other (Derthert at -100 with Tasynor)", and CR built on them as history. Gabriel (04/11/2026): believe a relation
// only when a reason can be named; with none, a vague unease and no number. If this is wrong, a random draw starts a
// real feud or picks a lover, or a true history built in play is thrown away.

#region

using CalradiaRemembers.Logic.Relations;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Relations
{
   [TestFixture]
   public sealed class PairBondPolicyTests
   {
      // STAKE: tashmetu's Derthert and Tasynor: a -100 drawn between two far realms is not believed, so no plot or
      // rivalry news starts from it; only a vague unease is left for words.
      [Test]
      public void GIVEN_a_drawn_enmity_between_strangers_of_far_realms_WHEN_assessed_THEN_it_is_not_believed_and_leaves_a_vague_unease()
      {
         PairBond bond = PairBondPolicy.Assess(new PairBondFacts {Personal = -100});

         bond.Basis.Should().Be(PairBondBasis.Unexplained);
         bond.Points.Should().Be(0);
         bond.Echo.Should().Be(PairBondEcho.VagueUnease);
      }

      // STAKE: the advisor's catch: the game's draw set the sign from temperament, so temperament alone would excuse
      // nearly every draw. Clashing natures with no tie are still strangers.
      [Test]
      public void GIVEN_a_drawn_enmity_and_clashing_natures_but_no_tie_WHEN_assessed_THEN_temperament_alone_does_not_explain_it()
      {
         PairBondPolicy.Assess(new PairBondFacts {Personal = -60, Temperament = -15}).Basis.Should().Be(PairBondBasis.Unexplained);
      }

      // STAKE: Gabriel: a relation with a reason the two would know each other by is believed, and the reason is told.
      [Test]
      public void GIVEN_a_start_between_lords_of_one_court_WHEN_assessed_THEN_it_is_believed_with_its_reason()
      {
         PairBond bond = PairBondPolicy.Assess(new PairBondFacts {Personal = -45, SameRealm = true, Temperament = -10});

         bond.Basis.Should().Be(PairBondBasis.Known);
         bond.Points.Should().Be(-45);
         bond.Reason.Should().Be("the same court, and at odds in nature");
      }

      // STAKE: a war between their realms explains why two lords hate each other, never why they love each other.
      [Test]
      public void GIVEN_realms_at_war_WHEN_the_start_is_friendship_or_enmity_THEN_only_the_enmity_is_explained()
      {
         PairBondPolicy.Assess(new PairBondFacts {Personal = -40, RealmsAtWar = true}).Basis.Should().Be(PairBondBasis.Known);
         PairBondPolicy.Assess(new PairBondFacts {Personal = 40, RealmsAtWar = true}).Basis.Should().Be(PairBondBasis.Unexplained);
      }

      // STAKE: tashmetu: R&S seeds its own ties with reasons ("an old rival realm", however far). CR takes another mod's
      // reason as its own, so those ties are believed without CR knowing the mod.
      [Test]
      public void GIVEN_a_reason_from_another_mod_WHEN_assessed_THEN_that_reason_explains_the_start()
      {
         PairBond bond = PairBondPolicy.Assess(new PairBondFacts {Personal = -50, OutsideReason = "an old rivalry between your peoples"});

         bond.Basis.Should().Be(PairBondBasis.Known);
         bond.Reason.Should().Be("an old rivalry between your peoples");
      }

      // STAKE: history built while CR watched is real: what changed since the first change seen is believed even with
      // no tie, and only the unexplained start is set aside.
      [Test]
      public void GIVEN_a_drawn_start_and_a_change_seen_since_WHEN_assessed_THEN_only_the_change_counts()
      {
         PairBond bond = PairBondPolicy.Assess(new PairBondFacts {Personal = -70, Baseline = -50});

         bond.Basis.Should().Be(PairBondBasis.Unexplained);
         bond.Points.Should().Be(-20);
         bond.Relation.IsEnemy.Should().BeTrue();
      }

      // STAKE: a pair that started at nothing and moved in play is earned history, the whole of it.
      [Test]
      public void GIVEN_a_pair_that_started_at_nothing_WHEN_it_moved_in_play_THEN_all_of_it_is_earned()
      {
         PairBond bond = PairBondPolicy.Assess(new PairBondFacts {Personal = 35, Baseline = 0});

         bond.Basis.Should().Be(PairBondBasis.Earned);
         bond.Points.Should().Be(35);
         PairBondPolicy.Assess(new PairBondFacts()).Basis.Should().Be(PairBondBasis.None);
      }

      // STAKE: tashmetu: R&S clears and seeds "in the first hours", after the game's draw, without an event. Only the
      // first announced change marks where history begins, whatever wrote the start, and an old save starts empty.
      [Test]
      public void GIVEN_several_changes_WHEN_the_ledger_marks_them_THEN_only_the_first_sets_the_baseline_in_either_order()
      {
         var ledger = new RelationBaselineLedger();

         ledger.Saw("lord_a", "lord_b", -50).Should().BeTrue();
         ledger.Saw("lord_b", "lord_a", -70).Should().BeFalse();
         RelationBaselineLedger.Deserialize(ledger.Serialize()).BaselineOf("lord_b", "lord_a").Should().Be(-50);
         RelationBaselineLedger.Deserialize(null).BaselineOf("lord_a", "lord_b").Should().BeNull();
      }

      // STAKE: a faint draw leaves nothing to say: no vague feeling for a few points either way.
      [Test]
      public void GIVEN_a_faint_unexplained_start_WHEN_assessed_THEN_no_feeling_is_left()
      {
         PairBondPolicy.Assess(new PairBondFacts {Personal = VanillaRelation.FriendTier - 1}).Echo.Should().Be(PairBondEcho.None);
         PairBondPolicy.Assess(new PairBondFacts {Personal = 50}).Echo.Should().Be(PairBondEcho.VagueGoodwill);
      }

      // STAKE: NPCs never lie: the vague unease is told as having no known cause, and a believed start says why.
      [Test]
      public void GIVEN_a_bond_WHEN_put_in_words_THEN_the_unease_has_no_cause_and_a_known_start_says_its_reason()
      {
         PairBondText.Clause(PairBondPolicy.Assess(new PairBondFacts {Personal = -80})).Should().Contain("know no cause");
         PairBondText.Clause(PairBondPolicy.Assess(new PairBondFacts {Personal = 30, IsKin = true})).Should().Be("known to each other through kinship");
         PairBondText.Clause(PairBondPolicy.Assess(new PairBondFacts {Personal = 30, Baseline = 0})).Should().BeNull();
      }

      // STAKE: tashmetu: a younger son is not his father's feelings. The houses' standing is a sentence of its own,
      // saying it is the house's, and silent when the house stands with no one.
      [Test]
      public void GIVEN_the_houses_standing_WHEN_put_in_words_THEN_it_is_said_apart_as_the_houses()
      {
         string line = PairBondText.HousesLine("Derthert", new[] {
            new HouseStanding {House = "the Fen Gerian", Points = -40}, new HouseStanding {House = "the Dey Tihr", Points = 25},
            new HouseStanding {House = "the Neutrals", Points = 0}
         });

         line.Should().Be("Between houses: your house, under Derthert, is at odds with the Fen Gerian, and on good terms with the Dey Tihr. " +
                          "That is your house's standing, not necessarily your own feeling.");
         PairBondText.HousesLine("Derthert", new[] {new HouseStanding {House = "the Neutrals", Points = 5}}).Should().BeNull();
         PairBondText.HouseClause(-35, "Derthert", "Tasynor").Should().Contain("bitter enmity").And.Contain("not necessarily your own feeling");
         PairBondText.HouseClause(0, "Derthert", "Tasynor").Should().BeNull();
      }
   }
}
