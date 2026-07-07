// Code written by Gabriel Mailhot, 06/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisBountyRewardPolicyTests
    {
        // ── ComputeGold ──────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_nemesis_never_recaptured_nor_encountered_WHEN_computing_gold_THEN_it_is_the_base_amount()
        {
            NemesisBountyRewardPolicy.ComputeGold(0, 0).Should().Be(NemesisBountyRewardPolicy.BaseGold);
        }

        [Test]
        public void GIVEN_prior_recaptures_WHEN_computing_gold_THEN_each_one_adds_its_own_share()
        {
            int gold = NemesisBountyRewardPolicy.ComputeGold(2, 0);

            gold.Should().Be(NemesisBountyRewardPolicy.BaseGold + 2 * NemesisBountyRewardPolicy.GoldPerRecapture);
        }

        [Test]
        public void GIVEN_prior_encounters_with_no_recapture_WHEN_computing_gold_THEN_each_one_adds_its_own_smaller_share()
        {
            int gold = NemesisBountyRewardPolicy.ComputeGold(0, 3);

            gold.Should().Be(NemesisBountyRewardPolicy.BaseGold + 3 * NemesisBountyRewardPolicy.GoldPerEncounter);
        }

        [Test]
        public void GIVEN_a_highly_notorious_nemesis_WHEN_computing_gold_THEN_it_is_capped_at_the_ceiling()
        {
            NemesisBountyRewardPolicy.ComputeGold(1000, 1000).Should().Be(NemesisBountyRewardPolicy.MaxGold);
        }

        [Test]
        public void GIVEN_growing_notoriety_WHEN_computing_gold_THEN_it_never_decreases()
        {
            int previous = 0;
            for (int recaptures = 0; recaptures <= 10; recaptures++)
            {
                int gold = NemesisBountyRewardPolicy.ComputeGold(recaptures, recaptures);
                gold.Should().BeGreaterThanOrEqualTo(previous);
                previous = gold;
            }
        }

        [Test]
        public void GIVEN_negative_counters_WHEN_computing_gold_THEN_they_are_treated_as_zero_not_subtracted()
        {
            NemesisBountyRewardPolicy.ComputeGold(-5, -5).Should().Be(NemesisBountyRewardPolicy.BaseGold);
        }

        // ── ComputeRelation ──────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_nemesis_never_recaptured_WHEN_computing_relation_THEN_it_is_the_base_amount()
        {
            NemesisBountyRewardPolicy.ComputeRelation(0).Should().Be(NemesisBountyRewardPolicy.BaseRelation);
        }

        [Test]
        public void GIVEN_prior_recaptures_WHEN_computing_relation_THEN_each_one_adds_its_own_share()
        {
            NemesisBountyRewardPolicy.ComputeRelation(2)
                .Should().Be(NemesisBountyRewardPolicy.BaseRelation + 2 * NemesisBountyRewardPolicy.RelationPerRecapture);
        }

        [Test]
        public void GIVEN_a_great_many_recaptures_WHEN_computing_relation_THEN_it_is_capped_at_the_ceiling()
        {
            NemesisBountyRewardPolicy.ComputeRelation(1000).Should().Be(NemesisBountyRewardPolicy.MaxRelation);
        }

        [Test]
        public void GIVEN_a_negative_recapture_count_WHEN_computing_relation_THEN_it_is_treated_as_zero()
        {
            NemesisBountyRewardPolicy.ComputeRelation(-3).Should().Be(NemesisBountyRewardPolicy.BaseRelation);
        }
    }
}
