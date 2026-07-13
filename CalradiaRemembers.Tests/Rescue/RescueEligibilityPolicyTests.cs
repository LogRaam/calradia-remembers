// Code written by Gabriel Mailhot, 11/07/2026.
// Rescue awareness (v1.30.5, ratified 2026-07-11): freeing a captive lord by force now earns real, LASTING
// consequences (gratitude + a memory for the freed lord and family, friends warm, the captor may grudge it).
// RescueEligibilityPolicy.IsPlayerRescue is the one gate RescueBehavior.CreditRescue trusts before any of
// that fires, and credit is once-per-lord and persisted for the rest of the save, so a false positive wastes
// it on a release that was not a genuine player rescue, and a false negative silently drops a rescue the
// player actually earned.

#region

using CalradiaRemembers.Logic.Rescue;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Rescue
{
    [TestFixture]
    public sealed class RescueEligibilityPolicyTests
    {
        // The baseline positive case the whole rescue pillar exists for.
        [Test]
        public void GIVEN_a_lord_freed_after_a_battle_the_player_won_WHEN_evaluated_THEN_it_counts_as_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: true,
                releasedAfterBattle: true,
                prisonerIsLord: true,
                prisonerIsBandit: false,
                prisonerInPlayerClan: false,
                prisonerIsPlayer: false);

            isRescue.Should().BeTrue();
        }

        // A lord freed as part of a battle the player LOST must not be credited: crediting it would turn a
        // defeat (or a rout) into a fabricated heroic rescue.
        [Test]
        public void GIVEN_the_player_did_not_win_the_battle_WHEN_evaluated_THEN_it_is_not_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: false,
                releasedAfterBattle: true,
                prisonerIsLord: true,
                prisonerIsBandit: false,
                prisonerInPlayerClan: false,
                prisonerIsPlayer: false);

            isRescue.Should().BeFalse();
        }

        // Only a release that is a DIRECT RESULT of the won battle counts; a release that merely coincides
        // with one (ransom paid elsewhere, an unrelated escape) must not be mistaken for a rescue.
        [Test]
        public void GIVEN_the_prisoner_was_not_released_after_the_battle_WHEN_evaluated_THEN_it_is_not_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: true,
                releasedAfterBattle: false,
                prisonerIsLord: true,
                prisonerIsBandit: false,
                prisonerInPlayerClan: false,
                prisonerIsPlayer: false);

            isRescue.Should().BeFalse();
        }

        // Gratitude propagates through a lord's family and friend network (RescueBehavior.ResolveFamily /
        // ResolveFriends); a freed commoner troop has no such graph to react, so this must not enter the
        // rescue-credit pipeline at all.
        [Test]
        public void GIVEN_the_freed_prisoner_is_not_a_lord_WHEN_evaluated_THEN_it_is_not_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: true,
                releasedAfterBattle: true,
                prisonerIsLord: false,
                prisonerIsBandit: false,
                prisonerInPlayerClan: false,
                prisonerIsPlayer: false);

            isRescue.Should().BeFalse();
        }

        // Per the policy's own doc, only a "non-bandit lord" qualifies: a bandit boss set loose is not a
        // grateful ally with a family and friends to warm toward the player.
        [Test]
        public void GIVEN_the_freed_prisoner_is_a_bandit_WHEN_evaluated_THEN_it_is_not_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: true,
                releasedAfterBattle: true,
                prisonerIsLord: true,
                prisonerIsBandit: true,
                prisonerInPlayerClan: false,
                prisonerIsPlayer: false);

            isRescue.Should().BeFalse();
        }

        // Freeing a member of the player's OWN clan is not a rescue in this sense: it must not trigger
        // gratitude/memory toward the player's own household, nor put a captor's grudge on a family matter.
        [Test]
        public void GIVEN_the_freed_prisoner_is_in_the_players_own_clan_WHEN_evaluated_THEN_it_is_not_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: true,
                releasedAfterBattle: true,
                prisonerIsLord: true,
                prisonerIsBandit: false,
                prisonerInPlayerClan: true,
                prisonerIsPlayer: false);

            isRescue.Should().BeFalse();
        }

        // The player cannot be their own rescuer; RescueBehavior already filters this earlier in the pipeline
        // (OnHeroPrisonerReleased), so this pins the policy itself as a second, independent backstop.
        [Test]
        public void GIVEN_the_freed_prisoner_is_the_player_WHEN_evaluated_THEN_it_is_not_a_rescue()
        {
            bool isRescue = RescueEligibilityPolicy.IsPlayerRescue(
                playerWonBattle: true,
                releasedAfterBattle: true,
                prisonerIsLord: true,
                prisonerIsBandit: false,
                prisonerInPlayerClan: false,
                prisonerIsPlayer: true);

            isRescue.Should().BeFalse();
        }
    }
}
