// Code written by Gabriel Mailhot, 20/07/2026.
// Documents how a council consents: the stance one member takes on a motion (from regard and personal stake) and
// whether the council as a whole carries it. The gradient is the point: an advisory council (companions, family)
// always carries because the clan head decides, while a lords' council can be blocked by its own members.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class AssemblyConsentPolicyTests
    {
        // Opposition is the costly-and-cold case: a member the motion HURTS, who also does not hold the player in
        // decent regard, speaks against it. If either half were missing (loyal, or unaffected) he would not oppose,
        // so this pins the exact corner where a stance turns to open dissent.
        [Test]
        public void GIVEN_a_motion_that_costs_a_cold_member_WHEN_he_takes_a_stance_THEN_he_opposes()
        {
            AssemblyConsentPolicy.Stance(new ConsentFacts(regardToInitiator: -10, stake: -1))
                .Should().Be(ConsentStance.Oppose);
        }

        // Loyalty overrides cost: a member who holds the player in warm regard backs a motion even when it costs
        // him. Without this a devoted brother would still vote against a levy on his own fief, which is not how
        // loyalty should read.
        [Test]
        public void GIVEN_a_costly_motion_but_a_warmly_loyal_member_WHEN_he_takes_a_stance_THEN_he_still_favors_it()
        {
            AssemblyConsentPolicy.Stance(new ConsentFacts(regardToInitiator: 30, stake: -5))
                .Should().Be(ConsentStance.Favor);
        }

        // Self-interest is enough on its own: a motion that helps a member wins his backing regardless of how he
        // feels about the player. A gift is a gift.
        [Test]
        public void GIVEN_a_motion_that_helps_even_an_indifferent_member_WHEN_he_takes_a_stance_THEN_he_favors_it()
        {
            AssemblyConsentPolicy.Stance(new ConsentFacts(regardToInitiator: -5, stake: 4))
                .Should().Be(ConsentStance.Favor);
        }

        // The indifferent middle: a neutral motion from a lukewarm member is an abstention, not a stance either
        // way. This keeps the tally honest, abstentions must not be read as quiet support or quiet dissent.
        [Test]
        public void GIVEN_a_neutral_motion_and_a_lukewarm_member_WHEN_he_takes_a_stance_THEN_he_abstains()
        {
            AssemblyConsentPolicy.Stance(new ConsentFacts(regardToInitiator: 5, stake: 0))
                .Should().Be(ConsentStance.Abstain);
        }

        // The advisory gradient: a companions' council carries even against a wall of opposition, because they
        // advise and the clan head decides. If this blocked, the player could not act on his own hired men.
        [Test]
        public void GIVEN_a_companions_council_that_all_oppose_WHEN_tallied_THEN_the_motion_still_carries()
        {
            ConsentResult r = AssemblyConsentPolicy.Decide(CouncilScope.Companions,
                new[] {ConsentStance.Oppose, ConsentStance.Oppose});

            r.Carries.Should().BeTrue();
            r.Oppose.Should().Be(2);
        }

        // The binding gradient: among clan lords, opposition outnumbering support BLOCKS the motion. This is the
        // real political weight the whole consent model exists to give the noble council.
        [Test]
        public void GIVEN_a_clan_lords_council_where_opposition_outnumbers_support_WHEN_tallied_THEN_it_is_blocked()
        {
            ConsentResult r = AssemblyConsentPolicy.Decide(CouncilScope.ClanLords,
                new[] {ConsentStance.Oppose, ConsentStance.Oppose, ConsentStance.Favor});

            r.Carries.Should().BeFalse();
        }

        // The tie goes to the clan head: with lords evenly split, the player's own vote is the caster, so the
        // motion carries. This pins the boundary at favor >= oppose, not a strict majority.
        [Test]
        public void GIVEN_a_clan_lords_council_split_evenly_WHEN_tallied_THEN_the_head_breaks_the_tie_and_it_carries()
        {
            ConsentResult r = AssemblyConsentPolicy.Decide(CouncilScope.ClanLords,
                new[] {ConsentStance.Favor, ConsentStance.Oppose});

            r.Carries.Should().BeTrue();
        }

        // Abstentions are inert: a lords' council of pure abstention still carries (nobody blocked), so silence is
        // not treated as a veto. Guards the tally against counting abstain toward opposition.
        [Test]
        public void GIVEN_a_clan_lords_council_of_only_abstentions_WHEN_tallied_THEN_nothing_blocks_and_it_carries()
        {
            ConsentResult r = AssemblyConsentPolicy.Decide(CouncilScope.ClanLords,
                new[] {ConsentStance.Abstain, ConsentStance.Abstain});

            r.Carries.Should().BeTrue();
            r.Abstain.Should().Be(2);
        }
    }
}
