// Code written by Gabriel Mailhot, 31/10/2026.
// The letter box, step 3 (Gabriel, 31/10/2026: "oui go pour etape 3"): a letter travels as an envelope and is written when
// the player first opens it. A letter without text used to mean only "still being written" or "dead"; this tells an
// envelope from both. If it is wrong, an envelope is never delivered, is deleted at the next load, is dropped by a late
// courier, or a child's pension letter is written twice.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterEnvelopePolicyTests
    {
        private static PendingLetter Envelope() => new() {Reason = LetterReason.RealmTidings, WrittenOnOpening = true, DispatchDay = 10, DeliveryDay = 13};

        private static PendingLetter Placeholder() => new() {Reason = LetterReason.RealmTidings, DispatchDay = int.MaxValue, DeliveryDay = 11};

        // STAKE: an envelope reaches the player unwritten; the old "no text = not ready" rule would hold it on the road forever.
        [Test]
        public void GIVEN_an_unwritten_envelope_WHEN_delivery_asks_if_it_is_ready_THEN_it_is_while_a_placeholder_is_not()
        {
            LetterEnvelopePolicy.IsReadyToDeliver(Envelope()).Should().BeTrue();
            LetterEnvelopePolicy.IsReadyToDeliver(Placeholder()).Should().BeFalse();
        }

        // STAKE: the session-start cleanup removes a placeholder whose writing died with the last session; it must never take
        // an envelope waiting in the box with it.
        [Test]
        public void GIVEN_a_saved_game_reloaded_WHEN_orphans_are_cleaned_THEN_envelopes_stay_and_dead_placeholders_go()
        {
            LetterEnvelopePolicy.IsOrphanedPlaceholder(Placeholder()).Should().BeTrue();
            PendingLetter stuck = Envelope();
            stuck.DispatchDay = int.MaxValue;
            LetterEnvelopePolicy.IsOrphanedPlaceholder(stuck).Should().BeFalse();
        }

        // STAKE: a courier past its deadline hands a real letter to the date fallback and drops only a blank placeholder; an
        // envelope is a real letter.
        [Test]
        public void GIVEN_a_courier_past_its_deadline_WHEN_its_letter_is_judged_THEN_an_envelope_is_not_dropped()
        {
            LetterEnvelopePolicy.DroppedAtCourierDeadline(Envelope()).Should().BeFalse();
            LetterEnvelopePolicy.DroppedAtCourierDeadline(Placeholder()).Should().BeTrue();
        }

        // STAKE: a scheduled letter (a child's pension) is fulfilled once sent; an envelope counted as "still being written"
        // would be written again.
        [Test]
        public void GIVEN_a_scheduled_letter_sent_as_an_envelope_WHEN_counted_THEN_it_is_sent()
        {
            LetterEnvelopePolicy.CountsAsSent(Envelope()).Should().BeTrue();
            LetterEnvelopePolicy.CountsAsSent(Placeholder()).Should().BeFalse();
        }

        // STAKE: once its text is written (opened, or when a courier was taken), an envelope is an ordinary letter and is
        // never written again.
        [Test]
        public void GIVEN_an_envelope_whose_text_exists_WHEN_asked_THEN_it_is_no_longer_unwritten()
        {
            PendingLetter written = Envelope();
            written.Content = "Friend, word reaches me from the north.";

            LetterEnvelopePolicy.IsUnwritten(Envelope()).Should().BeTrue();
            LetterEnvelopePolicy.IsUnwritten(written).Should().BeFalse();
        }
    }
}
