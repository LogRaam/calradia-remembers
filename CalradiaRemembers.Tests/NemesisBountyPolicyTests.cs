// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 3 (bounty missions, the world hook): the pure eligibility gate deciding whether
// a given lord may offer the player a bounty on a given tracked nemesis.
//
// WHY IT MATTERS: this gates NemesisBountyDialogFlow's offer on the hero_main_options hub. RegionMatches is
// the RATIFIED primary rule (the issuing lord's own region must match the nemesis's anchor), checked in a
// deliberate priority order (home settlement, then current location, then culture as a last resort) so the
// lord who plausibly suffered from this band is the one offering the bounty. IsEligibleIssuer additionally
// blocks a dead nemesis and a duplicate active bounty (tracked store-wide via HasActiveBountyFor). Get either
// wrong and lords across the map spam bounties on a nemesis who never troubled their lands, or the player
// can farm the same bounty from several lords at once.

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

        // Priority 1 (doc comment): home settlement is "the strongest signal a lord's lands were troubled by
        // this band", so it must match even when the lord's current location and culture would also qualify.
        [Test]
        public void GIVEN_the_lords_home_settlement_matches_the_nemesis_region_WHEN_checking_THEN_it_matches()
        {
            NemesisBountyPolicy.RegionMatches("town_a", "town_b", "empire", "town_a").Should().BeTrue();
        }

        // Priority 2: falls back to wherever the lord currently stands, covering a landless wanderer-lord or
        // one away from their fief who would otherwise never qualify to offer a bounty at all.
        [Test]
        public void GIVEN_the_lord_has_no_home_settlement_but_stands_in_the_nemesis_region_WHEN_checking_THEN_it_matches()
        {
            NemesisBountyPolicy.RegionMatches(null, "town_a", "empire", "town_a").Should().BeTrue();
        }

        // Priority 3, the last resort: culture, mirroring the same fallback NemesisIdentity.RegionId itself
        // uses when a band had no home settlement to anchor to in the first place.
        [Test]
        public void GIVEN_no_settlement_keys_but_the_lords_culture_matches_the_nemesis_region_WHEN_checking_THEN_it_matches()
        {
            NemesisBountyPolicy.RegionMatches(null, null, "empire", "empire").Should().BeTrue();
        }

        // Baseline negative: three non-matching keys must never accidentally satisfy any of the three priority checks.
        [Test]
        public void GIVEN_none_of_the_lords_keys_match_WHEN_checking_THEN_it_does_not_match()
        {
            NemesisBountyPolicy.RegionMatches("town_a", "town_b", "empire", "vlandia").Should().BeFalse();
        }

        // A missing nemesis RegionId must never match by falling through to an empty-string comparison; a
        // mint bug that leaves RegionId unset must fail closed (no bounty offer anywhere), not fail open (every lord qualifies).
        [Test]
        public void GIVEN_the_nemesis_has_no_region_id_WHEN_checking_THEN_it_never_matches()
        {
            NemesisBountyPolicy.RegionMatches("town_a", "town_a", "empire", null).Should().BeFalse();
            NemesisBountyPolicy.RegionMatches("town_a", "town_a", "empire", "").Should().BeFalse();
        }

        // A lord with none of the three keys resolved must not match by accident (e.g. two nulls comparing
        // equal); each priority check is guarded by IsNullOrEmpty specifically to prevent that.
        [Test]
        public void GIVEN_the_lord_has_no_keys_at_all_WHEN_checking_THEN_it_does_not_match()
        {
            NemesisBountyPolicy.RegionMatches(null, null, null, "town_a").Should().BeFalse();
        }

        // ── IsEligibleIssuer ─────────────────────────────────────────────────

        // Baseline positive: all three ratified conditions hold, so the offer must actually be allowed.
        [Test]
        public void GIVEN_a_living_nemesis_with_no_active_bounty_and_a_matching_region_WHEN_evaluating_THEN_the_lord_is_eligible()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: true, bountyAlreadyActive: false, regionMatches: true).Should().BeTrue();
        }

        // A bounty cannot be offered on a nemesis who no longer exists to capture or kill; without this
        // check, a dead nemesis could still spawn quest offers pointing at nothing.
        [Test]
        public void GIVEN_the_nemesis_is_already_dead_WHEN_evaluating_THEN_the_lord_is_never_eligible()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: false, bountyAlreadyActive: false, regionMatches: true).Should().BeFalse();
        }

        // Bounties are tracked store-wide (HasActiveBountyFor per ROADMAP), so a second lord must never stack
        // a duplicate bounty on the same nemesis, or the player could farm one nemesis for multiple rewards.
        [Test]
        public void GIVEN_a_bounty_on_him_is_already_active_somewhere_WHEN_evaluating_THEN_no_second_lord_may_offer_one()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: true, bountyAlreadyActive: true, regionMatches: true).Should().BeFalse();
        }

        // The region gate is one of the three required conditions, not merely advisory: a lord whose lands
        // this band never touched must not be able to offer a bounty regardless of the other two facts.
        [Test]
        public void GIVEN_the_lords_region_does_not_match_the_nemesis_WHEN_evaluating_THEN_the_lord_is_not_eligible()
        {
            NemesisBountyPolicy.IsEligibleIssuer(nemesisAlive: true, bountyAlreadyActive: false, regionMatches: false).Should().BeFalse();
        }
    }
}
