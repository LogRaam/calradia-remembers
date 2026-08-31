// Code written by Gabriel Mailhot, 31/08/2026.
// Guards JealousyHearingPolicy.WithinEarshot, the earshot gate JealousyBehavior.EvaluateAwareness adds
// ON TOP of WorldEventStore.IsAwareOf. Player report: the spouse seemed "psychic", learning of a
// liaison instantly from the far end of the map because the shared rumour scoring's same-faction
// bonus ignores distance. This gate pins the jealousy-specific truth: word must have reached their
// circles AND they must be close enough to where it happened — or be riding beside the player, in
// which case the affair happens under their nose and no distance argument applies.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class JealousyHearingPolicyTests
    {
        // A jealous party riding beside the player watches the courtship happen: they always "hear",
        // whatever the distance to the liaison's recorded place (even an unreadable one).
        [Test]
        public void GIVEN_a_party_beside_the_player_WHEN_gating_hearing_THEN_they_always_hear()
        {
            JealousyHearingPolicy.WithinEarshot(distanceUnits: null, besidePlayer: true)
                                 .Should().BeTrue("the affair happens under their nose");
            JealousyHearingPolicy.WithinEarshot(distanceUnits: 5000f, besidePlayer: true)
                                 .Should().BeTrue();
        }

        // Within earshot of the liaison's location, the rumour is audible: at the exact boundary
        // (MaxHearingDistanceUnits = 350) and comfortably inside it.
        [Test]
        public void GIVEN_a_party_within_earshot_WHEN_gating_hearing_THEN_they_hear()
        {
            JealousyHearingPolicy.WithinEarshot(distanceUnits: 120f, besidePlayer: false)
                                 .Should().BeTrue();
            JealousyHearingPolicy.WithinEarshot(distanceUnits: 350f, besidePlayer: false)
                                 .Should().BeTrue("the boundary is inclusive");
        }

        // The anti-"psychic spouse" case itself: past MaxHearingDistanceUnits, even a same-faction
        // spouse whose rumour score crossed the awareness threshold has NOT plausibly heard yet.
        [Test]
        public void GIVEN_a_party_beyond_earshot_WHEN_gating_hearing_THEN_they_have_not_heard()
        {
            JealousyHearingPolicy.WithinEarshot(distanceUnits: 351f, besidePlayer: false)
                                 .Should().BeFalse("word of mouth does not cross the whole map");
        }

        // An unreadable distance (position unknown) fails safe toward NOT hearing: better a rumour
        // that arrives late than a spouse who knows instantly from anywhere.
        [Test]
        public void GIVEN_an_unknown_distance_WHEN_gating_hearing_THEN_they_have_not_heard()
        {
            JealousyHearingPolicy.WithinEarshot(distanceUnits: null, besidePlayer: false)
                                 .Should().BeFalse("unknown position fails safe toward ignorance");
        }
    }
}
