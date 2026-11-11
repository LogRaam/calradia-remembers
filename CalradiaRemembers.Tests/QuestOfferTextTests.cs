// Code written by Gabriel Mailhot, 11/11/2026.
// What the player reads when a character puts a task to them, before it is theirs. Schmidt (Discord, 11/11/2026): "NPC's
// give you quests that you didn't agree to ... Same with asking NPC's to do something but it shows up for you as a quest."
// Gabriel (11/11/2026): "Tu peux faire 11." A task is now an offer to accept or decline. If this text is wrong, the player
// accepts a deed without seeing what it pays or how long it runs, or cannot tell that a bargain is the price of a favour
// they asked for.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestOfferTextTests
    {
        // THE REPORT: the player decides with the deed, the pay and the time in front of them, and knows the choice is theirs.
        [Test]
        public void GIVEN_a_task_offered_WHEN_shown_THEN_it_names_the_deed_the_reward_the_deadline_and_that_it_is_the_players_choice()
        {
            var facts = new QuestOfferFacts {GiverName = "Lord Aldric", Deed = "ride close to Hvalvik and report what it holds", RewardGold = 500, RewardRelation = 3, DeadlineDays = 10};

            QuestOfferText.Title(facts).Should().Be("Lord Aldric asks a task of you");
            QuestOfferText.Body(facts).Should().Be(
                "Lord Aldric would have you ride close to Hvalvik and report what it holds. On completion: 500 denars, their regard. You would have 10 days."
                + " It becomes your task only if you accept; declining breaks no word.");
        }

        // Schmidt's second case: a favour the player asked for came back as a task. Told as the price of that favour, the
        // player sees why it is asked and may refuse it.
        [Test]
        public void GIVEN_a_bargain_WHEN_shown_THEN_it_is_told_as_the_price_of_what_the_player_asked()
        {
            var facts = new QuestOfferFacts {GiverName = "Lord Aldric", Deed = "bring a dowry of 2,000 denars", Grant = RewardGrant.MarriageConsent};

            QuestOfferText.Title(facts).Should().Be("Lord Aldric names a price");
            QuestOfferText.Body(facts).Should().StartWith("In return for what you asked, Lord Aldric would have you bring a dowry")
                .And.Contain("On completion: their consent to the match.");
        }

        // A task with nothing promised says so, rather than leaving the player to guess at a reward that will not come.
        [Test]
        public void GIVEN_a_task_with_no_reward_and_no_deadline_WHEN_shown_THEN_it_says_no_reward_and_names_no_time()
        {
            string body = QuestOfferText.Body(new QuestOfferFacts {GiverName = "Ylva", Deed = "clear the bandits near Ab Comer"});

            body.Should().Contain("No reward is promised.").And.NotContain("day");
        }

        // While the choice waits, the chat says a task was put, not given, so nothing reads as already accepted.
        [Test]
        public void GIVEN_an_offer_waiting_WHEN_the_chat_notes_it_THEN_it_asks_for_an_answer()
        {
            QuestOfferText.Pending(new QuestOfferFacts {GiverName = "Ylva", Deed = "clear the bandits near Ab Comer"})
                .Should().Be("Ylva puts a task to you: clear the bandits near Ab Comer. Accept or decline it.");
        }
    }
}
