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
        // Kinship must be checked BEFORE the relation score: a spouse mid-quarrel can carry a deeply
        // negative score while still being family. Reading the number first would have the NPC call
        // their own wife or husband a bitter enemy.
        [Test]
        public void GIVEN_a_spouse_WHEN_categorizing_THEN_kinship_wins_over_any_relation_score()
        {
            //Arrange — kin is named as kin even if the stored relation would read otherwise.
            ThirdPartyDescriptor.Categorize(-50, isSpouse: true, false, false, false, sameClan: false)
                .Should().Be("your spouse");
        }

        // Pins that each kin branch (father/mother/child) resolves to its own label rather than
        // falling through to a lower tier; Categorize checks all four kinships before any relation
        // math runs, and this fixes that each one is actually wired, not just the spouse case above.
        [Test]
        public void GIVEN_a_parent_or_child_WHEN_categorizing_THEN_each_kinship_is_named()
        {
            ThirdPartyDescriptor.Categorize(0, false, isFather: true, false, false, false).Should().Be("your father");
            ThirdPartyDescriptor.Categorize(0, false, false, isMother: true, false, false).Should().Be("your mother");
            ThirdPartyDescriptor.Categorize(0, false, false, false, isChild: true, false).Should().Be("your child");
        }

        // CloseThreshold (30) is the ceiling where "a friend" prose upgrades to "a very close friend";
        // pinning it keeps the two friendly tiers from bleeding into each other if the constant is retuned.
        [Test]
        public void GIVEN_deep_regard_WHEN_categorizing_THEN_they_are_a_very_close_friend()
        {
            ThirdPartyDescriptor.Categorize(35, false, false, false, false, false)
                .Should().Be("a very close friend (deep trust)");
        }

        // FriendThreshold (+10) is INCLUSIVE: one point below reads as a mere acquaintance. Fixes the
        // exact line a future retune of FriendThreshold must not silently move.
        [Test]
        public void GIVEN_warmth_at_the_friend_threshold_WHEN_categorizing_THEN_they_are_a_friend()
        {
            //Arrange — the threshold is inclusive at +10.
            ThirdPartyDescriptor.Categorize(10, false, false, false, false, false)
                .Should().Be("a friend (genuine regard)");
        }

        // Mirrors the CloseThreshold test above on the negative side: BitterThreshold (-30) is the floor
        // where "an enemy" prose escalates to "a bitter enemy".
        [Test]
        public void GIVEN_deep_hostility_WHEN_categorizing_THEN_they_are_a_bitter_enemy()
        {
            ThirdPartyDescriptor.Categorize(-30, false, false, false, false, false)
                .Should().Be("a bitter enemy (resentment)");
        }

        // -10 is EnemyThreshold. Categorize and DescribeRelation deliberately read this same score two
        // different ways (an "enemy" label here, "quiet distrust" prose there); this pins Categorize's
        // own cut so the two boundaries can never be confused for one shared threshold.
        [Test]
        public void GIVEN_mild_hostility_at_the_enemy_threshold_WHEN_categorizing_THEN_they_are_an_enemy()
        {
            //Arrange — -10 is an enemy, and DescribeRelation reads it as quiet distrust (boundaries differ by design).
            ThirdPartyDescriptor.Categorize(-10, false, false, false, false, false)
                .Should().Be("an enemy (quiet distrust)");
        }

        // Clan membership only surfaces in the gap between the enemy and friend thresholds; outside that
        // gap the relation label wins outright, so a clanmate the NPC genuinely dislikes still reads as
        // an enemy, not as kin papering over the friction.
        [Test]
        public void GIVEN_a_neutral_clanmate_WHEN_categorizing_THEN_clan_membership_is_named()
        {
            //Arrange — same clan only surfaces between the enemy and friend thresholds.
            ThirdPartyDescriptor.Categorize(0, false, false, false, false, sameClan: true)
                .Should().Be("a member of your clan");
        }

        // Player report: two gang leaders sharing a turf were hallucinated into friendships/alliances by
        // the model. This rule tells the NPC the truth about a same-settlement rival before the neutral
        // fallback ("not personally known") can paper over the rivalry.
        [Test]
        public void GIVEN_two_same_turf_gang_leaders_at_neutral_standing_WHEN_categorizing_THEN_they_are_named_rivals_not_strangers()
        {
            ThirdPartyDescriptor.Categorize(0, false, false, false, false, sameClan: false, areRivalGangLeaders: true)
                .Should().Be("a rival gang leader (a competitor for the same turf, not a friend)");
        }

        // Even a rival gang leader the NPC happens to like (relation 40, above the friend threshold) must
        // still read as a rival: proves the override actually beats the FRIENDLY branch, not merely the
        // neutral fallback covered by the test above.
        [Test]
        public void GIVEN_rival_gang_leaders_who_happen_to_hold_regard_WHEN_categorizing_THEN_the_rivalry_overrides_the_friendly_label()
        {
            ThirdPartyDescriptor.Categorize(40, false, false, false, false, sameClan: false, areRivalGangLeaders: true)
                .Should().Contain("rival gang leader");
        }

        // A rival who is ALSO a genuine enemy (relation -40, past EnemyThreshold) keeps the stronger
        // "enemy" wording rather than being downgraded to the generic rival phrase: the code comment
        // in ThirdPartyDescriptor.Categorize is explicit that the rivalry note never weakens a real enemy.
        [Test]
        public void GIVEN_rival_gang_leaders_who_are_genuine_enemies_WHEN_categorizing_THEN_the_stronger_enemy_label_is_kept()
        {
            ThirdPartyDescriptor.Categorize(-40, false, false, false, false, sameClan: false, areRivalGangLeaders: true)
                .Should().Contain("enemy");
        }

        // A relation that is merely nonzero (below every named tier, no clan) must still read as a named
        // acquaintance rather than falling all the way to "not personally known": the latter would be a
        // factual lie about a tie that does exist, however faint.
        [Test]
        public void GIVEN_a_faint_acquaintance_outside_any_clan_WHEN_categorizing_THEN_they_are_an_acquaintance()
        {
            //Arrange — non-zero but below the thresholds, and not a clanmate.
            ThirdPartyDescriptor.Categorize(5, false, false, false, false, sameClan: false)
                .Should().Be("an acquaintance (neutral)");
        }

        // The floor of the whole ladder: relation exactly 0, no kinship, no clan. Anything looser here
        // would have the NPC claim some tie to someone they have never actually interacted with.
        [Test]
        public void GIVEN_a_stranger_WHEN_categorizing_THEN_they_are_not_personally_known()
        {
            ThirdPartyDescriptor.Categorize(0, false, false, false, false, false)
                .Should().Be("not personally known to you");
        }

        // Pins every DescribeRelation boundary in one pass, including the asymmetric edges (-9 still
        // "neutral", -10 already "quiet distrust"), so a future retune cannot silently shift one tier's
        // wording onto a score that belongs to its neighbour.
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

        // Physical presence is the strongest possible knowledge signal and must win outright, ahead of
        // any social tie, or two people standing in the same room could still be described as not
        // knowing where each other are.
        [Test]
        public void GIVEN_two_people_in_the_same_settlement_WHEN_judging_knowledge_THEN_it_is_certain()
        {
            ThirdPartyDescriptor.KnowledgeOf(isPresentTogether: true, false, -50, false)
                .Should().Be(KnowledgeTier.Present);
        }

        // Family and clan know roughly where each other are even through a bad patch (relation -40):
        // KnowledgeOf reads the STRUCTURAL tie, not the current mood, so a feuding sibling does not
        // suddenly become untraceable to their own family.
        [Test]
        public void GIVEN_close_kin_or_clan_WHEN_judging_knowledge_THEN_it_is_confident_regardless_of_relation()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, isCloseKinOrClan: true, -40, false)
                .Should().Be(KnowledgeTier.Close);
        }

        // A friend without kinship/clan/presence is capped at hearsay-level knowledge, never the
        // confident tier: keeps the NPC from sounding omniscient about someone they merely like.
        [Test]
        public void GIVEN_only_a_friendly_bond_WHEN_judging_knowledge_THEN_it_is_recent_hearsay()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, false, 15, false).Should().Be(KnowledgeTier.Friend);
        }

        // The weakest tie that still knows anything: a faction-mate with zero personal relation gets only
        // rough military hearsay, never a specific place, so soldiers do not track civilians they've
        // never personally met.
        [Test]
        public void GIVEN_no_personal_tie_but_a_shared_faction_WHEN_judging_knowledge_THEN_only_rough_movements_are_known()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, false, 0, sameFaction: true).Should().Be(KnowledgeTier.Faction);
        }

        // The floor: no tie of any kind yields KnowledgeTier.None, so WhereaboutsClause below has nothing
        // to build a sentence from and the NPC stays honestly silent instead of inventing a location.
        [Test]
        public void GIVEN_a_distant_stranger_WHEN_judging_knowledge_THEN_nothing_is_known()
        {
            ThirdPartyDescriptor.KnowledgeOf(false, false, 0, false).Should().Be(KnowledgeTier.None);
        }

        // Anti-fabrication floor: KnowledgeTier.None must return null so the caller renders nothing at
        // all, never a placeholder guess. This is exactly the omniscience failure the whereabouts feature
        // (ContextualNameResolver's "Option A - close ties only") was built to avoid.
        [Test]
        public void GIVEN_no_knowledge_WHEN_building_a_whereabouts_clause_THEN_there_is_none()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.None, "at Pravend", "Pravend").Should().BeNull();
        }

        // The one tier that can name a place with certainty ("here with you") rather than hearsay: it is
        // not gossip, it's the NPC's own eyes, so the phrasing must not hedge like the other tiers below.
        [Test]
        public void GIVEN_present_together_WHEN_building_a_whereabouts_clause_THEN_it_names_the_shared_place()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Present, null, "Pravend")
                .Should().Be("they are here with you, at Pravend");
        }

        // A social tie can outlive the engine's ability to say WHERE (place is null, e.g. no settlement
        // and no readable activity): the NPC must admit not knowing rather than going silent or inventing
        // a place, the same fabrication risk the None-tier test above guards from the other direction.
        [Test]
        public void GIVEN_a_known_person_whose_place_is_unreadable_WHEN_building_a_clause_THEN_honest_uncertainty_is_voiced()
        {
            ThirdPartyDescriptor.WhereaboutsClause(KnowledgeTier.Close, null, null)
                .Should().Be("you could not say where they are just now");
        }

        // Every non-present tier is phrased as hearsay of graded confidence (Close: "last you knew";
        // Friend: "you last heard"; Faction: "word among your own faction"), so the wording itself signals
        // the player how much to trust it, per ContextualNameResolver's anti-fabrication design.
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

        // The mirror case of the Redeye Gunnor report above: pins the male branch too, so the fix for the
        // misgendering bug cannot regress by only covering the female path it was reported on.
        [Test]
        public void GIVEN_a_man_WHEN_naming_his_gender_THEN_it_reads_a_man()
        {
            ThirdPartyDescriptor.GenderNoun(isFemale: false).Should().Be("a man");
        }
    }
}
