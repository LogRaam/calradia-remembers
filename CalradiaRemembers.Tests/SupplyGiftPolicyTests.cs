// Code written by Gabriel Mailhot, 26/10/2026.
// SupplyGiftPolicy decides what a gift of goods comes to when the receiver has a supply task outstanding: froggyluv
// (Nexus, 26/10/2026) handed lords their famine grain in words or through the Give menu, "the flavor text will say i
// delivered but the NPC still complain of my broken promises". If this is wrong, delivered grain vanishes from the party
// while the task expires as a broken promise, or a few bags handed over fake a delivery of fifty.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SupplyGiftPolicyTests
   {
      // STAKE: froggyluv's report: grain handed over in words, carried in full, must keep the promise, not break it.
      [Test]
      public void GIVEN_the_lord_asked_for_grain_and_the_party_carries_enough_WHEN_handed_over_in_words_THEN_the_task_is_done()
      {
         SupplyGiftPolicy.Read(new SupplyGiftFacts {TaskAsksTheseGoods = true, Held = 60, Asked = 40}).Should().Be(SupplyGiftReading.Settles);
      }

      // STAKE: the bag refuses a short delivery and takes nothing; words must not let ten bags pass for forty.
      [Test]
      public void GIVEN_the_party_carries_less_than_asked_WHEN_handed_over_in_words_THEN_nothing_leaves()
      {
         SupplyGiftPolicy.Read(new SupplyGiftFacts {TaskAsksTheseGoods = true, Held = 10, Asked = 40}).Should().Be(SupplyGiftReading.Short);
      }

      // STAKE: a gift no task asks for stays a gift: trust, gratitude and a softened grudge, as before.
      [Test]
      public void GIVEN_no_task_asks_for_these_goods_WHEN_given_THEN_it_is_a_plain_gift()
      {
         SupplyGiftPolicy.Read(new SupplyGiftFacts {TaskAsksTheseGoods = false, Held = 60, Asked = 40}).Should().Be(SupplyGiftReading.NotTheTask);
      }
   }
}
