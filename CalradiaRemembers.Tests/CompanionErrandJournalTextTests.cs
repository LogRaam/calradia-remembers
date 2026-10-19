// Code written by Gabriel Mailhot, 02/07/2026.
// Documents the exact wording of the companion-errand quest-journal mirror: one behaviour per phase /
// closing outcome, plus the per-errand-type verb used on the Outbound line.
//
// WHY IT MATTERS: CompanionErrandJournalBridge and CompanionErrandQuest read this straight into the
// vanilla quest-journal entry the player sees for a companion sent away (v1.20.4: "so you can always see
// who you sent where"). This is pure string formatting with no engine type in or out, so wrong wording
// here IS the bug the player sees, not just a symptom of one: a stale, wrong-tense, or mismatched line
// stays wrong until this file is fixed, and no other test layer would catch it.

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionErrandJournalTextTests
   {
      // The title is what the player scans in their quest list to find who is away; a wrong format breaks
      // that at-a-glance recognition (CompanionErrandQuest.Title reads this directly).
      [Test]
      public void GIVEN_a_companion_name_WHEN_building_the_title_THEN_it_reads_Errand_colon_name()
      {
         //Arrange/Act
         string title = CompanionErrandJournalText.BuildTitle("Martira");

         //Assert
         title.Should().Be("Errand: Martira");
      }

      // A missing name (companion name resolution can fail, e.g. across a save) must not leave the title
      // reading "Errand: " with a blank tail; the fallback keeps the journal entry legible either way.
      [Test]
      public void GIVEN_no_companion_name_WHEN_building_the_title_THEN_it_falls_back_to_your_companion()
      {
         //Arrange/Act
         string title = CompanionErrandJournalText.BuildTitle(null);

         //Assert
         title.Should().Be("Errand: Your companion");
      }

      // ── Outbound, per errand-type verb ──────────────────────────────────────

      // The default/fallback branch of ErrandPurpose, so this also stands in as the safety net for any
      // errand type the switch doesn't otherwise recognise. Wrong wording tells the player their companion
      // rode out to do something other than what they actually sent them to do.
      [Test]
      public void GIVEN_a_gather_news_errand_WHEN_outbound_THEN_the_line_says_bring_back_news()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.GatherNews, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to bring back news.");
      }

      // Pins the Spy verb distinct from Steal/GatherNews so a RISKY errand (capture-eligible, per
      // CompanionMissions.IsRisky) reads honestly as reconnaissance, not confused with a safe news run.
      [Test]
      public void GIVEN_a_spy_errand_WHEN_outbound_THEN_the_line_says_scout_it()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Spy, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to scout it.");
      }

      // The other RISKY errand: pins its own distinct verb so the journal doesn't imply a Steal run is as
      // safe as a Barter trip when only Steal can end in the companion's capture.
      [Test]
      public void GIVEN_a_steal_errand_WHEN_outbound_THEN_the_line_says_steal_something_worth_bringing_home()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Steal, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to steal something worth bringing home.");
      }

      // Barter and Steal share the same Gold payload but must read as clearly different errands, so a
      // player never confuses a legitimate trading trip with a theft that risks capture.
      [Test]
      public void GIVEN_a_barter_errand_WHEN_outbound_THEN_the_line_says_trade_for_a_profit()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Barter, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to trade for a profit.");
      }

      // Completes the five-way switch: pins the last errand type's verb so every CompanionMissionType has
      // an explicit, tested wording rather than silently falling through to the GatherNews default.
      [Test]
      public void GIVEN_an_envoy_errand_WHEN_outbound_THEN_the_line_says_carry_your_word_as_an_envoy()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Envoy, CompanionErrandPhase.Outbound, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira rides for Poros to carry your word as an envoy.");
      }

      // Destination resolution can fail to name the settlement; without this fallback the line would read
      // "Martira rides for  to bring back news." with a dangling gap where the town name should be.
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

      // Confirms the type parameter is deliberately IGNORED past Outbound (a Steal errand here reads
      // identically to any other), so a future edit that tries to colour this phase by type would be a
      // conscious departure from the documented design, not an accident.
      [Test]
      public void GIVEN_any_errand_WHEN_lingering_THEN_the_line_reports_arrival()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Steal, CompanionErrandPhase.Lingering, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira has reached Poros.");
      }

      // Same guarantee for the Returning phase: the player only needs to know the companion is on the way
      // home, not what errand they ran, so the line stays type-agnostic here too.
      [Test]
      public void GIVEN_any_errand_WHEN_returning_THEN_the_line_reports_heading_back()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildStatusLine(
            CompanionMissionType.Barter, CompanionErrandPhase.Returning, "Martira", "Poros");

         //Assert
         line.Should().Be("Martira is on the way back to you.");
      }

      // This is the line the journal shows once OnHeroPrisonerTaken flips a mid-errand companion to
      // AwaitingReunion (a botched risky errand's capture outcome, see CompanionMissions.Resolve). If it
      // read like an ordinary in-progress errand, the player would have no journal cue their companion was
      // actually taken prisoner and is waiting to be freed, not just running late.
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

      // CloseMirrorReturned writes this as the mirror's final status before the journal entry closes; it
      // is the last thing the player reads about this errand, so it must read as a clean, successful close
      // distinct from a recall (which reads as abandonment, see below).
      [Test]
      public void GIVEN_a_completed_errand_WHEN_closing_as_returned_THEN_the_line_reports_the_homecoming()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildReturnedLine("Martira");

         //Assert
         line.Should().Be("Martira has returned and reported in.");
      }

      // CloseMirrorRecalled's own doc pins "reason" as player-facing (e.g. "as your letter ordered") and
      // nullable; this locks the exact parenthetical format so a letter-triggered recall reads as
      // explained, not as an unexplained abandonment.
      [Test]
      public void GIVEN_a_recall_with_a_reason_WHEN_closing_as_recalled_THEN_the_reason_is_appended_in_parentheses()
      {
         //Arrange/Act
         string line = CompanionErrandJournalText.BuildRecalledLine("Martira", "as your letter ordered");

         //Assert
         line.Should().Be("Martira abandoned the errand and returned to you (as your letter ordered).");
      }

      // Guards the null-reason path specifically: without the IsNullOrWhiteSpace check the line would read
      // "...returned to you ()." with dangling empty parentheses on every reasonless (immediate) recall.
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
