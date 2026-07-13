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
        [TestCase(3, 5)]  // comfortably in time
        [TestCase(5, 5)]  // arrives exactly on the deadline
        [TestCase(1, 10)]
        public void GIVEN_the_journey_fits_the_deadline_WHEN_resolving_THEN_the_invitee_attends_in_person(int eta, int deadline)
            => AssemblyArrivalPolicy.ResolveArrival(eta, deadline).Should().Be(ArrivalMode.InPerson);

        [TestCase(6, 5)]  // one day short
        [TestCase(20, 3)] // far away
        public void GIVEN_the_journey_overruns_the_deadline_WHEN_resolving_THEN_an_ambassador_takes_the_seat(int eta, int deadline)
            => AssemblyArrivalPolicy.ResolveArrival(eta, deadline).Should().Be(ArrivalMode.Ambassador);

        [Test]
        public void GIVEN_the_invitee_is_already_at_the_host_WHEN_resolving_THEN_they_attend_in_person_even_past_the_deadline()
        {
            AssemblyArrivalPolicy.ResolveArrival(0, 10).Should().Be(ArrivalMode.InPerson);
            AssemblyArrivalPolicy.ResolveArrival(0, -3).Should().Be(ArrivalMode.InPerson);
            AssemblyArrivalPolicy.ResolveArrival(-2, -5).Should().Be(ArrivalMode.InPerson);
        }

        [Test]
        public void GIVEN_the_deadline_has_lapsed_while_still_on_the_road_WHEN_resolving_THEN_an_ambassador_takes_the_seat()
            => AssemblyArrivalPolicy.ResolveArrival(4, -1).Should().Be(ArrivalMode.Ambassador);
    }
}
