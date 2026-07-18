// Code written by Gabriel Mailhot, 18/07/2026.
// Courier audit (island report): ONE courier in flight per sender. Without this gate, every queued letter
// dispatched its own courier, and a slow or stuck courier (a sender on an island, a player at sea)
// snowballed into a whole pack of couriers riding from the same NPC. These tests pin the dedup rule: any
// undelivered, unlost letter still bearing a courier party id means the road is occupied, and no second
// courier may spawn for that sender until it clears.

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
    public sealed class CourierDispatchPolicyTests
    {
        // A null letter list means no sender state at all — the gate must treat it as "road clear", never
        // throw, or a missing profile would mute every courier dispatch.
        [Test]
        public void GIVEN_no_letters_at_all_WHEN_checking_courier_in_flight_THEN_it_is_false()
        {
            CourierDispatchPolicy.HasCourierInFlight(null).Should().BeFalse();
        }

        // The baseline: a sender with nothing queued has no courier on the road, so a dispatch must proceed.
        [Test]
        public void GIVEN_an_empty_letter_list_WHEN_checking_courier_in_flight_THEN_it_is_false()
        {
            CourierDispatchPolicy.HasCourierInFlight(new List<PendingLetter>()).Should().BeFalse();
        }

        // The core case the audit fixes: one letter already riding with a courier must block any further
        // dispatch for that sender, or the pack-of-couriers bug returns.
        [Test]
        public void GIVEN_an_undelivered_letter_with_a_courier_WHEN_checking_courier_in_flight_THEN_it_is_true()
        {
            var letters = new List<PendingLetter>
            {
                new() {CourierPartyId = "courier_123", IsDelivered = false, IsLost = false}
            };

            CourierDispatchPolicy.HasCourierInFlight(letters).Should().BeTrue();
        }

        // A delivered letter keeps its history but its courier is long gone; it must not block a fresh
        // dispatch forever, or a sender could only ever have one courier per campaign.
        [Test]
        public void GIVEN_a_delivered_letter_with_a_courier_id_WHEN_checking_courier_in_flight_THEN_it_is_false()
        {
            var letters = new List<PendingLetter>
            {
                new() {CourierPartyId = "courier_123", IsDelivered = true, IsLost = false}
            };

            CourierDispatchPolicy.HasCourierInFlight(letters).Should().BeFalse();
        }

        // Same for a lost letter (waylaid courier): the road is clear the moment the letter is written off,
        // or one interception would mute the sender for the rest of the campaign.
        [Test]
        public void GIVEN_a_lost_letter_with_a_courier_id_WHEN_checking_courier_in_flight_THEN_it_is_false()
        {
            var letters = new List<PendingLetter>
            {
                new() {CourierPartyId = "courier_123", IsDelivered = false, IsLost = true}
            };

            CourierDispatchPolicy.HasCourierInFlight(letters).Should().BeFalse();
        }

        // A queued letter still waiting for its dispatch day has an EMPTY courier id; it is exactly what the
        // gate must NOT count, or a backlog of queued letters would block each other from ever dispatching.
        [Test]
        public void GIVEN_a_queued_letter_without_a_courier_id_WHEN_checking_courier_in_flight_THEN_it_is_false()
        {
            var letters = new List<PendingLetter>
            {
                new() {CourierPartyId = "", IsDelivered = false, IsLost = false},
                new() {CourierPartyId = null, IsDelivered = false, IsLost = false}
            };

            CourierDispatchPolicy.HasCourierInFlight(letters).Should().BeFalse();
        }

        // One in-flight courier among any amount of other history (delivered, lost, still queued) must still
        // block: the rule counts the road, not the mailbag.
        [Test]
        public void GIVEN_one_in_flight_letter_among_history_WHEN_checking_courier_in_flight_THEN_it_is_true()
        {
            var letters = new List<PendingLetter>
            {
                new() {CourierPartyId = "courier_old", IsDelivered = true},
                new() {CourierPartyId = "courier_dead", IsLost = true},
                new() {CourierPartyId = null, IsDelivered = false, IsLost = false},
                new() {CourierPartyId = "courier_now", IsDelivered = false, IsLost = false}
            };

            CourierDispatchPolicy.HasCourierInFlight(letters).Should().BeTrue();
        }
    }
}
