// Code written by Gabriel Mailhot, 25/06/2026.
// Documents the prestige gate on a grown bastard's claim: a child of a grander maternal house follows that
// dynasty; a child of an equal-or-lesser house has motive to claim the father's clan. The flagship case is
// the player siring a child on Empress Rhagaea — the child wants the imperial line, not the player's clan.
//
// WHY IT MATTERS: BastardBehavior.ResolveClaimVerdict feeds this into the coming-of-age World-Event/rumour
// (ROADMAP: "ON_COMING_OF_AGE:bastard... makes the trigger characterful, not mechanical"). Without this gate
// EVERY grown bastard would press a claim, even the child of a ruling empress who has no reason to covet a
// minor lord's clan, which is exactly the flagship case this policy was built to get right. Get PrestigeScore
// wrong and the gate misjudges which houses are actually grander, breaking that same distinction quietly.

#region

using CalradiaRemembers.Logic.Bastards;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardPrestigePolicyTests
    {
        // The core rule this whole policy exists for: a grander maternal house means the bastard follows that
        // dynasty and presses no claim, per the design decision recorded 2026-06-25.
        [Test]
        public void GIVEN_a_grander_maternal_house_WHEN_resolving_THEN_the_bastard_follows_the_mothers_dynasty()
        {
            BastardPrestigePolicy.Resolve(maternalPrestige: 2000, paternalPrestige: 500)
                .Should().Be(BastardClaimVerdict.FollowsMotherDynasty);
        }

        // The mirror case: a lesser maternal house leaves the bastard with a genuine motive to claim the
        // father's clan, which is what turns them into "a natural enemy in the making" (per the verdict's own doc).
        [Test]
        public void GIVEN_a_lesser_maternal_house_WHEN_resolving_THEN_the_bastard_presses_a_claim()
        {
            BastardPrestigePolicy.Resolve(maternalPrestige: 300, paternalPrestige: 800)
                .Should().Be(BastardClaimVerdict.PressesClaim);
        }

        // Pins the exact boundary as "maternal STRICTLY greater -> no claim" (the policy's own doc comment):
        // a naive >= comparison here would silently rule out a claim for every equally-matched pair of houses.
        [Test]
        public void GIVEN_two_houses_of_equal_standing_WHEN_resolving_THEN_the_bastard_still_has_motive_to_claim()
        {
            //Arrange — equal prestige leaves a motive, so the gate is strictly "maternal greater → no claim".
            BastardPrestigePolicy.Resolve(maternalPrestige: 600, paternalPrestige: 600)
                .Should().Be(BastardClaimVerdict.PressesClaim);
        }

        // RulingClanBonus (1000) must dominate tier/renown, or a ruling dynasty (like the Empress's) could still
        // score as "no grander" than an equal-tier vassal house, undermining the whole prestige gate's premise.
        [Test]
        public void GIVEN_a_ruling_dynasty_WHEN_scoring_THEN_it_towers_over_an_equal_tier_vassal()
        {
            int ruling = BastardPrestigePolicy.PrestigeScore(clanTier: 4, clanRenown: 1000, isRulingClan: true);
            int vassal = BastardPrestigePolicy.PrestigeScore(clanTier: 4, clanRenown: 1000, isRulingClan: false);

            (ruling - vassal).Should().Be(BastardPrestigePolicy.RulingClanBonus);
        }

        // Defensive per the method's own doc ("a struggling house is still at least its tier"): without the
        // floor, a clan that has fallen into negative renown could score BELOW a clan with no tier at all.
        [Test]
        public void GIVEN_negative_renown_WHEN_scoring_THEN_it_never_subtracts_from_the_tier()
        {
            //Arrange — a struggling house is still worth at least its tier.
            BastardPrestigePolicy.PrestigeScore(clanTier: 3, clanRenown: -500, isRulingClan: false)
                .Should().Be(BastardPrestigePolicy.PrestigeScore(clanTier: 3, clanRenown: 0, isRulingClan: false));
        }

        // The flagship scenario named in this file's own header: end-to-end, PrestigeScore feeding Resolve()
        // must keep the Empress's child from claiming a modest player clan, or the policy fails its own reason
        // for existing.
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
