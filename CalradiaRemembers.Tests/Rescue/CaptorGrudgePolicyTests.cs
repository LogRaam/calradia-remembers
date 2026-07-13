// Code written by Gabriel Mailhot, 11/07/2026.
// Rescue pillar: when the player forcibly frees a held prisoner, this policy decides whether the CAPTOR
// who lost that prize ever traces the deed back to the player, how hard the resulting grudge bites, and
// whether the player is told about it. RescueBehavior consumes all four members directly to gate its
// GrudgeSource.PrisonerRescued plant. All four constants are still TUNE (first-pass, not yet play-tested,
// per the production header): these tests pin the CURRENT numbers and their comparison directions so a
// retune is a deliberate, visible change here, not a silent drift. Get the public/private split wrong and
// a quiet field release either always or never gets traced, undermining the "public is easier to trace"
// design the whole policy exists to express.

#region

using CalradiaRemembers.Logic.Rescue;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Rescue
{
    [TestFixture]
    public sealed class CaptorGrudgePolicyTests
    {
        // A public rescue (storming a castle) is easier to trace back to the player than a quiet field
        // release, per the policy's own design intent: pins that DiscoveryChance actually reads the public
        // constant, not the private one, for a public act.
        [Test]
        public void GIVEN_a_public_action_WHEN_reading_discovery_chance_THEN_it_is_the_public_constant()
        {
            CaptorGrudgePolicy.DiscoveryChance(publicAction: true).Should().Be(CaptorGrudgePolicy.PublicDiscoveryChance);
        }

        // Mirrors the public case: a quiet release must read the lower private discovery chance, or a
        // stealthy field rescue would be traced back to the player just as often as a public siege.
        [Test]
        public void GIVEN_a_private_action_WHEN_reading_discovery_chance_THEN_it_is_the_private_constant()
        {
            CaptorGrudgePolicy.DiscoveryChance(publicAction: false).Should().Be(CaptorGrudgePolicy.PrivateDiscoveryChance);
        }

        // RescueBehavior feeds MBRandom.RandomFloat straight into Discovers: pins the strict less-than
        // comparison at the roll just under PublicDiscoveryChance, so a roll this close to the edge still
        // counts as caught.
        [Test]
        public void GIVEN_a_public_action_and_a_roll_just_below_the_public_threshold_WHEN_checking_discovery_THEN_it_is_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.59, publicAction: true).Should().BeTrue();
        }

        // Pins the boundary as EXCLUSIVE: a roll landing exactly on PublicDiscoveryChance must NOT count as
        // discovered (Discovers uses roll01 < chance), the far side of the previous test's line.
        [Test]
        public void GIVEN_a_public_action_and_a_roll_at_the_public_threshold_WHEN_checking_discovery_THEN_it_is_not_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.60, publicAction: true).Should().BeFalse();
        }

        // Same edge, private branch: a roll just under PrivateDiscoveryChance still gets the quiet rescue caught.
        [Test]
        public void GIVEN_a_private_action_and_a_roll_just_below_the_private_threshold_WHEN_checking_discovery_THEN_it_is_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.34, publicAction: false).Should().BeTrue();
        }

        // Same exclusive-boundary guarantee for the private branch: exactly at the threshold, the captor
        // never learns who freed their prisoner.
        [Test]
        public void GIVEN_a_private_action_and_a_roll_at_the_private_threshold_WHEN_checking_discovery_THEN_it_is_not_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.35, publicAction: false).Should().BeFalse();
        }

        // A public loss is not just easier to trace, it is also resented more openly: pins that a
        // discovered public rescue plants the heavier of the two magnitudes RescueBehavior can hand
        // GrudgeBehavior.TryPlant.
        [Test]
        public void GIVEN_a_public_action_WHEN_reading_grudge_magnitude_THEN_it_is_thirty()
        {
            CaptorGrudgePolicy.GrudgeMagnitude(publicAction: true).Should().Be(30);
        }

        // Pins the lighter magnitude for a discovered private rescue: still a real grudge, but softer than
        // an open public humiliation of the captor.
        [Test]
        public void GIVEN_a_private_action_WHEN_reading_grudge_magnitude_THEN_it_is_twenty()
        {
            CaptorGrudgePolicy.GrudgeMagnitude(publicAction: false).Should().Be(20);
        }

        // A public rescue is never a secret grudge (production doc's own wording): the player must be told
        // via GrudgeBehavior.TryPlant's knownToPlayer flag, since the captor's coolness would otherwise be
        // unexplained to the player.
        [Test]
        public void GIVEN_a_public_action_WHEN_checking_if_known_to_player_THEN_it_is_true()
        {
            CaptorGrudgePolicy.KnownToPlayer(publicAction: true).Should().BeTrue();
        }

        // The mirror case: a private rescue stays a private grudge even once discovered, so the player gets
        // no in-fiction warning that this captor now resents them.
        [Test]
        public void GIVEN_a_private_action_WHEN_checking_if_known_to_player_THEN_it_is_false()
        {
            CaptorGrudgePolicy.KnownToPlayer(publicAction: false).Should().BeFalse();
        }
    }
}
