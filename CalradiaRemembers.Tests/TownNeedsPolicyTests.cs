// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "a 'bring me' iron ore, horses, beer, hardwood, whatever the city needs", read from what
// the town's own market shows. A trade good that costs twice its worth in a town is one it is short of. If these
// rules are wrong, a governor asks for goods his market is full of, or never asks for what it truly lacks, or a
// supply for his town and a supply for his own host are confused at the hand-over.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class TownNeedsPolicyTests
    {
        // ── What a town lacks ─────────────────────────────────────────────────────────────────────

        // Iron at three times its worth: the smiths are idle. Twice its worth is the line.
        [Test]
        public void GIVEN_a_trade_good_twice_its_worth_or_more_WHEN_read_THEN_the_town_lacks_it()
        {
            TownNeedsPolicy.Lacks(new MarketReading {CategoryId = "iron", IsTradeGood = true, PriceFactor = 3f}).Should().BeTrue();
            TownNeedsPolicy.Lacks(new MarketReading {CategoryId = "iron", IsTradeGood = true, PriceFactor = TownNeedsPolicy.LackingPriceFactor}).Should().BeTrue();
            TownNeedsPolicy.Lacks(new MarketReading {CategoryId = "iron", IsTradeGood = true, PriceFactor = 1.4f}).Should().BeFalse();
        }

        // Weapons and armour are priced in a narrow band (0.8-1.3) whatever the market holds: their price says
        // nothing about shortage, so they are never read as a need.
        [Test]
        public void GIVEN_something_that_is_not_a_trade_good_WHEN_read_THEN_it_is_never_a_need()
        {
            TownNeedsPolicy.Lacks(new MarketReading {CategoryId = "arms", IsTradeGood = false, PriceFactor = 9f}).Should().BeFalse();
        }

        // The governor asks for what is scarcest; a town that lacks nothing asks for nothing.
        [Test]
        public void GIVEN_several_goods_WHEN_the_most_lacking_is_asked_THEN_it_is_the_dearest_of_those_lacking()
        {
            var readings = new[] {
                new MarketReading {CategoryId = "beer", IsTradeGood = true, PriceFactor = 2.4f},
                new MarketReading {CategoryId = "hardwood", IsTradeGood = true, PriceFactor = 4.1f},
                new MarketReading {CategoryId = "wool", IsTradeGood = true, PriceFactor = 0.6f}
            };

            TownNeedsPolicy.MostLacking(readings).CategoryId.Should().Be("hardwood");
            TownNeedsPolicy.MostLacking(new[] {new MarketReading {CategoryId = "wool", IsTradeGood = true, PriceFactor = 1f}}).Should().BeNull();
        }

        // ── Whose supply it is ────────────────────────────────────────────────────────────────────

        // A market good travels as its own key, never confused with the giver's personal horses, livestock or grain.
        [Test]
        public void GIVEN_a_market_good_WHEN_keyed_THEN_its_category_reads_back_and_the_personal_keys_are_not_market_goods()
        {
            ProvideGoodsPolicy.MarketCategoryId(ProvideGoodsPolicy.MarketKey("iron")).Should().Be("iron");
            ProvideGoodsPolicy.MarketCategoryId(ProvideGoodsPolicy.Grain).Should().BeNull();
            ProvideGoodsPolicy.MarketCategoryId(ProvideGoodsPolicy.MarketKeyPrefix).Should().BeNull();
        }
    }
}
