// Code written by Gabriel Mailhot, 10/11/2026.
// What CompanyGiftPolicy decides: where an item handed to someone riding in the player's company ends up. If it is
// wrong, gear handed to a companion vanishes (Aporo, Discord 10/11/2026: "character took item and never equipped it"),
// the piece they took off vanishes, or the same sword handed over again and again buys regard each time.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanyGiftPolicyTests
   {
      // STAKE (Aporo): a piece the companion does not put on goes back to the stores, and is no gift.
      [Test]
      public void GIVEN_a_piece_not_put_on_WHEN_settled_THEN_it_goes_back_to_the_stores_and_counts_as_no_gift()
      {
         CompanyGiftOutcome o = CompanyGiftPolicy.Settle(new CompanyGiftFacts {Count = 1, PutOn = false});

         o.BackToStores.Should().Be(1);
         o.KeptAsGift.Should().Be(0);
         o.ReplacedPieceToStores.Should().BeFalse();
      }

      // STAKE: the piece put on is theirs, and the one it replaced is not lost: it goes back to the stores.
      [Test]
      public void GIVEN_a_piece_put_on_in_place_of_another_WHEN_settled_THEN_it_is_kept_and_the_old_one_goes_back()
      {
         CompanyGiftOutcome o = CompanyGiftPolicy.Settle(new CompanyGiftFacts {Count = 1, PutOn = true, ReplacedAPiece = true});

         o.KeptAsGift.Should().Be(1);
         o.BackToStores.Should().Be(0);
         o.ReplacedPieceToStores.Should().BeTrue();
      }

      // STAKE: three of a kind handed over, one worn: the other two are still the company's.
      [Test]
      public void GIVEN_three_handed_over_and_one_put_on_WHEN_settled_THEN_two_go_back()
      {
         CompanyGiftOutcome o = CompanyGiftPolicy.Settle(new CompanyGiftFacts {Count = 3, PutOn = true});

         o.KeptAsGift.Should().Be(1);
         o.BackToStores.Should().Be(2);
      }

      // STAKE: putting a piece into an empty slot takes nothing off, so nothing extra appears in the stores.
      [Test]
      public void GIVEN_a_piece_put_on_an_empty_slot_WHEN_settled_THEN_nothing_else_goes_back()
      {
         CompanyGiftPolicy.Settle(new CompanyGiftFacts {Count = 1, PutOn = true, ReplacedAPiece = false}).ReplacedPieceToStores.Should().BeFalse();
      }
   }
}
