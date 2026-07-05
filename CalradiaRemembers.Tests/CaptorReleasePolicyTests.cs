// Code written by Gabriel Mailhot, 05/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptorReleasePolicyTests
    {
        [Test]
        public void GIVEN_a_nemesis_vendetta_WHEN_resolving_release_THEN_the_captor_refuses_every_path()
        {
            var verdict = CaptorReleasePolicy.Resolve(captorMercy: 2, grudgeAgainstPlayer: 0, isNemesisVendetta: true);

            verdict.AllowsPersuasion.Should().BeFalse();
            verdict.AllowsRansom.Should().BeFalse();
            verdict.RefusesAll.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_heavy_grudge_at_the_vendetta_floor_WHEN_resolving_release_THEN_the_captor_refuses_every_path()
        {
            var verdict = CaptorReleasePolicy.Resolve(2, CaptorReleasePolicy.VendettaGrudgeFloor, false);

            verdict.RefusesAll.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_merciful_captor_with_no_grudge_WHEN_resolving_release_THEN_both_persuasion_and_ransom_are_open()
        {
            var verdict = CaptorReleasePolicy.Resolve(CaptorReleasePolicy.MercyPersuasionFloor, 0, false);

            verdict.AllowsPersuasion.Should().BeTrue();
            verdict.AllowsRansom.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_cruel_captor_with_a_moderate_grudge_WHEN_resolving_release_THEN_persuasion_is_closed_but_ransom_stays_open()
        {
            // Mercy 0 (below the persuasion floor) + a grudge above the light ceiling but below the vendetta floor.
            var verdict = CaptorReleasePolicy.Resolve(0, CaptorReleasePolicy.LightGrudgeCeiling, false);

            verdict.AllowsPersuasion.Should().BeFalse();
            verdict.AllowsRansom.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_cruel_captor_with_only_a_light_grudge_WHEN_resolving_release_THEN_they_can_still_be_talked_round()
        {
            var verdict = CaptorReleasePolicy.Resolve(0, CaptorReleasePolicy.LightGrudgeCeiling - 1, false);

            verdict.AllowsPersuasion.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_merciful_captor_with_a_moderate_grudge_WHEN_resolving_release_THEN_mercy_keeps_persuasion_open()
        {
            // Mercy carries persuasion even past the light-grudge ceiling, as long as it is below a vendetta.
            var verdict = CaptorReleasePolicy.Resolve(CaptorReleasePolicy.MercyPersuasionFloor, CaptorReleasePolicy.VendettaGrudgeFloor - 1, false);

            verdict.AllowsPersuasion.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_grudge_one_point_below_the_vendetta_floor_WHEN_resolving_release_THEN_ransom_is_still_open()
        {
            var verdict = CaptorReleasePolicy.Resolve(0, CaptorReleasePolicy.VendettaGrudgeFloor - 1, false);

            verdict.RefusesAll.Should().BeFalse();
            verdict.AllowsRansom.Should().BeTrue();
        }
    }
}
