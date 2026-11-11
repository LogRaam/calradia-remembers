// Code written by Gabriel Mailhot, 11/11/2026.
// Why a task offer was not recorded, said to the player and to the character. Aporo (Discord, 11/11/2026): Grand Prince
// Raganvad offered Mazhadan Castle and vassalage for a captured lord three times, and each refusal said only "could not be
// matched to the world. Ask them to restate it", so he restated the same castle and the player could not tell what to
// change. If this text is wrong, the player is left guessing and the character repeats the same refused offer.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestRefusalTextTests
    {
        // THE REPORT: a castle offered as the price with fief rewards off. STAKE: the player must learn it is the reward,
        // and which setting rules it, not that the world could not be matched.
        [Test]
        public void GIVEN_a_fief_offered_as_the_price_with_fief_rewards_off_WHEN_refused_THEN_the_player_is_told_the_reward_and_the_setting()
        {
            var facts = new QuestRefusalFacts {Reason = QuestRefusal.FiefRewardsOff, GiverName = "Grand Prince Raganvad", Fief = "Mazhadan Castle"};

            QuestRefusalText.ForThePlayer(facts).Should().Be(
                "No task was recorded: Grand Prince Raganvad offered a fief as its price, and fiefs are not quest rewards in your settings (\"Allow Fief And Marriage Quest Rewards\"). Ask for another reward.");
        }

        // STAKE: told only "restate it", the character offered the same castle three times. It must learn the castle is
        // what was refused, and what it may offer instead.
        [Test]
        public void GIVEN_a_fief_offered_with_fief_rewards_off_WHEN_refused_THEN_the_character_learns_a_fief_cannot_be_the_price_and_what_can()
        {
            string note = QuestRefusalText.ForTheCharacter(new QuestRefusalFacts {Reason = QuestRefusal.FiefRewardsOff});

            note.Should().StartWith("Your last task offer was NOT recorded")
                .And.Contain("a fief, a title or vassalage cannot be the price of a task")
                .And.Contain("gold, your regard or a listed favour");
        }

        // STAKE: with fief rewards on, a fief another clan holds is still refused; the player must hear it is not the
        // giver's to give, naming the fief, or the setting looks broken.
        [Test]
        public void GIVEN_a_fief_the_giver_does_not_hold_WHEN_refused_THEN_both_hear_it_is_not_theirs_to_give()
        {
            var facts = new QuestRefusalFacts {Reason = QuestRefusal.FiefNotTheirs, GiverName = "Grand Prince Raganvad", Fief = "Mazhadan Castle"};

            QuestRefusalText.ForThePlayer(facts).Should().Contain("offered Mazhadan Castle as its price, which is not theirs to give");
            QuestRefusalText.ForTheCharacter(facts).Should().Contain("Mazhadan Castle is not yours to give");
        }

        // STAKE: a favour the game cannot pay (troops) must be named, so the player asks for coin instead of the same troops.
        [Test]
        public void GIVEN_a_favour_the_game_cannot_pay_WHEN_refused_THEN_the_favour_is_named()
        {
            QuestRefusalText.ForThePlayer(new QuestRefusalFacts {Reason = QuestRefusal.GrantNotPayable, GiverName = "Lord Aldric", Grant = RewardGrant.GiveTroops})
                .Should().Be("No task was recorded: Lord Aldric offered troops as its price, which the game cannot pay yet. Ask for coin or their favour instead.");
        }

        // STAKE: a target the world does not hold keeps the old line for the player, and the character is told to name
        // only what it knows by exact name, so it does not invent the same target again.
        [Test]
        public void GIVEN_a_target_not_found_WHEN_refused_THEN_the_player_reads_the_old_line_and_the_character_is_told_to_use_exact_names()
        {
            var facts = new QuestRefusalFacts {Reason = QuestRefusal.NotPlaced, GiverName = "Lord Aldric"};

            QuestRefusalText.ForThePlayer(facts).Should().Be("No task was recorded: what they offered could not be matched to the world. Ask them to restate it.");
            QuestRefusalText.ForTheCharacter(facts).Should().Contain("exact names");
        }
    }
}
