// Code written by Gabriel Mailhot, 25/06/2026.
// Documents the prestige gate on a grown bastard's claim: a child of a grander maternal house follows that
// dynasty; a child of an equal-or-lesser house has motive to claim the father's clan. The flagship case is
// the player siring a child on Empress Rhagaea — the child wants the imperial line, not the player's clan.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardPrestigePolicyTests
    {
        [Test]
        public void GIVEN_a_grander_maternal_house_WHEN_resolving_THEN_the_bastard_follows_the_mothers_dynasty()
        {
            BastardPrestigePolicy.Resolve(maternalPrestige: 2000, paternalPrestige: 500)
                .Should().Be(BastardClaimVerdict.FollowsMotherDynasty);
        }

        [Test]
        public void GIVEN_a_lesser_maternal_house_WHEN_resolving_THEN_the_bastard_presses_a_claim()
        {
            BastardPrestigePolicy.Resolve(maternalPrestige: 300, paternalPrestige: 800)
                .Should().Be(BastardClaimVerdict.PressesClaim);
        }

        [Test]
        public void GIVEN_two_houses_of_equal_standing_WHEN_resolving_THEN_the_bastard_still_has_motive_to_claim()
        {
            //Arrange — equal prestige leaves a motive, so the gate is strictly "maternal greater → no claim".
            BastardPrestigePolicy.Resolve(maternalPrestige: 600, paternalPrestige: 600)
                .Should().Be(BastardClaimVerdict.PressesClaim);
        }

        [Test]
        public void GIVEN_a_ruling_dynasty_WHEN_scoring_THEN_it_towers_over_an_equal_tier_vassal()
        {
            int ruling = BastardPrestigePolicy.PrestigeScore(clanTier: 4, clanRenown: 1000, isRulingClan: true);
            int vassal = BastardPrestigePolicy.PrestigeScore(clanTier: 4, clanRenown: 1000, isRulingClan: false);

            (ruling - vassal).Should().Be(BastardPrestigePolicy.RulingClanBonus);
        }

        [Test]
        public void GIVEN_negative_renown_WHEN_scoring_THEN_it_never_subtracts_from_the_tier()
        {
            //Arrange — a struggling house is still worth at least its tier.
            BastardPrestigePolicy.PrestigeScore(clanTier: 3, clanRenown: -500, isRulingClan: false)
                .Should().Be(BastardPrestigePolicy.PrestigeScore(clanTier: 3, clanRenown: 0, isRulingClan: false));
        }

        [Test]
        public void GIVEN_the_empress_case_WHEN_resolving_THEN_her_child_covets_nothing_of_a_minor_player_clan()
        {
            //Arrange — Rhagaea: a high-tier ruling dynasty; the player: a modest, non-ruling clan.
            int empress = BastardPrestigePolicy.PrestigeScore(clanTier: 6, clanRenown: 3000, isRulingClan: true);
            int player = BastardPrestigePolicy.PrestigeScore(clanTier: 2, clanRenown: 400, isRulingClan: false);

            BastardPrestigePolicy.Resolve(maternalPrestige: empress, paternalPrestige: player)
                .Should().Be(BastardClaimVerdict.FollowsMotherDynasty);
        }
    }
}
