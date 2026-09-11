// Code written by Gabriel Mailhot, 20/06/2026.
// Pins the eligibility rule for the "What's the news here?" idle-chat option (CommonsDialogBehavior): who
// gets offered a slim, memoryless commoner conversation and who does not.
//
// WHY IT MATTERS: per CommonerChatContext's own header, this rule "has regressed before" (functional
// place-bound NPCs, notably the tavern keeper, getting hijacked by the generic commoner flow instead of their
// own dialogue). The whitelist approach (only Townsfolk/Villager qualify) rather than a blacklist is what is
// meant to stop that regression class for good; these tests are what proves the whitelist actually holds.

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
      // A functional captor NPC must keep its own dialogue; hijacking it with the generic idle-chat option is
      // exactly the class of regression the whitelist (not a blacklist) exists to close off for good.
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

      // A merchant is a functional occupation with its own trade dialogue, not an anonymous townsperson; the
      // whitelist must exclude it by construction (only Townsfolk/Villager are in the set) rather than by
      // enumerating every functional role one at a time.
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

      // A named Hero (lord, companion, etc.) has the full memory-backed chat flow; routing them into the
      // slim, memoryless commoner path would silently downgrade their conversation.
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

      // The named regression case: the tavern keeper owns its own dialogue (incl. sending letters) and must
      // never be hijacked by the generic idle-chat option.
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

      // A hideout (or no settlement at all) is never civilian ground; the option must not appear off-place,
      // e.g. offering "what's the news here" to a bandit lair's rank-and-file.
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

      // The whitelist is a case-insensitive set (GenericTownsperson uses OrdinalIgnoreCase); the occupation
      // string comes from the engine and its casing is not a contract the mod controls, so a case mismatch
      // must never silently exclude a genuine townsperson from the option.
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

      // The positive case for the other half of the whitelist: a villager is exactly the anonymous, non-named
      // person the "Talk to the locals" feature was built for.
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

      // The full happy path (all five facts favourable at once), proving no single true condition is quietly
      // gated by another the test above didn't isolate.
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

      // No partner means nothing to talk to; the option must not appear and offer a conversation the mod has
      // no NPC to attach it to.
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

      // Offering the option with no LLM backing it would be a dead end for the player (a choice that opens a
      // conversation that can never actually reply); this must be gated by live AI availability.
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

      // Player report: during a quest like The Spy Among Us, the player must be able to talk to random
      // townsfolk to get the quest's clues, but CR's "What's the news here?" idle chat option was hijacking
      // that same conversation, so the player could never see the vanilla quest dialogue. Idle chat must be
      // withheld while such a townsfolk-dependent quest is ongoing, or the quest becomes uncompletable.
      [Test]
      public void GIVEN_a_townsfolk_dependent_quest_is_active_WHEN_asked_THEN_idle_chat_is_not_offered()
      {
         //Arrange
         CommonerChatContext context = Context(townsfolkQuestActive: true);

         //Act
         bool offered = context.ShouldOfferIdleChat();

         //Assert
         offered.Should().BeFalse("a vanilla quest relying on townsfolk dialogue must not be diverted by CR's idle chat");
      }

      #region private

      // The "eligible" baseline; each test overrides only the one fact it is about.
      private static CommonerChatContext Context(
         bool isHeroConversation = false,
         bool isCivilianSettlement = true,
         bool hasConversationPartner = true,
         string? partnerOccupation = "Townsfolk",
         bool llmAvailable = true,
         bool townsfolkQuestActive = false)
         => new() {IsHeroConversation = isHeroConversation, IsCivilianSettlement = isCivilianSettlement, HasConversationPartner = hasConversationPartner, PartnerOccupation = partnerOccupation, LlmAvailable = llmAvailable, TownsfolkQuestActive = townsfolkQuestActive};

      #endregion
   }
}