// Code written by Gabriel Mailhot, 17/10/2026.
// ClanRegardListPolicy sorts the other houses into at odds and of affinity on a character's encyclopedia page. Gabriel
// (17/10/2026), question 1 of NPC_REGARD.md: "les deux": a house is listed by its leader's regard, and a house of a
// realm at war is marked. If this is wrong, a house whose lord had the page's lord's brother executed reads as a friend,
// a war hides behind a friendly number, or a secret reason leaks through the house list. tashmetu (18/10/2026, with
// screenshots): on a lord with many ties the list was one dense block, the same "resentment between their lords" five
// or six times, and a shared past said twice; so one line per reason, and a lord already explained is named instead.

#region

using System.Linq;
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

         lists.AtOdds.Should().Equal("had his brother executed: dey Meroc");
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

         lists.AtOdds.Should().Equal("a feud: story", "resentment between their lords: h0, h1, h2, h3, h4");
      }

      // STAKE: tashmetu's Halthdar: five houses of the same resentment read as one line, not five repeats, and a house
      // at war keeps its mark beside its own name.
      [Test]
      public void GIVEN_houses_sharing_a_reason_WHEN_listed_THEN_one_line_names_them_all_and_the_war_stays_on_its_house()
      {
         var lists = ClanRegardListPolicy.Lists(new[] {
            new ClanRegardCandidate {Name = "Osticos", VanillaRelation = -50},
            new ClanRegardCandidate {Name = "dey Sargot-Meroc", VanillaRelation = -50, AtWar = true},
            new ClanRegardCandidate {Name = "Kjolding", VanillaRelation = 50}
         });

         lists.AtOdds.Should().Equal("resentment between their lords: Osticos, dey Sargot-Meroc (at war)");
         lists.Affinity.Should().ContainSingle().Which.Should().EndWith(": Kjolding");
      }

      // STAKE: tashmetu: the same shared past showed under Why and again under the house; a house whose lord is already
      // explained is named through him.
      [Test]
      public void GIVEN_a_house_whose_lord_is_explained_under_why_WHEN_listed_THEN_it_is_named_through_him_without_the_reason_again()
      {
         var lists = ClanRegardListPolicy.Lists(new[] {
            new ClanRegardCandidate {Name = "Kjolding", VanillaRelation = 50},
            new ClanRegardCandidate {Name = "Skylfing", Derived = PairRegardVerdict.Friend, DerivedReason = "rode together against raiders", Through = "Hertogi Gornlautir"}
         });

         lists.Affinity.Should().Equal("deep trust between their lords: Kjolding; Skylfing, through Hertogi Gornlautir");
      }

      // STAKE: Gabriel (22/10/2026): the houses become rows with each house's banner, one row per reason, so a reason five
      // houses share is written once and its five banners sit side by side; affinity (pale green) before odds (red).
      [Test]
      public void GIVEN_houses_of_both_sides_WHEN_grouped_for_rows_THEN_one_row_per_reason_with_its_houses_and_affinity_first()
      {
         var groups = ClanRegardListPolicy.Groups(new[] {
            new ClanRegardCandidate {Name = "Osticos", Id = "clan_o", VanillaRelation = -50},
            new ClanRegardCandidate {Name = "dey Sargot-Meroc", Id = "clan_s", VanillaRelation = -50, AtWar = true},
            new ClanRegardCandidate {Name = "Kjolding", Id = "clan_k", VanillaRelation = 50}
         });

         groups.Should().HaveCount(2);
         groups[0].Affinity.Should().BeTrue();
         groups[0].Houses.Should().ContainSingle().Which.Id.Should().Be("clan_k");
         groups[1].Affinity.Should().BeFalse();
         groups[1].Reason.Should().Be("resentment between their lords");
         groups[1].Houses.Should().HaveCount(2);
      }

      // STAKE: the war mark stays on the house it belongs to, under its own banner, not on the whole row.
      [Test]
      public void GIVEN_one_house_at_war_among_a_reason_WHEN_grouped_for_rows_THEN_only_that_house_is_marked_at_war()
      {
         var groups = ClanRegardListPolicy.Groups(new[] {
            new ClanRegardCandidate {Name = "Osticos", Id = "clan_o", VanillaRelation = -50},
            new ClanRegardCandidate {Name = "dey Sargot-Meroc", Id = "clan_s", VanillaRelation = -50, AtWar = true}
         });

         groups[0].Houses.Should().Contain(h => h.Id == "clan_s" && h.AtWar).And.Contain(h => h.Id == "clan_o" && !h.AtWar);
      }

      // STAKE: a house whose lord is explained above has no reason of its own to repeat: it gets a row of its side,
      // named through its lord, and keeps its colour.
      [Test]
      public void GIVEN_a_house_named_through_its_lord_WHEN_grouped_for_rows_THEN_it_is_in_a_row_of_its_side_without_a_reason()
      {
         var groups = ClanRegardListPolicy.Groups(new[] {
            new ClanRegardCandidate {Name = "Skylfing", Id = "clan_sk", Derived = PairRegardVerdict.Enemy, DerivedReason = "burned his hall", Through = "Hertogi"}
         });

         groups.Should().ContainSingle();
         groups[0].Affinity.Should().BeFalse();
         groups[0].Reason.Should().BeNull();
         groups[0].Houses.Should().ContainSingle().Which.Through.Should().Be("Hertogi");
      }

      // STAKE: the rows and the old text list are the same houses: the cap of six per side and what is left out hold.
      [Test]
      public void GIVEN_more_than_six_houses_on_a_side_WHEN_grouped_for_rows_THEN_the_six_strongest_are_kept()
      {
         var many = new ClanRegardCandidate[8];
         for (int i = 0; i < many.Length; i++) many[i] = new ClanRegardCandidate {Name = "h" + i, Id = "c" + i, VanillaRelation = -40 - i};

         ClanRegardListPolicy.Groups(many).SelectMany(g => g.Houses).Should().HaveCount(ClanRegardListPolicy.MaxPerList)
                             .And.NotContain(h => h.Id == "c0" || h.Id == "c1");
      }
   }
}
