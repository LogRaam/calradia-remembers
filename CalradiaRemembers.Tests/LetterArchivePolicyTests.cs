// Code written by Gabriel Mailhot, 07/08/2026.
// The correspondence history is unbounded by design (2026-08-24): losing an old letter has no value to the
// player, so nothing is ever evicted (the eviction machinery was removed). This policy is now only
// membership: whether a letter counts as archived correspondence (delivered, not lost, with a body) and
// whether a contact has any correspondence at all. That last answer shows or hides the archive entry on the
// hero's encyclopedia page and the in-letter correspondence panel: a false positive would offer an empty
// archive; a false negative would hide a real history the player wrote.

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
    }
}
