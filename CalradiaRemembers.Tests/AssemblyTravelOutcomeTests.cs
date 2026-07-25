// Code written by Gabriel Mailhot, 25/07/2026.
// Pins what becomes of an invitee who set out in person when the world moves under them: the behavior re-checks
// every EnRoute member and applies this verdict (keep coming / send an ambassador / the seat falls empty).
//
// WHY IT MATTERS: Gabriel called out the combinations (2026-07-25). A member of your house riding to the
// council can be caught at sea, outrun the deadline, be pulled into a battle, be captured, or be killed. Each
// must resolve cleanly: death empties the seat, anything short of death that stops them arriving sends a
// stand-in, and only a hero still able to make it keeps travelling. A wrong verdict leaves a ghost seat or
// force-marches a captured lord to a hall he can never reach.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AssemblyTravelOutcomeTests
    {
        private static AssemblyTravelFacts OnTrack() => new() {
            IsAlive = true,
            IsPrisoner = false,
            IsOccupied = false,
            CanReachByDeadline = true
        };

        // The normal case: a live, free, unpinned hero who can still make it in time keeps coming, or nobody
        // would ever actually arrive in person.
        [Test]
        public void GIVEN_a_hero_still_on_track_WHEN_evaluated_THEN_they_keep_arriving()
        {
            AssemblyTravelOutcome.Evaluate(OnTrack()).Should().Be(TravelOutcome.Arriving);
        }

        // Death outranks everything: a hero killed on the road leaves an EMPTY seat, never an ambassador. If this
        // did not win over the other flags, a dead lord could be recorded as "sending a stand-in".
        [Test]
        public void GIVEN_a_hero_killed_on_the_road_WHEN_evaluated_THEN_the_seat_falls_empty()
        {
            AssemblyTravelFacts f = new() {IsAlive = false, IsPrisoner = true, IsOccupied = true, CanReachByDeadline = false};

            AssemblyTravelOutcome.Evaluate(f).Should().Be(TravelOutcome.Fell);
        }

        // Captured en route: they cannot attend, but their house still speaks for them, so an ambassador stands
        // in (never force-march a prisoner to the hall).
        [Test]
        public void GIVEN_a_hero_taken_prisoner_WHEN_evaluated_THEN_an_ambassador_stands_in()
        {
            AssemblyTravelFacts f = OnTrack();
            f = new AssemblyTravelFacts {IsAlive = true, IsPrisoner = true, IsOccupied = false, CanReachByDeadline = true};

            AssemblyTravelOutcome.Evaluate(f).Should().Be(TravelOutcome.Ambassador);
        }

        // Pulled into a battle or besieged on the way: they cannot break away, so a stand-in comes instead.
        [Test]
        public void GIVEN_a_hero_pinned_in_battle_or_siege_WHEN_evaluated_THEN_an_ambassador_stands_in()
        {
            AssemblyTravelFacts f = new() {IsAlive = true, IsPrisoner = false, IsOccupied = true, CanReachByDeadline = true};

            AssemblyTravelOutcome.Evaluate(f).Should().Be(TravelOutcome.Ambassador);
        }

        // The sea-crossing / distance case Gabriel named: a hero who simply cannot reach the host by the deadline
        // (at sea, too far, or already past due) sends an ambassador rather than holding the assembly open forever.
        [Test]
        public void GIVEN_a_hero_who_cannot_reach_in_time_WHEN_evaluated_THEN_an_ambassador_stands_in()
        {
            AssemblyTravelFacts f = new() {IsAlive = true, IsPrisoner = false, IsOccupied = false, CanReachByDeadline = false};

            AssemblyTravelOutcome.Evaluate(f).Should().Be(TravelOutcome.Ambassador);
        }
    }
}
