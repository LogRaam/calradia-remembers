// Code written by Gabriel Mailhot, 25/07/2026.
// Pins the host-validity rule: whether a convened assembly's chosen town or castle is still somewhere the
// gathering can be held. The behavior re-checks this each day and on siege / ownership events; a non-Valid
// verdict CANCELS the assembly.
//
// WHY IT MATTERS: Gabriel called it out (2026-07-25). If the host falls under siege, is taken by an enemy, or
// is razed while lords are converging on it, the assembly must not carry on sending people to a battlefield or
// hostile ground. A wrong verdict here either strands travellers heading into a siege or cancels a perfectly
// good gathering.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AssemblyHostValidityTests
    {
        private static AssemblyHostFacts Good() => new() {
            HostExists = true,
            HostBesieged = false,
            HostOwnerAtWarWithInitiator = false
        };

        // The baseline: a standing, peaceful, friendly-ground host is valid, or the assembly could never be held
        // even in the normal case.
        [Test]
        public void GIVEN_a_standing_peaceful_host_WHEN_evaluated_THEN_it_is_valid()
        {
            AssemblyHostValidity.Evaluate(Good()).Should().Be(HostValidity.Valid);
            AssemblyHostValidity.IsValid(Good()).Should().BeTrue();
        }

        // A razed / vanished settlement cannot host anything: this is the most fundamental loss and must outrank
        // every other reason, so the cancel message is the truest one.
        [Test]
        public void GIVEN_a_host_that_no_longer_exists_WHEN_evaluated_THEN_it_is_Gone()
        {
            AssemblyHostFacts f = Good();
            f = new AssemblyHostFacts {HostExists = false, HostBesieged = true, HostOwnerAtWarWithInitiator = true};

            AssemblyHostValidity.Evaluate(f).Should().Be(HostValidity.Gone);
        }

        // A siege turns the venue into a battlefield: the gathering cannot be held, so the assembly cancels.
        [Test]
        public void GIVEN_a_besieged_host_WHEN_evaluated_THEN_it_is_Besieged()
        {
            AssemblyHostFacts f = new() {HostExists = true, HostBesieged = true, HostOwnerAtWarWithInitiator = false};

            AssemblyHostValidity.Evaluate(f).Should().Be(HostValidity.Besieged);
        }

        // The host changing hands to an enemy of the summoner (or its owner declaring war) makes the ground
        // hostile: the neutral/friendly venue the assembly was called on no longer exists, so it cancels.
        [Test]
        public void GIVEN_a_host_now_held_by_an_enemy_WHEN_evaluated_THEN_it_is_EnemyHeld()
        {
            AssemblyHostFacts f = new() {HostExists = true, HostBesieged = false, HostOwnerAtWarWithInitiator = true};

            AssemblyHostValidity.Evaluate(f).Should().Be(HostValidity.EnemyHeld);
        }
    }
}
