// Code written by Gabriel Mailhot, 02/07/2026.
// Documents the exact wording of the companion-errand quest-journal mirror: one behaviour per phase /
// closing outcome, plus the per-errand-type verb used on the Outbound line.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionErrandJournalTextTests
   {
      [Test]
      public void GIVEN_a_companion_name_WHEN_building_the_title_THEN_it_reads_Errand_colon_name()
      {
         //Arrange/Act
         string title = CompanionErrandJournalText.BuildTitle("Martira");

         //Assert
         title.Should().Be("Errand: Martira");
      }

      [Test]
      public void GIVEN_no_companion_name_WHEN_building_the_title_THEN_it_falls_back_to_your_companion()
      {
         //Arrange/Act
         string title = CompanionErrandJournalText.BuildTitle(null);

         //Assert
         title.Should().Be("Errand: Your companion");
      }

      // ── Outbound, per errand-type verb ──────────────────────────────────────

      [Test]
      public void GIVEN_a_gather_news_errand_WHEN_outbound_THEN_the_line_says_bring_back_news()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.GatherNews, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to bring back news.");
      }

      [Test]
      public void GIVEN_a_spy_errand_WHEN_outbound_THEN_the_line_says_scout_it()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Spy, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to scout it.");
      }

      [Test]
      public void GIVEN_a_steal_errand_WHEN_outbound_THEN_the_line_says_steal_something_worth_bringing_home()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Steal, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to steal something worth bringing home.");
      }

      [Test]
      public void GIVEN_a_barter_errand_WHEN_outbound_THEN_the_line_says_trade_for_a_profit()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Barter, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to trade for a profit.");
      }

      [Test]
      public void GIVEN_an_envoy_errand_WHEN_outbound_THEN_the_line_says_carry_your_word_as_an_envoy()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Envoy, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to carry your word as an envoy.");
      }

      [Test]
      public void GIVEN_no_destination_name_WHEN_outbound_THEN_it_falls_back_to_a_town()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.GatherNews, CompanionErrandPhase.Outbound, "Martira", null);

         //Assert
         line.Should().Be("Martira rides for a town to bring back news.");
      }

      // ── Later phases, same wording regardless of errand type ─────────────────

      [Test]
      public void GIVEN_any_errand_WHEN_lingering_THEN_the_line_reports_arrival()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Steal, CompanionErrandPhase.Lingering, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira has reached Poros.");
      }

      [Test]
      public void GIVEN_any_errand_WHEN_returning_THEN_the_line_reports_heading_back()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Barter, CompanionErrandPhase.Returning, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira is on the way back to you.");
      }

      [Test]
      public void GIVEN_any_errand_WHEN_awaiting_reunion_THEN_the_line_reports_the_captivity_wait()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Spy, CompanionErrandPhase.AwaitingReunion, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira was waylaid and taken captive; the errand waits until they are freed.");
      }

      // ── Closing lines ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_completed_errand_WHEN_closing_as_returned_THEN_the_line_reports_the_homecoming()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildReturnedLine("Martira");

         //Assert
         line.Should().Be("Martira has returned and reported in.");
      }

      [Test]
      public void GIVEN_a_recall_with_a_reason_WHEN_closing_as_recalled_THEN_the_reason_is_appended_in_parentheses()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildRecalledLine("Martira", "as your letter ordered");

         //Assert
         line.Should().Be("Martira abandoned the errand and returned to you (as your letter ordered).");
      }

      [Test]
      public void GIVEN_a_recall_with_no_reason_WHEN_closing_as_recalled_THEN_no_parenthetical_is_added()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildRecalledLine("Martira", null);

         //Assert
         line.Should().Be("Martira abandoned the errand and returned to you.");
      }
   }
}
