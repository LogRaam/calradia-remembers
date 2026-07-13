// Code written by Gabriel Mailhot, 12/07/2026.
// Documents when an accepted invitee attends in person versus sends an ambassador: it fits when the journey
// still fits the deadline. A hero already at the host attends in person even past the deadline; a hero still
// on the road when the deadline lapses sends a stand-in.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class AssemblyArrivalPolicyTests
    {
        // The common case: the journey fits, so the hero shows up in person. Includes the inclusive boundary
        // (eta == deadline still counts as in time).
        [TestCase(3, 5)]  // comfortably in time
        [TestCase(5, 5)]  // arrives exactly on the deadline
        [TestCase(1, 10)]
        public void GIVEN_the_journey_fits_the_deadline_WHEN_resolving_THEN_the_invitee_attends_in_person(int eta, int deadline)
            => AssemblyArrivalPolicy.ResolveArrival(eta, deadline).Should().Be(ArrivalMode.InPerson);

        // This is the robust answer to fragile forced-AI movement (the courier scar tissue, per the policy's own
        // header): when the ETA cannot fit the deadline, an ambassador covers the gap with narration instead of
        // a stuck party breaking the assembly.
        [TestCase(6, 5)]  // one day short
        [TestCase(20, 3)] // far away
        public void GIVEN_the_journey_overruns_the_deadline_WHEN_resolving_THEN_an_ambassador_takes_the_seat(int eta, int deadline)
            => AssemblyArrivalPolicy.ResolveArrival(eta, deadline).Should().Be(ArrivalMode.Ambassador);

        // Documented exception in ResolveArrival's own XML doc: a hero already at the host must never be bumped
        // to an ambassador just because the deadline elapsed after they arrived, eta<=0 always wins.
        [Test]
        public void GIVEN_the_invitee_is_already_at_the_host_WHEN_resolving_THEN_they_attend_in_person_even_past_the_deadline()
        {
            AssemblyArrivalPolicy.ResolveArrival(0, 10).Should().Be(ArrivalMode.InPerson);
            AssemblyArrivalPolicy.ResolveArrival(0, -3).Should().Be(ArrivalMode.InPerson);
            AssemblyArrivalPolicy.ResolveArrival(-2, -5).Should().Be(ArrivalMode.InPerson);
        }

        // Contrast with the test above: still on the road (eta > 0) when the deadline lapses is the ordinary
        // ambassador case, not the "already there" exception.
        [Test]
        public void GIVEN_the_deadline_has_lapsed_while_still_on_the_road_WHEN_resolving_THEN_an_ambassador_takes_the_seat()
            => AssemblyArrivalPolicy.ResolveArrival(4, -1).Should().Be(ArrivalMode.Ambassador);
    }
}
