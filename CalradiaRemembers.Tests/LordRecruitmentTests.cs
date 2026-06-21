// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the lord-defection gate: a powerful action (poaching a lord into the player's clan) whose
// three thresholds break in silence — a regression would let anyone be recruited, or no one ever.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LordRecruitmentTests
    {
        [Test]
        public void GIVEN_deep_trust_a_higher_rank_and_a_landless_lord_WHEN_evaluating_THEN_they_can_be_recruited()
        {
            //Arrange
            LordRecruitmentFacts f = Facts(relation: 70, playerTier: 4, lordTier: 2, landless: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
            LordRecruitment.IsEligible(f).Should().BeTrue();
        }

        [Test]
        public void GIVEN_only_a_friendly_bond_WHEN_evaluating_THEN_trust_is_too_low()
        {
            //Arrange — everything else qualifies, but the bond is merely friendly, not deep.
            LordRecruitmentFacts f = Facts(relation: 40, playerTier: 5, lordTier: 1, landless: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.TrustTooLow);
        }

        [Test]
        public void GIVEN_a_bond_exactly_at_the_trust_floor_WHEN_evaluating_THEN_it_passes()
        {
            //Arrange — the threshold is inclusive.
            LordRecruitmentFacts f = Facts(relation: LordRecruitment.MinRelation, playerTier: 3, lordTier: 3, landless: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
        }

        [Test]
        public void GIVEN_the_player_outranked_by_the_lords_house_WHEN_evaluating_THEN_their_rank_is_too_low()
        {
            //Arrange — a duke does not follow a lesser house, however warm the bond.
            LordRecruitmentFacts f = Facts(relation: 80, playerTier: 2, lordTier: 4, landless: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.PlayerRankTooLow);
        }

        [Test]
        public void GIVEN_equal_clan_tiers_WHEN_evaluating_THEN_rank_is_not_a_barrier()
        {
            //Arrange — the gate is "player >= lord", so equal standing passes.
            LordRecruitmentFacts f = Facts(relation: 65, playerTier: 3, lordTier: 3, clanWeak: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
        }

        [Test]
        public void GIVEN_a_comfortable_landed_lord_in_a_strong_house_content_with_their_liege_WHEN_evaluating_THEN_they_will_not_leave()
        {
            //Arrange — trust and rank pass, but nothing makes this lord want to go.
            LordRecruitmentFacts f = Facts(relation: 90, playerTier: 6, lordTier: 2,
                                           landless: false, clanWeak: false, hasLiege: true, relWithLiege: 30);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.LordNotVulnerable);
        }

        [Test]
        public void GIVEN_a_lord_whose_clan_is_broken_WHEN_evaluating_THEN_that_alone_makes_them_recruitable()
        {
            //Arrange — landed but their house is shattered: vulnerability via clan strength.
            LordRecruitmentFacts f = Facts(relation: 65, playerTier: 3, lordTier: 2, landless: false, clanWeak: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
        }

        [Test]
        public void GIVEN_a_lord_estranged_from_their_liege_WHEN_evaluating_THEN_that_estrangement_makes_them_recruitable()
        {
            //Arrange — comfortable otherwise, but they despise the liege they serve.
            LordRecruitmentFacts f = Facts(relation: 65, playerTier: 4, lordTier: 3,
                                           landless: false, clanWeak: false, hasLiege: true, relWithLiege: -30);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
        }

        [Test]
        public void GIVEN_estrangement_exactly_at_the_threshold_WHEN_evaluating_THEN_it_counts()
        {
            //Arrange — the resentment threshold is inclusive.
            LordRecruitmentFacts f = Facts(relation: 65, playerTier: 4, lordTier: 3,
                                           hasLiege: true, relWithLiege: LordRecruitment.LiegeResentmentThreshold);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
        }

        [Test]
        public void GIVEN_a_lord_with_no_liege_but_a_low_stored_liege_relation_WHEN_evaluating_THEN_it_does_not_count_as_vulnerability()
        {
            //Arrange — a landless guard would mask this, so test landed + strong clan + no liege.
            LordRecruitmentFacts f = Facts(relation: 65, playerTier: 4, lordTier: 2,
                                           landless: false, clanWeak: false, hasLiege: false, relWithLiege: -99);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.LordNotVulnerable);
        }

        [Test]
        public void GIVEN_both_trust_and_rank_fail_WHEN_evaluating_THEN_trust_is_reported_first()
        {
            //Arrange — the most fundamental blocker is surfaced first, for clear prompt/debug hinting.
            LordRecruitmentFacts f = Facts(relation: 10, playerTier: 1, lordTier: 5, landless: true);

            //Act/Assert
            LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.TrustTooLow);
        }

        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_THEN_it_fails_closed()
        {
            //Act/Assert — never default a powerful action open.
            LordRecruitment.IsEligible(null).Should().BeFalse();
        }

        #region private

        private static LordRecruitmentFacts Facts(
            int relation,
            int playerTier,
            int lordTier,
            bool landless = false,
            bool clanWeak = false,
            bool hasLiege = false,
            int relWithLiege = 0)
            => new LordRecruitmentFacts
            {
                Relation                = relation,
                PlayerClanTier          = playerTier,
                LordClanTier            = lordTier,
                LordIsLandless          = landless,
                LordClanWeakOrDestroyed = clanWeak,
                HasLiege                = hasLiege,
                RelationWithLiege       = relWithLiege
            };

        #endregion
    }
}
