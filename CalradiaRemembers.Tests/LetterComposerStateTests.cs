// Code written by Gabriel Mailhot, 21/06/2026.
// Guards the courier window's mode rules: a blank reply or a letter the player can't pay for must never
// seal, and each mode must show exactly the right parchment. A drift here loses words or gold silently.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterComposerStateTests
    {
        // ── Send eligibility ────────────────────────────────────────────────

        // A read-only session must never expose a send path at all, whatever gold or text a caller passes in:
        // Mode alone should already veto it, so a UI bug that shows the Send button in Read mode cannot enable
        // it.
        [Test]
        public void GIVEN_reading_a_letter_WHEN_judging_send_THEN_it_is_never_sendable()
        {
            var state = new LetterComposerState(LetterWindowMode.Read);
            state.CanSend("anything", playerGold: 9999).Should().BeFalse();
        }

        // Sealing a blank letter would waste the courier fee (and confuse the recipient) on nothing at all; a
        // whitespace-only draft must count as blank, not slip through as "technically non-empty" text.
        [Test]
        public void GIVEN_a_blank_draft_WHEN_judging_send_THEN_it_cannot_send()
        {
            new LetterComposerState(LetterWindowMode.Compose).CanSend("   ", playerGold: 9999).Should().BeFalse();
            new LetterComposerState(LetterWindowMode.Reply).CanSend("", playerGold: 9999).Should().BeFalse();
        }

        // The file header names the stake plainly: a drift here loses the player's gold silently. Sending must
        // never be allowed to go through one denar short of the courier fee.
        [Test]
        public void GIVEN_a_composed_letter_the_player_cannot_pay_for_WHEN_judging_send_THEN_it_cannot_send()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose, courierCost: 500);
            state.CanSend("My lord,", playerGold: 499).Should().BeFalse();
        }

        // The other side of the same boundary as the test above: exactly enough gold must not be wrongly
        // treated as insufficient, or a solvent player would be blocked from sending at all.
        [Test]
        public void GIVEN_a_composed_letter_the_player_can_afford_WHEN_judging_send_THEN_it_can_send()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose, courierCost: 500);
            state.CanSend("My lord,", playerGold: 500).Should().BeTrue();
        }

        // A reply rides the existing exchange (production doc): charging a fresh courier fee here would wrongly
        // block a penniless player from ever answering a letter they already received.
        [Test]
        public void GIVEN_a_reply_WHEN_judging_send_THEN_no_courier_fee_applies()
        {
            // A reply rides the existing exchange — a penniless player can still answer.
            var state = new LetterComposerState(LetterWindowMode.Reply);
            state.CanSend("I will come.", playerGold: 0).Should().BeTrue();
        }

        // A negative cost is a caller bug, not a discount to pass along: the constructor clamps it to free
        // rather than letting a malformed value make CanAfford unpredictable.
        [Test]
        public void GIVEN_a_negative_courier_cost_WHEN_constructed_THEN_it_is_clamped_to_free()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose, courierCost: -50);
            state.CourierCost.Should().Be(0);
            state.CanAfford(0).Should().BeTrue();
        }

        // ── Widget visibility per mode ──────────────────────────────────────

        // Each mode must show exactly the right parchment (file header): Read must offer Acknowledge/Reply and
        // the received body, never a writing area, or the player could type into a window meant only for
        // reading.
        [Test]
        public void GIVEN_read_mode_WHEN_laying_out_THEN_only_the_received_body_and_choices_show()
        {
            var state = new LetterComposerState(LetterWindowMode.Read);
            state.ShowsReceivedBody.Should().BeTrue();
            state.ShowsAcknowledge.Should().BeTrue();
            state.ShowsReplyButton.Should().BeTrue();
            state.ShowsInput.Should().BeFalse();
            state.ShowsSend.Should().BeFalse();
        }

        // A fresh letter has no received body to show and no prior exchange to acknowledge or reply to; showing
        // those widgets in Compose would offer choices that make no sense for a blank page.
        [Test]
        public void GIVEN_compose_mode_WHEN_laying_out_THEN_only_the_writing_area_and_send_show()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose);
            state.ShowsInput.Should().BeTrue();
            state.ShowsSend.Should().BeTrue();
            state.ShowsReceivedBody.Should().BeFalse();
            state.ShowsAcknowledge.Should().BeFalse();
            state.ShowsReplyButton.Should().BeFalse();
        }

        // Replying needs the original letter visible above the answer, but the Read-only Acknowledge/Reply
        // choices must not reappear once a reply is already underway, or the player could restart the same
        // reply loop instead of finishing it.
        [Test]
        public void GIVEN_reply_mode_WHEN_laying_out_THEN_both_the_received_body_and_the_writing_area_show()
        {
            var state = new LetterComposerState(LetterWindowMode.Reply);
            state.ShowsReceivedBody.Should().BeTrue();
            state.ShowsInput.Should().BeTrue();
            state.ShowsSend.Should().BeTrue();
            state.ShowsAcknowledge.Should().BeFalse();
            state.ShowsReplyButton.Should().BeFalse();
        }
    }
}
