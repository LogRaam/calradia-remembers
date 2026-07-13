// Code written by Gabriel Mailhot, 05/07/2026.
// Captivity fix (ratified 2026-07-05): decides WHETHER a captor can be brought to free the PLAYER at all, via
// two paths, persuasion (talked round) and ransom (gold buys freedom), so the bridge only ever teaches/executes
// a release the captor would actually grant ("the prompt is advice, the bridge is law"). A VENDETTA captor
// (a nemesis, or a grudge at/above the floor) refuses both: they did not take the player to let them buy their
// way out. If this policy is wrong, either a captor holding real blood-hatred can be talked or ransomed free
// (the vendetta becomes hollow), or an ordinary disposed captor is wrongly locked into refusing every release,
// leaving the player stuck with no negotiated way out at all.

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
        // A tracked nemesis wants blood, not gold: even a captor at MAXIMUM mercy (2) must refuse both paths
        // outright, because the vendetta flag is meant to override everything else the captor is otherwise
        // disposed toward. If mercy could still buy a path here, the nemesis pillar's "he wants you, not your
        // coin" premise would be undermined by construction.
        [Test]
        public void GIVEN_a_nemesis_vendetta_WHEN_resolving_release_THEN_the_captor_refuses_every_path()
        {
            var verdict = CaptorReleasePolicy.Resolve(captorMercy: 2, grudgeAgainstPlayer: 0, isNemesisVendetta: true);

            verdict.AllowsPersuasion.Should().BeFalse();
            verdict.AllowsRansom.Should().BeFalse();
            verdict.RefusesAll.Should().BeTrue();
        }

        // Pins the INCLUSIVE boundary of VendettaGrudgeFloor: a grudge landing exactly ON the floor must
        // already count as a vendetta (mercy 2 here still can't rescue it), or a future retune of the floor
        // could silently leave a genuinely blood-hating captor persuadable.
        [Test]
        public void GIVEN_a_heavy_grudge_at_the_vendetta_floor_WHEN_resolving_release_THEN_the_captor_refuses_every_path()
        {
            var verdict = CaptorReleasePolicy.Resolve(2, CaptorReleasePolicy.VendettaGrudgeFloor, false);

            verdict.RefusesAll.Should().BeTrue();
        }

        // The ordinary, expected case for most captors: any non-vendetta captor takes gold, and a merciful one
        // can also be talked round. Without this baseline working, the negotiated-release feature would have
        // no reachable happy path at all.
        [Test]
        public void GIVEN_a_merciful_captor_with_no_grudge_WHEN_resolving_release_THEN_both_persuasion_and_ransom_are_open()
        {
            var verdict = CaptorReleasePolicy.Resolve(CaptorReleasePolicy.MercyPersuasionFloor, 0, false);

            verdict.AllowsPersuasion.Should().BeTrue();
            verdict.AllowsRansom.Should().BeTrue();
        }

        // Ransom is meant to ALWAYS be open short of a vendetta ("any captor takes gold" per the production
        // doc), independent of mercy or grudge; this is the test that would catch a bug coupling ransom to the
        // persuasion condition instead of leaving it unconditional.
        [Test]
        public void GIVEN_a_cruel_captor_with_a_moderate_grudge_WHEN_resolving_release_THEN_persuasion_is_closed_but_ransom_stays_open()
        {
            // Mercy 0 (below the persuasion floor) + a grudge above the light ceiling but below the vendetta floor.
            var verdict = CaptorReleasePolicy.Resolve(0, CaptorReleasePolicy.LightGrudgeCeiling, false);

            verdict.AllowsPersuasion.Should().BeFalse();
            verdict.AllowsRansom.Should().BeTrue();
        }

        // Pins the exclusive boundary of LightGrudgeCeiling: even a captor with ZERO mercy stays persuadable
        // one point below the ceiling, because the OR condition (mercy floor OR light grudge) means either path
        // alone is enough. Paired with the test above, this fixes the exact line where cruelty starts to matter.
        [Test]
        public void GIVEN_a_cruel_captor_with_only_a_light_grudge_WHEN_resolving_release_THEN_they_can_still_be_talked_round()
        {
            var verdict = CaptorReleasePolicy.Resolve(0, CaptorReleasePolicy.LightGrudgeCeiling - 1, false);

            verdict.AllowsPersuasion.Should().BeTrue();
        }

        // Mercy is the OTHER half of that OR condition: it must carry persuasion open even PAST the light-grudge
        // ceiling, all the way up to (but not including) a vendetta. Without this, a merciful captor holding a
        // moderate grudge would be wrongly treated the same as a cruel one.
        [Test]
        public void GIVEN_a_merciful_captor_with_a_moderate_grudge_WHEN_resolving_release_THEN_mercy_keeps_persuasion_open()
        {
            // Mercy carries persuasion even past the light-grudge ceiling, as long as it is below a vendetta.
            var verdict = CaptorReleasePolicy.Resolve(CaptorReleasePolicy.MercyPersuasionFloor, CaptorReleasePolicy.VendettaGrudgeFloor - 1, false);

            verdict.AllowsPersuasion.Should().BeTrue();
        }

        // The far side of the vendetta boundary from the two vendetta tests above: one point BELOW the floor,
        // ransom must still be open. Together the three grudge-floor tests fix the exact line where a captor's
        // grudge tips from "can still be bought off" to "wants blood, not gold".
        [Test]
        public void GIVEN_a_grudge_one_point_below_the_vendetta_floor_WHEN_resolving_release_THEN_ransom_is_still_open()
        {
            var verdict = CaptorReleasePolicy.Resolve(0, CaptorReleasePolicy.VendettaGrudgeFloor - 1, false);

            verdict.RefusesAll.Should().BeFalse();
            verdict.AllowsRansom.Should().BeTrue();
        }
    }
}
