// Code written by Gabriel Mailhot, 21/06/2026.
// Guards the courier window's mode rules: a blank reply or a letter the player can't pay for must never
// seal, and each mode must show exactly the right parchment. A drift here loses words or gold silently.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterComposerStateTests
    {
        // ── Send eligibility ────────────────────────────────────────────────

        [Test]
        public void GIVEN_reading_a_letter_WHEN_judging_send_THEN_it_is_never_sendable()
        {
            var state = new LetterComposerState(LetterWindowMode.Read);
            state.CanSend("anything", playerGold: 9999).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_blank_draft_WHEN_judging_send_THEN_it_cannot_send()
        {
            new LetterComposerState(LetterWindowMode.Compose).CanSend("   ", playerGold: 9999).Should().BeFalse();
            new LetterComposerState(LetterWindowMode.Reply).CanSend("", playerGold: 9999).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_composed_letter_the_player_cannot_pay_for_WHEN_judging_send_THEN_it_cannot_send()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose, courierCost: 500);
            state.CanSend("My lord,", playerGold: 499).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_composed_letter_the_player_can_afford_WHEN_judging_send_THEN_it_can_send()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose, courierCost: 500);
            state.CanSend("My lord,", playerGold: 500).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_reply_WHEN_judging_send_THEN_no_courier_fee_applies()
        {
            // A reply rides the existing exchange — a penniless player can still answer.
            var state = new LetterComposerState(LetterWindowMode.Reply);
            state.CanSend("I will come.", playerGold: 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_negative_courier_cost_WHEN_constructed_THEN_it_is_clamped_to_free()
        {
            var state = new LetterComposerState(LetterWindowMode.Compose, courierCost: -50);
            state.CourierCost.Should().Be(0);
            state.CanAfford(0).Should().BeTrue();
        }

        // ── Widget visibility per mode ──────────────────────────────────────

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
