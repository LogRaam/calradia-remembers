// Code written by Gabriel Mailhot, 28/09/2026.
// The balance-of-power survey (Gabriel, 28/09/2026): before building any push toward balance, measure whether one realm
// actually runs away with the map in 1.4.8, which already forms alliances against a threatening neighbour. The survey
// is the evidence the decision will rest on: if it misreads who leads, how concentrated the map is, or how often the
// world turned on the leader, a lever gets built (or refused) on a wrong picture.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.WorldAlmanac;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BalanceSurveyPolicyTests
   {
      private static SurveyRow Row(int day, string id, int towns, int castles)
         => new SurveyRow {Day = day, KingdomId = id, KingdomName = id + " Kingdom", Towns = towns, Castles = castles};

      // Share is read on fiefs, a town twice a castle: armies swing with every battle, fiefs are what a realm holds.
      [Test]
      public void GIVEN_realms_holding_towns_and_castles_WHEN_their_shares_are_read_THEN_a_town_counts_twice_a_castle()
      {
         Dictionary<string, double> shares = BalanceSurveyPolicy.Shares(new[] {Row(1, "a", 1, 0), Row(1, "b", 0, 2)});

         shares["a"].Should().BeApproximately(0.5, 1e-9);
         shares["b"].Should().BeApproximately(0.5, 1e-9);
      }

      // The concentration reads 1/n for an even map and 1 when one realm holds everything: the one number that says
      // whether the map is drifting toward a single power.
      [Test]
      public void GIVEN_an_even_map_and_a_conquered_one_WHEN_concentration_is_read_THEN_it_runs_from_one_over_n_to_one()
      {
         BalanceSurveyPolicy.Concentration(new[] {0.25, 0.25, 0.25, 0.25}).Should().BeApproximately(0.25, 1e-9);
         BalanceSurveyPolicy.Concentration(new[] {1.0, 0, 0, 0}).Should().BeApproximately(1.0, 1e-9);
      }

      // Gabriel's worry, stated as a threshold: one realm holding 40% of the fiefs, or climbing 15 points since the start.
      [TestCase(0.40, 0.30, true)]
      [TestCase(0.35, 0.15, true)]
      [TestCase(0.30, 0.20, false)]
      public void GIVEN_a_leaders_share_WHEN_judged_THEN_a_runaway_is_a_large_share_or_a_steep_climb(double atEnd, double atStart, bool runaway)
      {
         BalanceSurveyPolicy.IsRunaway(atEnd, atStart).Should().Be(runaway);
      }

      // The summary a decision rests on: who led at each end, how high the leader climbed, and whether the world
      // turned on it (wars declared on the leader, alliances formed against it).
      [Test]
      public void GIVEN_a_survey_where_one_realm_swallows_its_neighbours_WHEN_summarised_THEN_the_runaway_and_the_world_answer_are_both_read()
      {
         var rows = new List<SurveyRow> {
            Row(7, "a", 2, 2), Row(7, "b", 2, 2), Row(7, "c", 2, 2),
            Row(14, "a", 5, 4), Row(14, "b", 1, 0), Row(14, "c", 1, 1)
         };
         var events = new List<SurveyEvent> {
            new SurveyEvent {Day = 8, Kind = "war", AgainstLeader = true},
            new SurveyEvent {Day = 9, Kind = "war", AgainstLeader = false},
            new SurveyEvent {Day = 10, Kind = "alliance", AgainstLeader = true},
            new SurveyEvent {Day = 11, Kind = "call_to_war"}
         };

         BalanceSurveySummary s = BalanceSurveyPolicy.Summarize(rows, events);

         s.Weeks.Should().Be(2);
         s.LeaderAtEnd.Should().Be("a Kingdom");
         s.LeaderShareAtEnd.Should().BeApproximately(14.0 / 19.0, 1e-9);
         s.HighestLeaderDay.Should().Be(14);
         s.ConcentrationAtEnd.Should().BeGreaterThan(s.ConcentrationAtStart);
         s.Runaway.Should().BeTrue();
         s.Wars.Should().Be(2);
         s.WarsOnLeader.Should().Be(1);
         s.AlliancesAgainstLeader.Should().Be(1);
         s.CallsToWar.Should().Be(1);
      }

      // The file must survive the session and read back exactly: a survey run over a long accelerated campaign is
      // read in a later session, possibly after a crash that tore the last line.
      [Test]
      public void GIVEN_a_survey_written_to_its_file_WHEN_read_back_with_a_torn_last_line_THEN_every_whole_line_returns()
      {
         var row = new SurveyRow {Day = 21, KingdomId = "empire_w", KingdomName = "Western; Empire", PowerIndex = 81.4, Strength = 12345, Towns = 5, Castles = 7, Villages = 20, Wars = 2, Allies = 1};
         var ev = new SurveyEvent {Day = 22, Kind = "alliance", AId = "vlandia", AName = "Vlandia", BId = "battania", BName = "Battania", AgainstLeader = true};
         string[] lines = {BalanceSurveyPolicy.Header, BalanceSurveyPolicy.ToLine(row), BalanceSurveyPolicy.ToLine(ev), "K;28;sturgia;Stur"};

         (List<SurveyRow> rows, List<SurveyEvent> events) = BalanceSurveyPolicy.Parse(lines);

         rows.Should().ContainSingle();
         rows[0].KingdomName.Should().Be("Western, Empire");
         rows[0].Towns.Should().Be(5);
         rows[0].PowerIndex.Should().BeApproximately(81.4, 1e-9);
         events.Should().ContainSingle().Which.AgainstLeader.Should().BeTrue();
      }
   }
}
