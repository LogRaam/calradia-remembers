// Code written by Gabriel Mailhot, 06/09/2026.
// ViolenceConsequencePolicy prices what an act of violence costs the player, and it is the half of the pillar that
// keeps the sandbox honest: the reporter who asked for this feature said himself he expected it could be "pretty
// OP". Three failures would each open an exploit. If a deed done in private cost nothing, the optimal play would be
// to get every victim alone and cut throats for free. If a FAILED attempt cost nothing, attempted murder would be a
// free roll to retry forever. And if the tiers flattened, robbing a merchant would weigh the same with the law as
// murdering a lord. These pin the shape of the price, not merely its size.

#region

using CalradiaRemembers.Logic.Violence;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Violence
{
    [TestFixture]
    public sealed class ViolenceConsequencePolicyTests
    {
        private static ViolenceConsequence Cost(ViolentAct act,
            ViolenceVerdict verdict = ViolenceVerdict.Succeeded,
            ViolenceExposure exposure = ViolenceExposure.Alone)
            => ViolenceConsequencePolicy.Resolve(act, verdict, exposure);

        // A forbidden act never happened, so it costs nothing at all: with the lethal toggle off, a narrated murder
        // must not leave the player wanted for a crime they were never allowed to commit.
        [Test]
        public void GIVEN_a_forbidden_act_WHEN_priced_THEN_it_costs_nothing()
        {
            ViolenceConsequence c = Cost(ViolentAct.Kill, ViolenceVerdict.Forbidden);

            c.CrimeRating.Should().Be(0);
            c.RelationHit.Should().Be(0);
            c.RenownLoss.Should().Be(0);
            c.KinGrudgeMagnitude.Should().Be(0);
        }

        // THE anti-exploit rule: even with nobody watching, the law still hears. A living victim accuses you and a
        // body is eventually found, so "get them alone" is an advantage, never a free pass.
        [Test]
        public void GIVEN_nobody_watching_WHEN_priced_THEN_the_law_still_hears_of_it()
        {
            Cost(ViolentAct.Kill).CrimeRating.Should().BeGreaterThan(0);
            Cost(ViolentAct.Rob).CrimeRating.Should().BeGreaterThan(0);
        }

        // Being seen makes it worse, and being seen by guards worse still: this is what keeps isolation meaningful
        // without making it consequence-free.
        [Test]
        public void GIVEN_witnesses_WHEN_priced_THEN_the_crime_grows_with_who_saw_it()
        {
            int alone = Cost(ViolentAct.Assault).CrimeRating;
            int onlookers = Cost(ViolentAct.Assault, exposure: ViolenceExposure.Onlookers).CrimeRating;
            int guarded = Cost(ViolentAct.Assault, exposure: ViolenceExposure.Guarded).CrimeRating;

            onlookers.Should().BeGreaterThan(alone);
            guarded.Should().BeGreaterThan(onlookers);
        }

        // Renown is public standing, so it is only ever charged when someone SAW it. A secret deed shames no one,
        // which is a different axis from the law hearing of it later.
        [Test]
        public void GIVEN_a_deed_done_unseen_WHEN_priced_THEN_no_standing_is_lost_but_it_is_when_seen()
        {
            Cost(ViolentAct.Kill).RenownLoss.Should().Be(0);
            Cost(ViolentAct.Kill, exposure: ViolenceExposure.Onlookers).RenownLoss.Should().BeGreaterThan(0);
        }

        // A failed attempt still costs: you tried, and they know. Without this, attempted murder would be a free
        // roll the player could repeat until it landed.
        [Test]
        public void GIVEN_a_failed_attempt_WHEN_priced_THEN_it_still_costs_but_less_than_the_deed()
        {
            int failed = Cost(ViolentAct.Kill, ViolenceVerdict.Failed).CrimeRating;
            int succeeded = Cost(ViolentAct.Kill).CrimeRating;

            failed.Should().BeGreaterThan(0);
            failed.Should().BeLessThan(succeeded);
        }

        // The tiers must stay ordered: the law weighs a murder above a beating above a theft, or robbing a merchant
        // would be as grave as killing a lord.
        [Test]
        public void GIVEN_the_three_acts_WHEN_priced_THEN_killing_outweighs_beating_outweighs_robbery()
        {
            Cost(ViolentAct.Kill).CrimeRating
                .Should().BeGreaterThan(Cost(ViolentAct.Assault).CrimeRating);
            Cost(ViolentAct.Assault).CrimeRating
                .Should().BeGreaterThan(Cost(ViolentAct.Rob).CrimeRating);
        }

        // A dead victim has no regard left to lose, so nothing is charged there; but someone who SURVIVED your
        // attempt on their life has every reason to hate you, and that must be the harshest hit of all.
        [Test]
        public void GIVEN_a_killing_WHEN_priced_THEN_the_dead_lose_no_regard_but_a_survivor_hates_you()
        {
            Cost(ViolentAct.Kill).RelationHit.Should().Be(0);

            Cost(ViolentAct.Kill, ViolenceVerdict.Failed).RelationHit
                .Should().Be(ViolenceConsequencePolicy.AttemptedMurderRelationHit);
            Cost(ViolentAct.Kill, ViolenceVerdict.Failed).RelationHit
                .Should().BeLessThan(Cost(ViolentAct.Assault).RelationHit);
        }

        // Blood makes enemies of a family; a stolen purse does not. This is what turns a murder into a lasting
        // story rather than a one-off transaction.
        [Test]
        public void GIVEN_the_three_acts_WHEN_priced_THEN_only_blood_and_beatings_make_enemies_of_kin()
        {
            Cost(ViolentAct.Kill).KinGrudgeMagnitude.Should().BeGreaterThan(0);
            Cost(ViolentAct.Assault).KinGrudgeMagnitude.Should().BeGreaterThan(0);
            Cost(ViolentAct.Rob).KinGrudgeMagnitude.Should().Be(0);

            Cost(ViolentAct.Kill).KinGrudgeMagnitude
                .Should().BeGreaterThan(Cost(ViolentAct.Assault).KinGrudgeMagnitude);
        }
    }
}
