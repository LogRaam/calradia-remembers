// Code written by Gabriel Mailhot, 17/10/2026.
// SharedPastPolicy draws what two lords shared before the campaign (docs/design/NPC_REGARD.md section 7). Gabriel's
// rulings: drawn once and never re-rolled, a good share get nothing, written into the world's long-term memory, and a
// warm vanilla relation forbids a quarrel (the reverse too). If this is wrong, a lord's past changes between two
// openings of his page, every pair of lords has a story, a past is set in years the player saw, or two friends by the
// vanilla number are given an old feud.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SharedPastPolicyTests
   {
      private const int Year = 84;
      private const int Start = 1084 * Year;

      private static SharedPastPairFacts Pair(string a, string b, int compatibility = 0, int vanilla = 0, int ageOfYounger = 40) => new() {
         A = new SharedPastSide {Id = a, Name = a, BirthDay = Start - 50 * Year, Valor = 1, Generosity = 0, IsLord = true},
         B = new SharedPastSide {Id = b, Name = b, BirthDay = Start - ageOfYounger * Year, Valor = 1, Generosity = 1, IsLord = true},
         Neighbours = true, SameRealm = true, SameCulture = true, Compatibility = compatibility, VanillaRelation = vanilla,
         PlaceName = "Pravend", CampaignStartDay = Start, DaysInYear = Year, ComesOfAgeYears = 18
      };

      // STAKE: Gabriel's "drawn once, never re-rolled": the same pair always draws the same past.
      [Test]
      public void GIVEN_the_same_pair_WHEN_drawn_twice_THEN_the_same_past_comes_out()
      {
         for (int i = 0; i < 50; i++)
         {
            SharedPastDraw first = SharedPastPolicy.Draw(Pair("lord_" + i, "lord_x"));
            SharedPastDraw again = SharedPastPolicy.Draw(Pair("lord_" + i, "lord_x"));
            (again?.Kind).Should().Be(first?.Kind);
            (again?.Day).Should().Be(first?.Day);
         }
      }

      // STAKE: both must have been adults then: a lord who came of age after the campaign began shared nothing before it.
      [Test]
      public void GIVEN_a_lord_who_came_of_age_after_the_campaign_began_WHEN_a_past_is_drawn_THEN_there_is_none()
      {
         Enumerable.Range(0, 200).Select(i => SharedPastPolicy.Draw(Pair("a" + i, "b", ageOfYounger: 17))).Should().OnlyContain(d => d == null);
      }

      // STAKE: the player saw everything since the start; a past is always placed before it, both of age.
      [Test]
      public void GIVEN_any_drawn_past_WHEN_dated_THEN_it_falls_before_the_campaign_began()
      {
         var draws = Enumerable.Range(0, 500).Select(i => SharedPastPolicy.Draw(Pair("a" + i, "b", ageOfYounger: 22))).Where(d => d != null).ToList();

         draws.Should().NotBeEmpty();
         draws.Should().OnlyContain(d => d.Day < Start && d.Day >= Start - 4 * Year); // the younger came of age 4 years before
      }

      // STAKE: "a good share get nothing": most pairs have no shared past.
      [Test]
      public void GIVEN_a_thousand_unrelated_pairs_WHEN_drawn_THEN_most_have_no_shared_past()
      {
         int drawn = Enumerable.Range(0, 1000).Count(i => SharedPastPolicy.Draw(Pair("a" + i, "b" + i)) != null);

         drawn.Should().BeInRange(250, 500);
      }

      // STAKE: two lords of opposed honour never draw an old friendship.
      [Test]
      public void GIVEN_two_lords_with_opposed_traits_WHEN_a_past_is_drawn_THEN_it_is_never_an_old_friendship()
      {
         Enumerable.Range(0, 1000).Select(i => SharedPastPolicy.Draw(Pair("a" + i, "b", compatibility: -6)))
                   .Should().NotContain(d => d != null && d.Kind == SharedPastKind.OldFriendship);
      }

      // STAKE: Gabriel 17/10/2026: a pair warm by the vanilla number cannot draw a quarrel (the page would show a feud
      // beside a "Clan Relation" of 80); a bitter pair draws no friendship.
      [Test]
      public void GIVEN_a_warm_vanilla_relation_WHEN_drawn_THEN_no_grievance_and_a_bitter_one_no_bond()
      {
         Enumerable.Range(0, 1000).Select(i => SharedPastPolicy.Draw(Pair("a" + i, "b", vanilla: 80))).Where(d => d != null)
                   .Should().OnlyContain(d => SharedPastPolicy.WeightToward(d.Kind, true, 0) >= 0);
         Enumerable.Range(0, 1000).Select(i => SharedPastPolicy.Draw(Pair("a" + i, "b", vanilla: -80))).Where(d => d != null)
                   .Should().OnlyContain(d => SharedPastPolicy.WeightToward(d.Kind, true, 0) <= 0);
      }

      // STAKE: design 7.5: full weight for two years, then a grievance halves every 8 years and a bond every 15.
      [Test]
      public void GIVEN_years_since_WHEN_a_past_is_weighed_THEN_it_fades_as_designed()
      {
         SharedPastPolicy.WeightToward(SharedPastKind.BoundaryQuarrel, true, 2).Should().Be(-30);
         SharedPastPolicy.WeightToward(SharedPastKind.BoundaryQuarrel, true, 10).Should().Be(-15);
         SharedPastPolicy.WeightToward(SharedPastKind.OldFriendship, false, 10).Should().Be(35);
         SharedPastPolicy.WeightToward(SharedPastKind.Rescue, true, 0).Should().Be(45);
      }

      // STAKE: the record keeps its kind in its status, so it is weighed the same way after a save and a load.
      [Test]
      public void GIVEN_a_drawn_kind_WHEN_written_as_a_status_THEN_it_reads_back()
      {
         SharedPastPolicy.KindOf(SharedPastPolicy.StatusPrefix + SharedPastKind.Rescue).Should().Be(SharedPastKind.Rescue);
         SharedPastPolicy.KindOf("npc_captured").Should().Be(SharedPastKind.None);
      }
   }
}
