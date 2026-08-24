// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 4 (support_claimant): this policy is the decision SettleSupportClaimant used to make inline, now
// pure and testable. What breaks if it is wrong: a hostile or ineligible vassal appears to back the player (moving
// real clan influence they should not), or a genuine backer is turned away. The gate ORDER matters (a dead backer
// is "not found" before anything else is even asked), and a zero/negative relation must yield no backing so a cold
// vassal can never publicly support the player. Exact influence amounts are CourtActionPolicy's own tested job;
// these tests pin the outcomes and the "positive relation backs, non-positive does not" boundary.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ClaimantSupportPolicyTests
    {
        // A well-funded, well-liked, same-kingdom other-clan lord is the whole point: they back the player, moving
        // a positive amount of influence.
        [Test]
        public void GIVEN_an_eligible_well_liked_backer_THEN_the_backing_is_applied_with_a_positive_amount()
        {
            ClaimantSupportResult r = ClaimantSupportPolicy.Evaluate(
                backerAlive: true, backerIsLord: true, backerIsPrisoner: false,
                backerHasClan: true, backerIsPlayerClan: false, backerInPlayerKingdom: true,
                relation: 50, backerInfluence: 100000f);

            r.Outcome.Should().Be(ClaimantSupportOutcome.Applied);
            r.Amount.Should().BeGreaterThan(0);
        }

        // A dead / non-lord / imprisoned backer is "not found" before any other gate is asked (gate order).
        [Test]
        public void GIVEN_a_dead_backer_THEN_the_outcome_is_not_found_even_if_otherwise_eligible()
        {
            ClaimantSupportPolicy.Evaluate(false, true, false, true, false, true, 50, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.NotFound);
        }

        [Test]
        public void GIVEN_a_backer_who_is_not_a_lord_or_is_a_prisoner_THEN_the_outcome_is_not_found()
        {
            ClaimantSupportPolicy.Evaluate(true, false, false, true, false, true, 50, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.NotFound);
            ClaimantSupportPolicy.Evaluate(true, true, true, true, false, true, 50, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.NotFound);
        }

        // No clan of their own, or a clan that IS the player's: there is no separate house to lend backing with.
        [Test]
        public void GIVEN_no_clan_or_the_players_own_clan_THEN_the_outcome_is_backer_is_player_house()
        {
            ClaimantSupportPolicy.Evaluate(true, true, false, false, false, true, 50, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.BackerIsPlayerHouse);
            ClaimantSupportPolicy.Evaluate(true, true, false, true, true, true, 50, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.BackerIsPlayerHouse);
        }

        // A backer whose clan sits in a different kingdom cannot back a claim in the player's own court.
        [Test]
        public void GIVEN_a_backer_outside_the_players_kingdom_THEN_the_outcome_is_not_in_player_kingdom()
        {
            ClaimantSupportPolicy.Evaluate(true, true, false, true, false, false, 50, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.NotInPlayerKingdom);
        }

        // The cold-vassal guard: a zero or negative relation yields a zero grant, so an unfriendly backer never
        // appears to support the player (CannotAfford, amount 0).
        [Test]
        public void GIVEN_a_non_positive_relation_THEN_the_backing_is_refused_as_unaffordable()
        {
            ClaimantSupportPolicy.Evaluate(true, true, false, true, false, true, 0, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.CannotAfford);
            ClaimantSupportPolicy.Evaluate(true, true, false, true, false, true, -20, 100000f)
                .Outcome.Should().Be(ClaimantSupportOutcome.CannotAfford);
        }

        // A willing backer whose clan simply has no influence to spare is refused, not applied.
        [Test]
        public void GIVEN_a_willing_backer_with_no_influence_THEN_the_backing_is_unaffordable()
        {
            ClaimantSupportPolicy.Evaluate(true, true, false, true, false, true, 50, 0f)
                .Outcome.Should().Be(ClaimantSupportOutcome.CannotAfford);
        }
    }
}
