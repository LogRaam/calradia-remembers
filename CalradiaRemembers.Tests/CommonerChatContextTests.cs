// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the "talk to the locals" eligibility rule — which regressed before (the tavern keeper and
// other place-bound NPCs being hijacked) — by asserting the pure decision across every relevant fact.

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
      public void GIVEN_an_anonymous_townsperson_in_a_town_with_the_ai_ready_WHEN_evaluated_THEN_idle_chat_is_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Townsfolk");

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_villager_WHEN_evaluated_THEN_idle_chat_is_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Villager");

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_townsperson_whose_occupation_casing_differs_WHEN_evaluated_THEN_idle_chat_is_still_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "townsfolk");

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_tavern_keeper_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Tavernkeeper");

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse("a tavern keeper has their own dialogue, incl. sending letters");
      }

      [Test]
      public void GIVEN_a_bandit_hideout_captain_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Bandit");

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_merchant_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(partnerOccupation: "Merchant");

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_named_hero_conversation_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(isHeroConversation: true);

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse("a named lord or notable uses their own conversation flow");
      }

      [Test]
      public void GIVEN_a_townsperson_but_not_in_a_civilian_settlement_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(isCivilianSettlement: false); // e.g. a hideout, or no settlement at all

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_no_conversation_partner_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(hasConversationPartner: false, partnerOccupation: null);

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_ai_is_unavailable_WHEN_evaluated_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(llmAvailable: false);

         //Act
         var offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse();
      }

      #region private

      // The "eligible" baseline; each test overrides only the one fact it is about.
      private static CommonerChatContext Context(
         bool    isHeroConversation     = false,
         bool    isCivilianSettlement   = true,
         bool    hasConversationPartner = true,
         string? partnerOccupation      = "Townsfolk",
         bool    llmAvailable           = true)
         => new CommonerChatContext(
            isHeroConversation, isCivilianSettlement, hasConversationPartner, partnerOccupation, llmAvailable);

      #endregion
   }
}
