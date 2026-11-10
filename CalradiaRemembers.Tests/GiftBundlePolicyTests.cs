// Code written by Gabriel Mailhot, 29/10/2026.
// Several goods given at once, each in a chosen amount. fearente (Nexus, 29/10/2026): he wanted to hand over grain, fish
// and meat in one gesture and in different amounts, and could only give one kind at a time, one unit each. If the amount
// is misread, the wrong quantity leaves the party; if the bundle is misnamed, the character thanks him for something else;
// if its welcome is misweighed, a gift holding what they love is received coldly.

#region

using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GiftBundlePolicyTests
   {
      // He typed how many fish; that many leave, never more than he carries.
      [Test]
      public void GIVEN_an_amount_typed_within_what_he_carries_WHEN_read_THEN_that_many_are_given()
      {
         GiftBundlePolicy.Amount("20", 35).Should().Be(20);
         GiftBundlePolicy.Amount("35", 35).Should().Be(35);
      }

      // The box opens filled with all he carries; pressing Enter on it, or clearing it, gives the whole stack.
      [Test]
      public void GIVEN_no_amount_typed_WHEN_read_THEN_the_whole_stack_is_given()
      {
         GiftBundlePolicy.Amount("", 35).Should().Be(35);
         GiftBundlePolicy.Amount(null, 35).Should().Be(35);
      }

      // More than he carries, nothing, or a word: not an amount, so the bag refuses it and nothing leaves.
      [TestCase("40")]
      [TestCase("0")]
      [TestCase("-3")]
      [TestCase("lots")]
      public void GIVEN_an_amount_he_cannot_give_WHEN_read_THEN_it_is_refused(string typed)
      {
         GiftBundlePolicy.Amount(typed, 35).Should().BeNull();
      }

      // He picked 300 grain, then rewrote the line to "a sack of grain": the character thanks him for a sack, so 300 must
      // not leave behind his words. Only the line as the bag wrote it lets the bag's amounts decide.
      [Test]
      public void GIVEN_the_handover_line_edited_before_sending_WHEN_checked_THEN_the_bag_no_longer_decides()
      {
         const string written = "*I hand you 300 Grain (worth 3,000 denars).*";

         GiftBundlePolicy.BagStillDecides(written, written).Should().BeTrue();
         GiftBundlePolicy.BagStillDecides("  " + written + " ", written).Should().BeTrue();
         GiftBundlePolicy.BagStillDecides("*I hand you a sack of grain.*", written).Should().BeFalse();
         GiftBundlePolicy.BagStillDecides(written, null).Should().BeFalse();
      }

      // The line put in his mouth, and in the character's memory, names every good with its count.
      [Test]
      public void GIVEN_three_goods_in_different_amounts_WHEN_named_THEN_each_is_said_with_its_count()
      {
         GiftBundlePolicy.Describe(new[] {
            new GiftLine {Name = "Fish", Count = 20}, new GiftLine {Name = "Meat", Count = 10}, new GiftLine {Name = "Grain", Count = 1}
         }).Should().Be("20 Fish, 10 Meat and Grain");
      }

      // One good alone reads as the single gift always did.
      [Test]
      public void GIVEN_one_good_WHEN_named_THEN_it_reads_as_a_single_gift()
      {
         GiftBundlePolicy.Describe(new[] {new GiftLine {Name = "Fish", Count = 20}}).Should().Be("20 Fish");
      }

      // What You Know (Gabriel, 27/10/2026): a character prefers some gifts. One loved good in the bundle carries it.
      [Test]
      public void GIVEN_a_bundle_holding_one_thing_they_love_WHEN_weighed_THEN_it_is_received_as_loved()
      {
         GiftBundlePolicy.Welcome(new[] {GiftWelcome.Cold, GiftWelcome.Loved, GiftWelcome.Ordinary}).Should().Be(GiftWelcome.Loved);
      }

      // Cold only when every good in it leaves them cold; one ordinary good keeps the whole ordinary.
      [Test]
      public void GIVEN_a_bundle_cold_in_part_WHEN_weighed_THEN_it_is_cold_only_if_all_of_it_is()
      {
         GiftBundlePolicy.Welcome(new[] {GiftWelcome.Cold, GiftWelcome.Ordinary}).Should().Be(GiftWelcome.Ordinary);
         GiftBundlePolicy.Welcome(new[] {GiftWelcome.Cold, GiftWelcome.Cold}).Should().Be(GiftWelcome.Cold);
      }

      // Several goods laid out in the bag for one price (froggyluv, 04/11/2026; Gabriel 09/11/2026): the lot is sold whole.

      // STAKE: grain, fish and meat laid out for 900 denars all leave together, paid once.
      [Test]
      public void GIVEN_every_good_of_the_lot_still_held_WHEN_the_sale_is_struck_THEN_the_lot_can_be_sold()
      {
         GiftBundlePolicy.WholeLotHeld(new[] {new LaidOutGood {Count = 20, Held = 35}, new LaidOutGood {Count = 10, Held = 10}}).Should().BeTrue();
      }

      // STAKE: the fish went to someone else since the price was named; the buyer must not pay 900 for the grain alone.
      [Test]
      public void GIVEN_one_good_no_longer_held_in_full_WHEN_the_sale_is_struck_THEN_nothing_of_the_lot_is_sold()
      {
         GiftBundlePolicy.WholeLotHeld(new[] {new LaidOutGood {Count = 20, Held = 35}, new LaidOutGood {Count = 10, Held = 4}}).Should().BeFalse();
         GiftBundlePolicy.WholeLotHeld(new[] {new LaidOutGood {Count = 20, Held = 35}, new LaidOutGood {Count = 10, Held = 0}}).Should().BeFalse();
      }

      // STAKE: an empty bag sells nothing, whatever price was agreed.
      [Test]
      public void GIVEN_an_empty_lot_WHEN_the_sale_is_struck_THEN_nothing_is_sold()
      {
         GiftBundlePolicy.WholeLotHeld(new LaidOutGood[0]).Should().BeFalse();
         GiftBundlePolicy.WholeLotHeld(null).Should().BeFalse();
      }
   }
}
