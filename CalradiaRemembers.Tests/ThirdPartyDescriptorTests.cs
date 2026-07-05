// Code written by Gabriel Mailhot, 20/06/2026.
// Protects how an NPC describes (and locates) third parties the player names — relation thresholds
// and the kin/clan/faction gating of gossip. These run on every name-bearing message and break in
// silence: a drift would have lords mislabel friends as strangers, or omnisciently place everyone.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ThirdPartyDescriptorTests
    {
        [Test]
        public void GIVEN_a_spouse_WHEN_categorizing_THEN_kinship_wins_over_any_relation_score()
        {
            //Arrange — kin is named as kin even if the stored relation would read otherwise.
            ThirdPartyDescriptor.Categorize(-50, isSpouse: true, false, false, false, sameClan: false)
                .Should().Be("your spouse");
        }

        [Test]
        public void GIVEN_a_parent_or_child_WHEN_categorizing_THEN_each_kinship_is_named()
        {
            ThirdPartyDescriptor.Categorize(0, false, isFather: true, false, false, false).Should().Be("your father");
            ThirdPartyDescriptor.Categorize(0, false, false, isMother: true, false, false).Should().Be("your mother");
            ThirdPartyDescriptor.Categorize(0, false, false, false, isChild: true, false).Should().Be("your child");
        }

        [Test]
        public void GIVEN_deep_regard_WHEN_categorizing_THEN_they_are_a_very_close_friend()
        {
            ThirdPartyDescriptor.Categorize(35, false, false, false, false, false)
                .Should().Be("a very close friend (deep trust)");
        }

        [Test]
        public void GIVEN_warmth_at_the_friend_threshold_WHEN_categorizing_THEN_they_are_a_friend()
        {
            //Arrange — the threshold is inclusive at +10.
            ThirdPartyDescriptor.Categorize(10, false, false, false, false, false)
                .Should().Be("a friend (genuine regard)");
        }

        [Test]
        public void GIVEN_deep_hostility_WHEN_categorizing_THEN_they_are_a_bitter_enemy()
        {
            ThirdPartyDescriptor.Categorize(-30, false, false, false, false, false)
                .Should().Be("a bitter enemy (resentment)");
        }

        [Test]
        public void GIVEN_mild_hostility_at_the_enemy_threshold_WHEN_categorizing_THEN_they_are_an_enemy()
        {
            //Arrange — -10 is an enemy, and DescribeRelation reads it as quiet distrust (boundaries differ by design).
            ThirdPartyDescriptor.Categorize(-10, false, false, false, false, false)
                .Should().Be("an enemy (quiet distrust)");
        }

        [Test]
        public void GIVEN_a_neutral_clanmate_WHEN_categorizing_THEN_clan_membership_is_named()
        {
            //Arrange — same clan only surfaces between the enemy and friend thresholds.
            ThirdPartyDescriptor.Categorize(0, false, false, false, false, sameClan: true)
                .Should().Be("a member of your clan");
        }

        [Test]
        public void GIVEN_two_same_turf_gang_leaders_at_neutral_standing_WHEN_categorizing_THEN_they_are_named_rivals_not_strangers()
        {
            ThirdPartyDescriptor.Categorize(0, false, false, false, false, sameClan: false, areRivalGangLeaders: true)
                .Should().Be("a rival gang leader (a competitor for the same turf, not a friend)");
        }

        [Test]
        public void GIVEN_rival_gang_leaders_who_happen_to_hold_regard_WHEN_categorizing_THEN_the_rivalry_overrides_the_friendly_label()
        {
            ThirdPartyDescriptor.Categorize(40, false, false, false, false, sameClan: false, areRivalGangLeaders: true)
                .Should().Contain("rival gang leader");
        }

        [Test]
        public void GIVEN_rival_gang_leaders_who_are_genuine_enemies_WHEN_categorizing_THEN_the_stronger_enemy_label_is_kept()
        {
            ThirdPartyDescriptor.Categorize(-40, false, false, false, false, sameClan: false, areRivalGangLeaders: true)
                .Should().Contain("enemy");
        }

        [Test]
        public void GIVEN_a_faint_acquaintance_outside_any_clan_WHEN_categorizing_THEN_they_are_an_acquaintance()
        {
            //Arrange — non-zero but below the thresholds, and not a clanmate.
            ThirdPartyDescriptor.Categorize(5, false, false, false, false, sameClan: false)
                .Should().Be("an acquaintance (neutral)");
        }

        [Test]
        public void GIVEN_a_stranger_WHEN_categorizing_THEN_they_are_not_personally_known()
        {
            ThirdPartyDescriptor.Categorize(0, false, false, false, false, false)
                .Should().Be("not personally known to you");
        }

        [Test]
        public void GIVEN_relation_scores_across_the_range_WHEN_describing_THEN_each_tier_is_named()
        {
            ThirdPartyDescriptor.DescribeRelation(30).Should().Be("deep trust");
            ThirdPartyDescriptor.DescribeRelation(10).Should().Be("genuine regard");
            ThirdPartyDescriptor.DescribeRelation(0).Should().Be("neutral");
            ThirdPartyDescriptor.DescribeRelation(-9).Should().Be("neutral");
            ThirdPartyDescriptor.DescribeRelation(-10).Should().Be("quiet distrust");
            ThirdPartyDescriptor.DescribeRelation(-29).Should().Be("quiet distrust");
            ThirdPartyDescriptor.DescribeRelation(-30).Should().Be("resentment");
        }

        [Test]
        public void GIVEN_two_people_in_the_same_settlement_WHEN_judging_knowledge_THEN_it_is_certain()
        {
            ThirdPartyDescriptor.KnowledgeOf(isPresentTogether: true, false, -50, false)
                .Should().Be(KnowledgeTier.Present);
        }

        [Test]
        public void GIVEN_close_kin_or_clan_WHEN_judging_knowledge_THEN_it_is_confident_regardless_of_relation()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, isCloseKinOrClan: true, -40, false)
                .Should().Be(KnowledgeTier.Close);
        }

        [Test]
        public void GIVEN_only_a_friendly_bond_WHEN_judging_knowledge_THEN_it_is_recent_hearsay()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, false, 15, false).Should().Be(KnowledgeTier.Friend);
        }

        [Test]
        public void GIVEN_no_personal_tie_but_a_shared_faction_WHEN_judging_knowledge_THEN_only_rough_movements_are_known()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, false, 0, sameFaction: true).Should().Be(KnowledgeTier.Faction);
        }

        [Test]
        public void GIVEN_a_distant_stranger_WHEN_judging_knowledge_THEN_nothing_is_known()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, false, 0, false).Should().Be(KnowledgeTier.None);
        }

        [Test]
        public void GIVEN_no_knowledge_WHEN_building_a_whereabouts_clause_THEN_there_is_none()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.None, "at Pravend", "Pravend").Should().BeNull();
        }

        [Test]
        public void GIVEN_present_together_WHEN_building_a_whereabouts_clause_THEN_it_names_the_shared_place()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Present, null, "Pravend")
                .Should().Be("they are here with you, at Pravend");
        }

        [Test]
        public void GIVEN_a_known_person_whose_place_is_unreadable_WHEN_building_a_clause_THEN_honest_uncertainty_is_voiced()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Close, null, null)
                .Should().Be("you could not say where they are just now");
        }

        [Test]
        public void GIVEN_each_knowing_tier_WHEN_building_a_clause_THEN_the_framing_matches_the_tie()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Close, "at Pravend", null)
                .Should().Be("last you knew, they were at Pravend");
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Friend, "at Pravend", null)
                .Should().Be("you last heard they were at Pravend");
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Faction, "at Pravend", null)
                .Should().Be("word among your own faction has them at Pravend");
        }

        [Test]
        public void GIVEN_a_woman_WHEN_naming_her_gender_THEN_it_reads_a_woman()
        {
            // A player reported an NPC "correcting" them that a woman named Redeye Gunnor was a man,
            // because the gender was never stated and the name is ambiguous.
            ThirdPartyDescriptor.GenderNoun(isFemale: true).Should().Be("a woman");
        }

        [Test]
        public void GIVEN_a_man_WHEN_naming_his_gender_THEN_it_reads_a_man()
        {
            ThirdPartyDescriptor.GenderNoun(isFemale: false).Should().Be("a man");
        }
    }
}
