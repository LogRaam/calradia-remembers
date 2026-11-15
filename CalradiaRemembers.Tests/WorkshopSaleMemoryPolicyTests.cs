// Code written by Gabriel Mailhot, 14/11/2026.
// WorkshopSaleMemoryPolicy decides what a character remembers when the player buys his workshop. Windmolen (Nexus,
// 14/11/2026): the man who sold him a business "contested my ownership, wanted me to produce a sealed document or a
// witness"; the purchase is a click in the game, so the seller must know it happened. If this is wrong: the seller
// disputes a sale the game made, or a character is told he sold something he lost another way (the player's own sale,
// a war seizure, an inheritance), which would be a false memory.

#region

using CalradiaRemembers.Logic.Ventures;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class WorkshopSaleMemoryPolicyTests
   {
      private static WorkshopSaleFacts Bought(string trade = "Brewery", string town = "Pravend", string player = "Arwa")
         => new() {NewOwnerIsPlayer = true, OldOwnerIsPlayer = false, HasOldOwner = true, Trade = trade, Town = town, PlayerName = player};

      // STAKE (Windmolen): the seller knows he sold, to whom and where, so he does not ask for a sealed deed.
      [Test]
      public void GIVEN_the_player_buys_a_characters_workshop_WHEN_the_seller_remembers_THEN_he_knows_he_sold_it_to_them()
      {
         WorkshopSaleMemoryPolicy.SellerMemory(Bought()).Should().Be("Sold my brewery in Pravend to Arwa, paid in full. It is theirs now.");
      }

      // STAKE: a workshop the player sold or lost to a war was not bought from anyone; nobody may remember selling it to them.
      [Test]
      public void GIVEN_the_workshop_was_the_players_WHEN_it_changes_hands_THEN_nobody_remembers_selling_it_to_them()
      {
         WorkshopSaleMemoryPolicy.SellerMemory(new WorkshopSaleFacts {NewOwnerIsPlayer = false, OldOwnerIsPlayer = true, HasOldOwner = true})
                                 .Should().BeNull();
      }

      // STAKE: a change between two other characters (an inheritance, a bankruptcy) is not a sale to the player.
      [Test]
      public void GIVEN_a_workshop_passing_between_two_characters_WHEN_it_changes_hands_THEN_no_sale_to_the_player_is_remembered()
      {
         WorkshopSaleMemoryPolicy.SellerMemory(new WorkshopSaleFacts {NewOwnerIsPlayer = false, OldOwnerIsPlayer = false, HasOldOwner = true})
                                 .Should().BeNull();
      }

      // STAKE: with no former owner there is no seller to give the memory to.
      [Test]
      public void GIVEN_a_workshop_with_no_former_owner_WHEN_the_player_takes_it_THEN_no_memory_is_made()
      {
         WorkshopSaleMemoryPolicy.SellerMemory(new WorkshopSaleFacts {NewOwnerIsPlayer = true, HasOldOwner = false}).Should().BeNull();
      }

      // STAKE: a missing name or place still gives a true line, never a blank in the middle of it.
      [Test]
      public void GIVEN_the_trade_town_and_name_are_unknown_WHEN_the_seller_remembers_THEN_the_line_still_reads_true()
      {
         WorkshopSaleMemoryPolicy.SellerMemory(Bought(trade: "", town: " ", player: null))
                                 .Should().Be("Sold my workshop to the player, paid in full. It is theirs now.");
      }
   }
}
