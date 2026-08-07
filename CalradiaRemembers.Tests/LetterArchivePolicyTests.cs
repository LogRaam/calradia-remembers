// Code written by Gabriel Mailhot, 07/08/2026.
// Retained letters live in the byte[] JSON profile store, which has a native-string size ceiling, so a
// contact's kept correspondence MUST be bounded or a chatty NPC eventually bloats (and can corrupt) the
// save. This policy is that bound: it selects the oldest delivered letters to evict once the archive passes
// the cap, and it must NEVER select a letter the delivery pipeline still owns (in transit, waylaid, or a
// blank placeholder): pruning one of those would drop a letter mid-flight or strand a courier. It also
// answers "does this contact have any correspondence", which is what shows or hides the archive button on
// the hero's encyclopedia page: a false positive would offer an empty archive.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterArchivePolicyTests
    {
        private static PendingLetter Incoming(int sentOnDay, bool delivered = true, bool lost = false)
            => new PendingLetter
            {
                SenderName = "Derthert",
                Content = $"letter {sentOnDay}",
                SentOnDay = sentOnDay,
                DeliveryDay = sentOnDay + 2,
                IsDelivered = delivered,
                IsLost = lost
            };

        private static PlayerLetter Outgoing(int sentOnDay, bool delivered = true)
            => new PlayerLetter
            {
                RecipientName = "Derthert",
                Content = $"reply {sentOnDay}",
                SentOnDay = sentOnDay,
                DeliveryDay = sentOnDay + 1,
                IsDelivered = delivered
            };

        // The button that opens the archive must appear only when there is something to read; a delivered
        // letter in either direction is enough, so both lists are checked.
        [Test]
        public void GIVEN_a_delivered_letter_in_either_direction_WHEN_asked_THEN_correspondence_exists()
        {
            LetterArchivePolicy.HasCorrespondence(new List<PendingLetter> { Incoming(10) }, null).Should().BeTrue();
            LetterArchivePolicy.HasCorrespondence(null, new List<PlayerLetter> { Outgoing(10) }).Should().BeTrue();
        }

        // A letter still on the road is not correspondence yet; offering an archive for it would open an empty
        // window, so an in-transit-only contact must read as having none.
        [Test]
        public void GIVEN_only_an_in_transit_letter_WHEN_asked_THEN_no_correspondence_exists()
        {
            LetterArchivePolicy.HasCorrespondence(new List<PendingLetter> { Incoming(10, delivered: false) }, null)
                .Should().BeFalse();
        }

        // Under the cap, nothing is evicted: a normal correspondence is kept whole, and the player loses no
        // history they might want to reread.
        [Test]
        public void GIVEN_fewer_letters_than_the_cap_WHEN_selecting_prunable_THEN_nothing_is_dropped()
        {
            var sent = new List<PendingLetter> { Incoming(1), Incoming(2), Incoming(3) };

            (IReadOnlyList<PendingLetter> incoming, IReadOnlyList<PlayerLetter> outgoing) =
                LetterArchivePolicy.SelectPrunable(sent, null, cap: 5);

            incoming.Should().BeEmpty();
            outgoing.Should().BeEmpty();
        }

        // Over the cap, the OLDEST go first (across both directions), keeping the newest correspondence; the
        // count dropped is exactly the overflow, so the archive settles at the cap rather than over- or
        // under-pruning.
        [Test]
        public void GIVEN_more_letters_than_the_cap_WHEN_selecting_prunable_THEN_the_oldest_are_chosen()
        {
            var sent = new List<PendingLetter> { Incoming(1), Incoming(3), Incoming(5) };
            var received = new List<PlayerLetter> { Outgoing(2), Outgoing(4) };

            (IReadOnlyList<PendingLetter> incoming, IReadOnlyList<PlayerLetter> outgoing) =
                LetterArchivePolicy.SelectPrunable(sent, received, cap: 3);

            // 5 archived, cap 3 -> drop the 2 oldest: the day-1 NPC letter and the day-2 player letter.
            incoming.Should().HaveCount(1);
            incoming[0].SentOnDay.Should().Be(1);
            outgoing.Should().HaveCount(1);
            outgoing[0].SentOnDay.Should().Be(2);
        }

        // The eviction must ignore in-flight, waylaid, and blank letters entirely: they belong to the delivery
        // pipeline, and dropping one here would lose a letter before it ever reached the player. Even though the
        // in-transit letter is the "oldest", only the delivered ones may be counted or evicted.
        [Test]
        public void GIVEN_letters_still_owned_by_the_delivery_pipeline_WHEN_selecting_prunable_THEN_they_are_never_chosen()
        {
            var sent = new List<PendingLetter>
            {
                Incoming(1, delivered: false), // in transit, not archived, must never be pruned
                Incoming(2, lost: true),       // waylaid, not archived
                Incoming(3),                   // delivered
                Incoming(4)                    // delivered
            };

            (IReadOnlyList<PendingLetter> incoming, IReadOnlyList<PlayerLetter> _) =
                LetterArchivePolicy.SelectPrunable(sent, null, cap: 1);

            // Only 2 archived (days 3 and 4); cap 1 -> drop just the day-3 one. The in-transit and lost letters
            // are untouched despite being older.
            incoming.Should().HaveCount(1);
            incoming[0].SentOnDay.Should().Be(3);
        }
    }
}
