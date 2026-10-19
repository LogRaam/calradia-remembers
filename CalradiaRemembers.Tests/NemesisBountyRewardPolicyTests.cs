// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 3 (bounty missions): the pure gold/relation math for a lord's bounty on a
// tracked nemesis, scaled by his notoriety (RecaptureCount, TimesEncountered).
//
// WHY IT MATTERS: this feeds QuestService.IssueNemesisBounty's reward, paid out on report-back exactly like
// any other verified quest. Both counters are clamped to match QuestFactory's own MaxRewardGold/MaxRewardRelation
// ceilings, so a long-lived, many-times-recaptured nemesis can never break the quest reward economy by paying
// out more than an ordinary quest ever could. Get the clamp wrong and a notorious nemesis becomes an infinite
// gold farm; get the base/scaling wrong and a genuinely fearsome rival pays out the same pittance as a nobody.

#region

using CalradiaRemembers.Logic.Nemesis;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisBountyRewardPolicyTests
    {
        // ── ComputeGold ──────────────────────────────────────────────────────

        // A nemesis fresh on the roster still needs a real reward on offer, or the bounty would be worthless
        // the moment it is first issued.
        [Test]
        public void GIVEN_a_nemesis_never_recaptured_nor_encountered_WHEN_computing_gold_THEN_it_is_the_base_amount()
        {
            NemesisBountyRewardPolicy.ComputeGold(0, 0).Should().Be(NemesisBountyRewardPolicy.BaseGold);
        }

        // RecaptureCount is called out (in the production doc comment) as the STRONGEST notoriety signal,
        // since it is the counter of times he has personally beaten the player.
        [Test]
        public void GIVEN_prior_recaptures_WHEN_computing_gold_THEN_each_one_adds_its_own_share()
        {
            int gold = NemesisBountyRewardPolicy.ComputeGold(2, 0);

            gold.Should().Be(NemesisBountyRewardPolicy.BaseGold + 2 * NemesisBountyRewardPolicy.GoldPerRecapture);
        }

        // TimesEncountered is the SOFTER signal (mere presence behind an encounter, not a personal beating),
        // so it must add its own, smaller per-unit share rather than being conflated with recapture notoriety.
        [Test]
        public void GIVEN_prior_encounters_with_no_recapture_WHEN_computing_gold_THEN_each_one_adds_its_own_smaller_share()
        {
            int gold = NemesisBountyRewardPolicy.ComputeGold(0, 3);

            gold.Should().Be(NemesisBountyRewardPolicy.BaseGold + 3 * NemesisBountyRewardPolicy.GoldPerEncounter);
        }

        // The ceiling exists specifically so a long-lived nemesis never breaks the quest reward economy
        // (matches QuestFactory's own MaxRewardGold cap); without it, notoriety alone could outpay every
        // other quest type in the game.
        [Test]
        public void GIVEN_a_highly_notorious_nemesis_WHEN_computing_gold_THEN_it_is_capped_at_the_ceiling()
        {
            NemesisBountyRewardPolicy.ComputeGold(1000, 1000).Should().Be(NemesisBountyRewardPolicy.MaxGold);
        }

        // A more infamous nemesis must never pay out LESS than a lesser one; a dip anywhere on the curve
        // would make the bounty economy feel arbitrary rather than notoriety-driven.
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

        // Defensive: a corrupt or uninitialized counter must never SUBTRACT from the base reward, which
        // multiplication by a negative count would otherwise do.
        [Test]
        public void GIVEN_negative_counters_WHEN_computing_gold_THEN_they_are_treated_as_zero_not_subtracted()
        {
            NemesisBountyRewardPolicy.ComputeGold(-5, -5).Should().Be(NemesisBountyRewardPolicy.BaseGold);
        }

        // ── ComputeRelation ──────────────────────────────────────────────────

        // Same floor guarantee as gold: even a first-ever bounty must offer a real, non-zero relation gain.
        [Test]
        public void GIVEN_a_nemesis_never_recaptured_WHEN_computing_relation_THEN_it_is_the_base_amount()
        {
            NemesisBountyRewardPolicy.ComputeRelation(0).Should().Be(NemesisBountyRewardPolicy.BaseRelation);
        }

        // Relation scales on RecaptureCount ALONE (per the doc comment, "the personal-grudge signal"), unlike
        // gold which also reads TimesEncountered; this is the deliberate asymmetry between the two rewards.
        [Test]
        public void GIVEN_prior_recaptures_WHEN_computing_relation_THEN_each_one_adds_its_own_share()
        {
            NemesisBountyRewardPolicy.ComputeRelation(2)
                .Should().Be(NemesisBountyRewardPolicy.BaseRelation + 2 * NemesisBountyRewardPolicy.RelationPerRecapture);
        }

        // The ceiling matches QuestFactory's own MaxRewardRelation, so a nemesis bounty can never outstrip
        // the relation an ordinary quest would ever pay.
        [Test]
        public void GIVEN_a_great_many_recaptures_WHEN_computing_relation_THEN_it_is_capped_at_the_ceiling()
        {
            NemesisBountyRewardPolicy.ComputeRelation(1000).Should().Be(NemesisBountyRewardPolicy.MaxRelation);
        }

        // Defensive, mirrors the gold guard: a negative recapture count must never subtract relation below the base.
        [Test]
        public void GIVEN_a_negative_recapture_count_WHEN_computing_relation_THEN_it_is_treated_as_zero()
        {
            NemesisBountyRewardPolicy.ComputeRelation(-3).Should().Be(NemesisBountyRewardPolicy.BaseRelation);
        }
    }
}
