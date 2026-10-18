// Code written by Gabriel Mailhot, 17/10/2026.
// ClanRegardListPolicy sorts the other houses into at odds and of affinity on a character's encyclopedia page. Gabriel
// (17/10/2026), question 1 of NPC_REGARD.md: "les deux": a house is listed by its leader's regard, and a house of a
// realm at war is marked. If this is wrong, a house whose lord had the page's lord's brother executed reads as a friend,
// a war hides behind a friendly number, or a secret reason leaks through the house list.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ClanRegardListPolicyTests
   {
      // STAKE: what really happened between the two lords decides the house, with its reason, over a friendly number.
      [Test]
      public void GIVEN_a_house_whose_lord_wronged_ours_WHEN_listed_THEN_it_is_at_odds_with_the_reason()
      {
         var lists = ClanRegardListPolicy.Lists(new[] {
            new ClanRegardCandidate {Name = "dey Meroc", Derived = PairRegardVerdict.Enemy, DerivedReason = "had his brother executed", VanillaRelation = 40}
         });

         lists.AtOdds.Should().Equal("dey Meroc: had his brother executed");
         lists.Affinity.Should().BeEmpty();
      }

      // STAKE: Gabriel's answer: listed by regard, and a war is marked beside it, never hidden.
      [Test]
      public void GIVEN_a_friendly_house_of_a_realm_at_war_WHEN_listed_THEN_it_is_of_affinity_and_marked_at_war()
      {
         var lists = ClanRegardListPolicy.Lists(new[] {new ClanRegardCandidate {Name = "fen Gael", VanillaRelation = 30, AtWar = true}});

         lists.Affinity.Should().ContainSingle().Which.Should().EndWith("(at war)");
      }

      // STAKE: a house is not listed for a reason the player may not read.
      [Test]
      public void GIVEN_a_derived_enemy_with_a_secret_reason_WHEN_listed_THEN_the_house_is_nowhere()
      {
         var lists = ClanRegardListPolicy.Lists(new[] {new ClanRegardCandidate {Name = "dey Tihr", Derived = PairRegardVerdict.Enemy, VanillaRelation = 40}});

         lists.AtOdds.Should().BeEmpty();
         lists.Affinity.Should().BeEmpty();
      }

      // STAKE: houses with nothing between their lords, neither friendly nor hostile, fill no list.
      [Test]
      public void GIVEN_a_house_of_neutral_standing_WHEN_listed_THEN_it_is_on_neither_list()
      {
         var lists = ClanRegardListPolicy.Lists(new[] {new ClanRegardCandidate {Name = "neutral", VanillaRelation = 0}});

         lists.AtOdds.Should().BeEmpty();
         lists.Affinity.Should().BeEmpty();
      }

      // STAKE: the lists stay short (six each, design 8.2), the houses with a story first.
      [Test]
      public void GIVEN_many_hostile_houses_WHEN_listed_THEN_six_at_most_and_the_derived_one_first()
      {
         var houses = new System.Collections.Generic.List<ClanRegardCandidate>();
         for (int i = 0; i < 9; i++) houses.Add(new ClanRegardCandidate {Name = "h" + i, VanillaRelation = -50});
         houses.Add(new ClanRegardCandidate {Name = "story", Derived = PairRegardVerdict.Enemy, DerivedReason = "a feud"});

         var lists = ClanRegardListPolicy.Lists(houses);

         lists.AtOdds.Should().HaveCount(ClanRegardListPolicy.MaxPerList);
         lists.AtOdds[0].Should().StartWith("story:");
      }
   }
}
