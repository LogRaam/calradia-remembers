// Code written by Gabriel Mailhot, 06/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisBountyPolicyTests
    {
        // ── RegionMatches ────────────────────────────────────────────────────

        [Test]
        public void GIVEN_the_lords_home_settlement_matches_the_nemesis_region_WHEN_checking_THEN_it_matches()
        {
            NemesisBountyPolicy.RegionMatches("town_a", "town_b", "empire", "town_a").Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_lord_has_no_home_settlement_but_stands_in_the_nemesis_region_WHEN_checking_THEN_it_matches()
        {
            NemesisBountyPolicy.RegionMatches(null, "town_a", "empire", "town_a").Should().BeTrue();
        }

        [Test]
        public void GIVEN_no_settlement_keys_but_the_lords_culture_matches_the_nemesis_region_WHEN_checking_THEN_it_matches()
        {
            NemesisBountyPolicy.RegionMatches(null, null, "empire", "empire").Should().BeTrue();
        }

        [Test]
        public void GIVEN_none_of_the_lords_keys_match_WHEN_checking_THEN_it_does_not_match()
        {
            NemesisBountyPolicy.RegionMatches("town_a", "town_b", "empire", "vlandia").Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_nemesis_has_no_region_id_WHEN_checking_THEN_it_never_matches()
        {
            NemesisBountyPolicy.RegionMatches("town_a", "town_a", "empire", null).Should().BeFalse();
            NemesisBountyPolicy.RegionMatches("town_a", "town_a", "empire", "").Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_lord_has_no_keys_at_all_WHEN_checking_THEN_it_does_not_match()
        {
            NemesisBountyPolicy.RegionMatches(null, null, null, "town_a").Should().BeFalse();
        }

        // ── IsEligibleIssuer ─────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_living_nemesis_with_no_active_bounty_and_a_matching_region_WHEN_evaluating_THEN_the_lord_is_eligible()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: true, bountyAlreadyActive: false, regionMatches: true).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_nemesis_is_already_dead_WHEN_evaluating_THEN_the_lord_is_never_eligible()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: false, bountyAlreadyActive: false, regionMatches: true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_bounty_on_him_is_already_active_somewhere_WHEN_evaluating_THEN_no_second_lord_may_offer_one()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: true, bountyAlreadyActive: true, regionMatches: true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_lords_region_does_not_match_the_nemesis_WHEN_evaluating_THEN_the_lord_is_not_eligible()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: true, bountyAlreadyActive: false, regionMatches: false).Should().BeFalse();
        }
    }
}
