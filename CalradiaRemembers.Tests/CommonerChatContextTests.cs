// Code written by Gabriel Mailhot, 20/06/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CommonerChatContextTests
   {
      [Test]
      public void GIVEN_a_bandit_hideout_captain_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Bandit");

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_merchant_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Merchant");

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_named_hero_conversation_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(true);

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse("a named lord or notable uses their own conversation flow");
      }

      [Test]
      public void GIVEN_a_tavern_keeper_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Tavernkeeper");

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse("a tavern keeper has their own dialogue, incl. sending letters");
      }

      [Test]
      public void GIVEN_a_townsperson_but_not_in_a_civilian_settlement_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(isCivilianSettlement: false); // e.g. a hideout, or no settlement at all

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_townsperson_whose_occupation_casing_differs_WHEN_evaluated_THEN_idle_chat_is_still_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "townsfolk");

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_villager_WHEN_evaluated_THEN_idle_chat_is_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Villager");

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_anonymous_townsperson_in_a_town_with_the_ai_ready_WHEN_evaluated_THEN_idle_chat_is_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Townsfolk");

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeTrue();
      }

      [Test]
      public void GIVEN_no_conversation_partner_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(hasConversationPartner: false, partnerOccupation: null);

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_ai_is_unavailable_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(llmAvailable: false);

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      #region private

      // The "eligible" baseline; each test overrides only the one fact it is about.
      private static CommonerChatContext Context(
         bool isHeroConversation = false,
         bool isCivilianSettlement = true,
         bool hasConversationPartner = true,
         string? partnerOccupation = "Townsfolk",
         bool llmAvailable = true)
         => new(
            isHeroConversation, isCivilianSettlement, hasConversationPartner, partnerOccupation, llmAvailable);

      #endregion
   }
}