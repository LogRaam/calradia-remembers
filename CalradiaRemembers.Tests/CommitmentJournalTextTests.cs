// Code written by Gabriel Mailhot, 03/07/2026.
// Documents the exact wording of the pledge (TrackedCommitment) quest-journal mirror: one behaviour
// per lifecycle transition, plus the fallback names when a hero reference is unavailable.

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

        [Test]
        public void GIVEN_a_promisor_name_WHEN_building_the_title_THEN_it_reads_Pledge_colon_name()
        {
            //Arrange/Act
            string title = CommitmentJournalText.BuildTitle("Derthert");

            //Assert
            title.Should().Be("Pledge: Derthert");
        }

        [Test]
        public void GIVEN_no_promisor_name_WHEN_building_the_title_THEN_it_falls_back_to_they()
        {
            //Arrange/Act
            string title = CommitmentJournalText.BuildTitle(null);

            //Assert
            title.Should().Be("Pledge: They");
        }

        // ── Opened ───────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_promisor_and_rival_WHEN_the_pledge_is_opened_THEN_the_line_reports_the_vow()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine("Derthert", "Raganvad");

            //Assert
            line.Should().Be("Derthert pledged to move against Raganvad.");
        }

        [Test]
        public void GIVEN_no_rival_name_WHEN_the_pledge_is_opened_THEN_it_falls_back_to_their_rival()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine("Derthert", null);

            //Assert
            line.Should().Be("Derthert pledged to move against their rival.");
        }

        // ── Fulfilled ────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_promisor_and_rival_WHEN_the_pledge_is_fulfilled_THEN_the_line_reports_the_kept_word()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildFulfilledLine("Derthert", "Raganvad");

            //Assert
            line.Should().Be("Derthert kept their word and moved against Raganvad.");
        }

        // ── Breached ─────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_promisor_and_rival_WHEN_the_pledge_is_breached_THEN_the_line_reports_the_broken_oath()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildBreachedLine("Derthert", "Raganvad");

            //Assert
            line.Should().Be("Derthert pledged to move against Raganvad and did nothing.");
        }

        // ── State dispatch ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_the_active_state_WHEN_building_a_log_line_THEN_it_reads_as_the_opened_line()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildLogLine(CommitmentState.Active, "Derthert", "Raganvad");

            //Assert
            line.Should().Be(CommitmentJournalText.BuildOpenedLine("Derthert", "Raganvad"));
        }

        [Test]
        public void GIVEN_the_fulfilled_state_WHEN_building_a_log_line_THEN_it_reads_as_the_fulfilled_line()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildLogLine(CommitmentState.Fulfilled, "Derthert", "Raganvad");

            //Assert
            line.Should().Be(CommitmentJournalText.BuildFulfilledLine("Derthert", "Raganvad"));
        }

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
