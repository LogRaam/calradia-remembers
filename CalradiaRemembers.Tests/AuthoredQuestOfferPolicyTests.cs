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

      // A repeatable quest (unique false) waits its file's repeat_cooldown_days after it was last played. If not, a
      // modder's "repeatable, long cooldown" quest is raised again the day after it ended.
      [Test]
      public void GIVEN_a_repeatable_quest_played_recently_WHEN_its_cooldown_has_not_passed_THEN_it_is_not_offered()
      {
         var f = new AuthoredOfferFacts {Today = 100, Chance = 1, Unique = false, AlreadyPlayed = true, LastPlayedOnDay = 80, RepeatCooldownDays = 60};

         AuthoredQuestOfferPolicy.ShouldOffer(f).Should().BeFalse();
         AuthoredQuestOfferPolicy.WhyNot(f).Should().Contain("played on day 80").And.Contain("60 days");
      }

      // Once the cooldown has passed, the repeatable quest may come back.
      [Test]
      public void GIVEN_a_repeatable_quest_WHEN_its_cooldown_has_passed_THEN_it_may_be_offered_again()
      {
         AuthoredQuestOfferPolicy.ShouldOffer(new AuthoredOfferFacts {Today = 140, Chance = 1, Unique = false, AlreadyPlayed = true, LastPlayedOnDay = 80, RepeatCooldownDays = 60})
            .Should().BeTrue();
      }

      // The reason a quest is not raised is said in plain words (cr.story_start, cr.story_check), and a refusal whose
      // season has passed is no longer given as the reason. If it were, the author chases a rule that no longer holds.
      [Test]
      public void GIVEN_a_refusal_whose_season_has_passed_WHEN_asked_why_not_THEN_it_is_not_the_reason()
      {
         var f = new AuthoredOfferFacts {Today = 90 + AuthoredQuestOfferPolicy.DeclineWaitDays, Chance = 1, DeclinedOnDay = 90};

         AuthoredQuestOfferPolicy.WhyNot(f).Should().BeNull();
         AuthoredQuestOfferPolicy.WhyNot(new AuthoredOfferFacts {Today = 95, Chance = 1, DeclinedOnDay = 90}).Should().Contain("declined on day 90");
      }

      // The weight of the file decides which of several qualifying quests a giver raises: a quest of weight 30 beside
      // one of weight 10 is chosen three times in four. If not, the first file in the folder always wins.
      [Test]
      public void GIVEN_two_qualifying_quests_WHEN_one_is_chosen_THEN_the_weights_share_the_roll()
      {
         int[] weights = {10, 30};

         AuthoredQuestOfferPolicy.PickByWeight(weights, 0.0).Should().Be(0);
         AuthoredQuestOfferPolicy.PickByWeight(weights, 0.24).Should().Be(0);
         AuthoredQuestOfferPolicy.PickByWeight(weights, 0.26).Should().Be(1);
         AuthoredQuestOfferPolicy.PickByWeight(weights, 0.999).Should().Be(1);
      }

      // A weight of zero or less never wins while another quest qualifies, and an empty choice picks nothing.
      [Test]
      public void GIVEN_a_quest_of_weight_zero_WHEN_one_is_chosen_THEN_it_is_never_picked()
      {
         AuthoredQuestOfferPolicy.PickByWeight(new[] {0, 10}, 0.0).Should().Be(1);
         AuthoredQuestOfferPolicy.PickByWeight(new int[0], 0.5).Should().Be(-1);
         AuthoredQuestOfferPolicy.PickByWeight(new[] {0}, 0.5).Should().Be(-1);
      }

      // fkasad (Nexus, 06/10/2026): "What about an NPC who already offers a vanilla quest? ... to avoid trouble during
      // discussion". A giver with a task of their own already offered or under way does not raise a written tale too:
      // two errands from one mouth tangle the conversation, and the model may mix them.
      [Test]
      public void GIVEN_a_giver_with_a_vanilla_quest_for_the_player_WHEN_a_tale_would_be_raised_THEN_it_waits()
      {
         var f = new AuthoredOfferFacts {Today = 100, Chance = 1, GiverHasVanillaQuest = true};

         AuthoredQuestOfferPolicy.ShouldOffer(f).Should().BeFalse();
         AuthoredQuestOfferPolicy.WhyNot(f).Should().Contain("task of their own");
      }

      // fkasad: "ModuleData/Custom/Quests (consistency, one folder to manage) would be nice, and the external folder (MCM
      // path) would be great." Tales are read from the shipped folder, the old Custom folder (files there keep working),
      // and Quests under the player's Custom folder, the MCM path when set; each once.
      [Test]
      public void GIVEN_the_custom_folder_WHEN_tale_folders_are_listed_THEN_the_shipped_the_old_and_the_players_custom_are_read_once()
      {
         AuthoredQuestFolders.List(@"M\ModuleData\Quests", @"D:\MyCR")
                             .Should().Equal(@"M\ModuleData\Quests", System.IO.Path.Combine(@"M\ModuleData\Quests", "Custom"), System.IO.Path.Combine(@"D:\MyCR", "Quests"));
         AuthoredQuestFolders.List(@"M\ModuleData\Quests", null)
                             .Should().Equal(@"M\ModuleData\Quests", System.IO.Path.Combine(@"M\ModuleData\Quests", "Custom"));
      }
   }
}
