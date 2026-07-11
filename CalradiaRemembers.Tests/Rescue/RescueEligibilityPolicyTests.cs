// Code written by Gabriel Mailhot, 11/07/2026.

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
