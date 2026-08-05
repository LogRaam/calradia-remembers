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

        // ── R7-light: kind-aware overloads (swear_oath) ─────────────────────

        // STAKE: a pay_gold oath's journal mirror must name the actual sum, or the player reading their quest
        // log has no idea how much is owed and by when they should expect it.
        [Test]
        public void GIVEN_a_pay_gold_oath_WHEN_the_oath_is_opened_THEN_the_line_names_the_sum()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine(CommitmentKind.PayGold, "Derthert", null, 300);

            //Assert
            line.Should().Be("Derthert swore to pay you 300 denars.");
        }

        // A keep_peace oath's own clause names the faction, never a rival's name; this is the field the mod
        // resolves via CommitmentBehavior.FindFactionById, and the wording must reflect that it is a faction.
        [Test]
        public void GIVEN_a_keep_peace_oath_WHEN_the_oath_is_fulfilled_THEN_the_line_names_the_faction()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildFulfilledLine(CommitmentKind.KeepPeace, "Derthert", "Vlandia", null);

            //Assert
            line.Should().Be("Derthert kept their oath to keep the peace with Vlandia.");
        }

        // A protect oath names no target at all: the wording must never leave a dangling "with" or "against"
        // clause when neither a rival nor a faction was ever recorded for this kind.
        [Test]
        public void GIVEN_a_protect_oath_WHEN_the_oath_is_breached_THEN_the_line_reads_without_a_target()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildBreachedLine(CommitmentKind.Protect, "Derthert", null, null);

            //Assert
            line.Should().Be("Derthert swore to stand at your side in battle, and broke that oath.");
        }

        // Guards the case a promisor Hero reference fails to resolve for a swear_oath kind too: the same
        // "They" fallback the original MoveAgainst-only builders already use, never a blank or null tail.
        [Test]
        public void GIVEN_no_promisor_name_WHEN_a_pay_gold_oath_is_opened_THEN_it_falls_back_to_they()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine(CommitmentKind.PayGold, null, null, 300);

            //Assert
            line.Should().Be("They swore to pay you 300 denars.");
        }

        // A pay_gold oath with no readable amount (defensive: should never happen once ClampPromisedAmount has
        // run, but the journal text must still read as a sentence rather than showing a blank or "0 denars").
        [Test]
        public void GIVEN_a_pay_gold_oath_with_no_amount_WHEN_opened_THEN_it_still_reads_as_a_plain_sentence()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildOpenedLine(CommitmentKind.PayGold, "Derthert", null, null);

            //Assert
            line.Should().Be("Derthert swore to pay you a sum of denars.");
        }

        // Confirms the kind-aware dispatcher (mirroring BuildLogLine's own two-name overload) routes to the
        // matching wording for every lifecycle state, so CommitmentBehavior never has to duplicate this switch.
        [Test]
        public void GIVEN_the_active_state_WHEN_building_a_kind_aware_log_line_THEN_it_reads_as_the_opened_line()
        {
            //Arrange/Act
            string line = CommitmentJournalText.BuildLogLine(CommitmentState.Active, CommitmentKind.Protect, "Derthert", null, null);

            //Assert
            line.Should().Be(CommitmentJournalText.BuildOpenedLine(CommitmentKind.Protect, "Derthert", null, null));
        }
    }
}
