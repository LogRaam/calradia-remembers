// Code written by Gabriel Mailhot, 03/07/2026.
// Documents the exact wording of the pledge (TrackedCommitment) quest-journal mirror: one behaviour
// per lifecycle transition, plus the fallback names when a hero reference is unavailable.
// This wording is what the player actually reads in their quest log (CommitmentJournalBridge calls these
// builders straight into the journal entry's status line), so a wrong word here is a wrong sentence in front
// of the player, not an internal detail. The fallback names exist because resolving a live promisor/rival
// Hero can fail: the log line must still read as a sentence, never break or blank out.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CommitmentJournalTextTests
    {
        // ── Title ────────────────────────────────────────────────────────────

        // The journal entry's header text, shown in the quest list before the player ever opens the entry.
        [Test]
        public void GIVEN_a_promisor_name_WHEN_building_the_title_THEN_it_reads_Pledge_colon_name()
        {
            //Arrange/Act
            string title = CommitmentJournalText.BuildTitle("Derthert");

            //Assert
            title.Should().Be("Pledge: Derthert");
        }

        // Guards the case a promisor Hero reference fails to resolve: the journal title must still read as
        // English, never "Pledge: " with a blank or null tail.
        [Test]
        public void GIVEN_no_promisor_name_WHEN_building_the_title_THEN_it_falls_back_to_they()
        {
            //Arrange/Act
            string title = CommitmentJournalText.BuildTitle(null);

            //Assert
            title.Should().Be("Pledge: They");
        }

        // ── Opened ───────────────────────────────────────────────────────────

        // The line written the moment the pledge mirror opens, the player's first read of what was just vowed.
        [Test]
        public void GIVEN_a_promisor_and_rival_WHEN_the_pledge_is_opened_THEN_the_line_reports_the_vow()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine("Derthert", "Raganvad");

            //Assert
            line.Should().Be("Derthert pledged to move against Raganvad.");
        }

        // Same resolution-failure guard as the title, for the rival side of the sentence.
        [Test]
        public void GIVEN_no_rival_name_WHEN_the_pledge_is_opened_THEN_it_falls_back_to_their_rival()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine("Derthert", null);

            //Assert
            line.Should().Be("Derthert pledged to move against their rival.");
        }

        // ── Fulfilled ────────────────────────────────────────────────────────

        // The closing line CommitmentJournalBridge writes once StanceService confirms the pledge was kept.
        [Test]
        public void GIVEN_a_promisor_and_rival_WHEN_the_pledge_is_fulfilled_THEN_the_line_reports_the_kept_word()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildFulfilledLine("Derthert", "Raganvad");

            //Assert
            line.Should().Be("Derthert kept their word and moved against Raganvad.");
        }

        // ── Breached ─────────────────────────────────────────────────────────

        // The closing line CommitmentJournalBridge writes on the daily overdue sweep's breach: an oath
        // publicly broken, told to the player as plainly as the kept one.
        [Test]
        public void GIVEN_a_promisor_and_rival_WHEN_the_pledge_is_breached_THEN_the_line_reports_the_broken_oath()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildBreachedLine("Derthert", "Raganvad");

            //Assert
            line.Should().Be("Derthert pledged to move against Raganvad and did nothing.");
        }

        // ── State dispatch ───────────────────────────────────────────────────

        // Active is the one state a mirror is ever newly created in, so its log line must dispatch to the
        // opening wording exactly.
        [Test]
        public void GIVEN_the_active_state_WHEN_building_a_log_line_THEN_it_reads_as_the_opened_line()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildLogLine(CommitmentState.Active, "Derthert", "Raganvad");

            //Assert
            line.Should().Be(CommitmentJournalText.BuildOpenedLine("Derthert", "Raganvad"));
        }

        // Confirms the dispatcher routes to the fulfilled wording, not a generic or stale opened line.
        [Test]
        public void GIVEN_the_fulfilled_state_WHEN_building_a_log_line_THEN_it_reads_as_the_fulfilled_line()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildLogLine(CommitmentState.Fulfilled, "Derthert", "Raganvad");

            //Assert
            line.Should().Be(CommitmentJournalText.BuildFulfilledLine("Derthert", "Raganvad"));
        }

        // Confirms the dispatcher routes to the breached wording, so CommitmentJournalBridge never has to
        // duplicate this switch itself.
        [Test]
        public void GIVEN_the_breached_state_WHEN_building_a_log_line_THEN_it_reads_as_the_breached_line()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildLogLine(CommitmentState.Breached, "Derthert", "Raganvad");

            //Assert
            line.Should().Be(CommitmentJournalText.BuildBreachedLine("Derthert", "Raganvad"));
        }
    }
}
