// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: when a character who could give a quest raises it (docs/design/AUTHORED_QUESTS.md).
// Gabriel's rulings: one authored quest at a time; unique quests once per campaign; a refusal is respected. If these
// rules are wrong, a second story starts over the first, a unique story is played twice, or a lord asks again the day
// after the player said no.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredQuestOfferPolicyTests
   {
      private static AuthoredOfferFacts Ready() => new() {Today = 100, Roll = 0.0, Chance = 0.5};

      // A giver who qualifies, no story under way, the dice say yes: the quest is raised.
      [Test]
      public void GIVEN_a_qualifying_giver_and_no_story_under_way_WHEN_the_roll_passes_THEN_the_quest_is_offered()
      {
         AuthoredQuestOfferPolicy.ShouldOffer(Ready()).Should().BeTrue();
      }

      // Gabriel: one authored quest at a time.
      [Test]
      public void GIVEN_a_story_already_under_way_WHEN_judged_THEN_no_other_is_offered()
      {
         AuthoredQuestOfferPolicy.ShouldOffer(new AuthoredOfferFacts {Today = 100, Chance = 1, StoryUnderWay = true}).Should().BeFalse();
      }

      // A unique story already played (won or lost) never comes back in that campaign.
      [Test]
      public void GIVEN_a_unique_story_already_played_WHEN_judged_THEN_it_is_not_offered_again()
      {
         AuthoredQuestOfferPolicy.ShouldOffer(new AuthoredOfferFacts {Today = 100, Chance = 1, Unique = true, AlreadyPlayed = true}).Should().BeFalse();
      }

      // A refusal is respected for a season: the same quest is not raised again before then.
      [Test]
      public void GIVEN_the_player_declined_WHEN_the_wait_has_not_passed_THEN_it_is_not_raised_again()
      {
         var f = new AuthoredOfferFacts {Today = 100, Chance = 1, DeclinedOnDay = 90};

         AuthoredQuestOfferPolicy.ShouldOffer(f).Should().BeFalse();
         AuthoredQuestOfferPolicy.ShouldOffer(new AuthoredOfferFacts {Today = 90 + AuthoredQuestOfferPolicy.DeclineWaitDays, Chance = 1, DeclinedOnDay = 90}).Should().BeTrue();
      }

      // The dice say no: nothing this time, so a story is a surprise, not a fixture of every conversation.
      [Test]
      public void GIVEN_a_roll_above_the_chance_WHEN_judged_THEN_nothing_is_offered()
      {
         AuthoredQuestOfferPolicy.ShouldOffer(new AuthoredOfferFacts {Today = 100, Roll = 0.9, Chance = 0.5}).Should().BeFalse();
      }
   }
}
