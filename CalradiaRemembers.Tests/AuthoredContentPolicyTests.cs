// Code written by Gabriel Mailhot, 10/10/2026.
// A written tale's content level against the player's own setting (Gabriel, 10/10/2026: "can a quest be classed
// Hardcore?"). If this rule is wrong, a hardcore tale is offered to a player who switched adult content off and its
// scenes reach their conversations, or a tale for everyone is held back from them.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredContentPolicyTests
   {
      // The point of the field: a hardcore tale never reaches a player below Hardcore.
      [TestCase(AdultContentLevel.Off)]
      [TestCase(AdultContentLevel.Mature)]
      [TestCase(AdultContentLevel.Explicit)]
      public void GIVEN_a_hardcore_tale_WHEN_the_player_is_below_hardcore_THEN_it_is_not_for_them(AdultContentLevel player)
      {
         AuthoredContentPolicy.Allows("hardcore", player).Should().BeFalse();
      }

      // A player at the level, or above it, gets the tale.
      [Test]
      public void GIVEN_a_tale_at_or_below_the_players_level_WHEN_checked_THEN_it_is_for_them()
      {
         AuthoredContentPolicy.Allows("hardcore", AdultContentLevel.Hardcore).Should().BeTrue();
         AuthoredContentPolicy.Allows("explicit", AdultContentLevel.Hardcore).Should().BeTrue();
         AuthoredContentPolicy.Allows("mature", AdultContentLevel.Mature).Should().BeTrue();
      }

      // Every tale written before the field (the wolf, the bridge) asks for nothing and stays open to everyone.
      [Test]
      public void GIVEN_a_tale_that_names_no_level_WHEN_checked_THEN_every_player_gets_it()
      {
         AuthoredContentPolicy.Allows(null, AdultContentLevel.Off).Should().BeTrue();
         AuthoredContentPolicy.Allows("none", AdultContentLevel.Off).Should().BeTrue();
         AuthoredContentPolicy.Allows(" Hardcore ", AdultContentLevel.Hardcore).Should().BeTrue("the word is read in any case");
      }

      // A level the game does not know is never taken as permission: a misspelt "hardcor" stays closed.
      [Test]
      public void GIVEN_a_word_that_is_no_level_WHEN_checked_THEN_the_tale_is_closed()
      {
         AuthoredContentPolicy.Required("hardcor").Should().BeNull();
         AuthoredContentPolicy.Allows("hardcor", AdultContentLevel.Hardcore).Should().BeFalse();
      }

      // The offer itself waits, and says why (cr.story_check), when the setting does not reach the tale.
      [Test]
      public void GIVEN_a_tale_above_the_players_setting_WHEN_it_would_be_offered_THEN_it_is_not_and_the_author_is_told_why()
      {
         var f = new AuthoredOfferFacts {Today = 100, Chance = 1, ContentAllowed = false, Content = "hardcore"};

         AuthoredQuestOfferPolicy.ShouldOffer(f).Should().BeFalse();
         AuthoredQuestOfferPolicy.WhyNot(f).Should().Contain("hardcore");
      }
   }
}
