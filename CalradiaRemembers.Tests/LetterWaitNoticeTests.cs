// Code written by Gabriel Mailhot, 19/11/2026.
// LetterWaitNotice: what the player is told while an opened letter is being written, and how often. Vexilary (Nexus,
// 19/11/2026, small local models): "Letters from letters box doesn't load"; after "You break the seal", nothing showed
// until the slow model answered or timed out. If this is wrong, a slow letter reads as a frozen game, or the reminders
// flood the screen.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterWaitNoticeTests
    {
        // STAKE: the player knows at once that the letter is being written and will open by itself.
        [Test]
        public void GIVEN_an_envelope_opened_WHEN_the_seal_breaks_THEN_the_player_is_told_it_is_being_written()
        {
            LetterWaitNotice.Opening("Derthert").Should().Be("You break the seal of Derthert's letter. It is being written, and opens when it is ready.");
        }

        // STAKE: a slow model shows a sign of life every so often, never one per frame.
        [TestCase(5, 0, false)]
        [TestCase(15, 0, true)]
        [TestCase(20, 1, false)]
        [TestCase(30, 1, true)]
        public void GIVEN_a_letter_still_being_written_WHEN_time_passes_THEN_a_reminder_comes_each_interval(double waited, int shown, bool due)
        {
            LetterWaitNotice.ReminderDue(waited, shown).Should().Be(due);
        }

        // STAKE: the reminder names the sender and how long it has been, so it reads as progress.
        [Test]
        public void GIVEN_thirty_seconds_waited_WHEN_reminded_THEN_the_sender_and_the_time_are_named()
        {
            LetterWaitNotice.Reminder("Derthert", 30.7).Should().Be("Derthert's letter is still being written (30 s)...");
        }
    }
}
