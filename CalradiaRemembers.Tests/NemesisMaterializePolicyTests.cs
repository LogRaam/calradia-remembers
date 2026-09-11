// Code written by Gabriel Mailhot, 08/07/2026.
// Nemesis pillar, ACTIVE HUNT (map-presence mechanic): the nemesis keeps NO standing map party (nothing for
// the AI to attrit); he only materializes when the player engages a bandit party of his culture close enough
// to his anchored region. This is the pure proximity gate that decides "close enough".
//
// WHY IT MATTERS: NemesisBehavior's FindHuntableNemesisNear consumes this to decide which engaged bandit
// parties are even eligible to roll the reveal (NemesisRevealPolicy handles the chance itself). Too generous
// a radius and a nemesis anchored to one region would haunt bands anywhere on the map, breaking "regional";
// too tight and a player standing right on his region's border could never trigger a reveal at all.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisMaterializePolicyTests
    {
        // Baseline positive: a party well within the TUNE radius must be eligible, or a nemesis could never
        // materialize even in the encounters that are supposed to be his.
        [Test]
        public void GIVEN_a_party_inside_the_region_radius_WHEN_checking_THEN_it_can_be_his()
        {
            NemesisMaterializePolicy.WithinRegion(new MapPoint(100f, 100f), new MapPoint(150f, 100f), NemesisMaterializePolicy.RegionMatchRadius)
                .Should().BeTrue();
        }

        // The whole point of the pillar being REGIONAL: a bandit party on the other side of the map must not
        // count as this nemesis's, or "you recross Ranulf" would stop meaning anything geographically.
        [Test]
        public void GIVEN_a_party_beyond_the_region_radius_WHEN_checking_THEN_it_is_not_his()
        {
            NemesisMaterializePolicy.WithinRegion(new MapPoint(0f, 0f), new MapPoint(1000f, 1000f), NemesisMaterializePolicy.RegionMatchRadius)
                .Should().BeFalse();
        }

        // Per the doc comment, callers with an UNRESOLVABLE anchor (a culture-key or "wilds" region, not a real
        // settlement) are meant to skip this check and match on culture alone; a zero/negative radius must
        // therefore fail closed rather than accidentally matching by degenerate math.
        [Test]
        public void GIVEN_a_non_positive_radius_WHEN_checking_THEN_it_never_matches()
        {
            NemesisMaterializePolicy.WithinRegion(new MapPoint(0f, 0f), new MapPoint(0f, 0f), 0f).Should().BeFalse();
        }

        // Boundary inclusive at zero distance: the party standing exactly on the anchor is the most obvious
        // "his territory" case there is, so it must not be excluded by a strict-less-than comparison.
        [Test]
        public void GIVEN_a_party_exactly_on_the_anchor_WHEN_checking_THEN_it_matches()
        {
            NemesisMaterializePolicy.WithinRegion(new MapPoint(50f, 50f), new MapPoint(50f, 50f), NemesisMaterializePolicy.RegionMatchRadius)
                .Should().BeTrue();
        }
    }
}
