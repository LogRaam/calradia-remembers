// Code written by Gabriel Mailhot, 01/10/2026.
// A hungry town remembers who fed it, and who emptied its market (fkasad, Nexus, 01/10/2026: "a large quantity of grain
// sold at a low price (such as after a siege). That is the sort of thing a starving population would remember. And, of
// course, the reverse applies, such as buying up all the supplies in a city's market."). If these rules are wrong, a
// town remembers every sack of grain a trader passes through, or a famine relief goes unnoticed, or a player is
// branded a hoarder for buying his own rations in a well-fed town.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    // Increment 2: grain a governor, lord or notable asked for while his town goes hungry feeds the town, not his
    // own cellar. If this is wrong, the player answers a famine plea and the town starves on regardless, or a
    // lord's grain for his own host vanishes into a granary that was never short.
    [TestFixture]
    public sealed class TownFoodPolicyTests
    {
        // ── When is a town hungry ─────────────────────────────────────────────────────────────────

        // Starving by the game's own reckoning, or its stores low and falling: the same reading the weekly
        // hardship news already uses ("Rations run short in X").
        [Test]
        public void GIVEN_a_town_starving_or_low_and_falling_WHEN_read_THEN_it_is_hungry_and_a_town_merely_low_but_rising_is_not()
        {
            TownFoodPolicy.IsHungry(new TownFoodFacts {IsStarving = true, FoodStocks = 300}).Should().BeTrue();
            TownFoodPolicy.IsHungry(new TownFoodFacts {FoodStocks = 50, FoodChange = -3}).Should().BeTrue();
            TownFoodPolicy.IsHungry(new TownFoodFacts {FoodStocks = 50, FoodChange = 2}).Should().BeFalse();
        }

        // ── What the town remembers ───────────────────────────────────────────────────────────────

        // "A large quantity of grain... a starving population would remember."
        [Test]
        public void GIVEN_a_hungry_town_WHEN_the_player_sells_it_a_large_load_of_food_THEN_it_is_remembered_as_relief()
        {
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodSold = TownFoodPolicy.ReliefLoads}).Should().Be(FoodTradeVerdict.Relief);
        }

        // "The reverse applies, such as buying up all the supplies."
        [Test]
        public void GIVEN_a_hungry_town_WHEN_the_player_buys_up_its_food_THEN_it_is_remembered_as_hoarding()
        {
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodBought = TownFoodPolicy.HoardingLoads}).Should().Be(FoodTradeVerdict.Hoarding);
        }

        // A few sacks are trade, not news; and a well-fed town remembers neither.
        [Test]
        public void GIVEN_a_small_trade_or_a_well_fed_town_WHEN_judged_THEN_nothing_is_remembered()
        {
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodSold = TownFoodPolicy.ReliefLoads - 1}).Should().Be(FoodTradeVerdict.None);
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = false, FoodSold = 500, FoodBought = 500}).Should().Be(FoodTradeVerdict.None);
        }

        // Selling and buying in one visit: what the town keeps is the balance.
        [Test]
        public void GIVEN_food_both_sold_and_bought_WHEN_judged_THEN_the_balance_decides()
        {
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodSold = 100, FoodBought = 20}).Should().Be(FoodTradeVerdict.Relief);
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodSold = 20, FoodBought = 100}).Should().Be(FoodTradeVerdict.Hoarding);
        }

        // ── Where the grain asked for goes ───────────────────────────────────────────────────────

        // The governor who asked for grain while his town starves asked for the town. Closest seat first: what he
        // governs day to day before a fief he merely holds, the same order his prompt describes them in.
        [Test]
        public void GIVEN_a_character_whose_town_is_hungry_WHEN_the_grain_he_asked_for_arrives_THEN_it_goes_to_the_closest_hungry_place()
        {
            var seats = new[] {
                new ReliefSeat {SettlementId = "fed_governed", Food = new TownFoodFacts {FoodStocks = 400, FoodChange = 2}},
                new ReliefSeat {SettlementId = "hungry_held", Food = new TownFoodFacts {IsStarving = true}},
                new ReliefSeat {SettlementId = "hungry_too", Food = new TownFoodFacts {FoodStocks = 20, FoodChange = -1}}
            };

            TownFoodPolicy.ReliefDestination(seats).Should().Be("hungry_held");
        }

        // Nothing is hungry: the grain is his, as a supply task always was (horses for the cavalry, grain for the host).
        [Test]
        public void GIVEN_no_hungry_place_WHEN_the_grain_arrives_THEN_it_stays_with_the_character_who_asked()
        {
            TownFoodPolicy.ReliefDestination(new[] {new ReliefSeat {SettlementId = "fed", Food = new TownFoodFacts {FoodStocks = 400}}}).Should().BeNull();
            TownFoodPolicy.ReliefDestination(null).Should().BeNull();
        }

        // The granary has the game's own ceiling: grain past it is not conjured into stores the town cannot hold,
        // and a delivery never lowers a granary already over it.
        [Test]
        public void GIVEN_a_delivery_WHEN_it_fills_the_stores_THEN_they_rise_by_the_load_up_to_the_ceiling()
        {
            TownFoodPolicy.StocksAfterRelief(stocks: 30, units: 50, ceiling: 500).Should().Be(80);
            TownFoodPolicy.StocksAfterRelief(stocks: 480, units: 50, ceiling: 500).Should().Be(500);
            TownFoodPolicy.StocksAfterRelief(stocks: 520, units: 50, ceiling: 500).Should().Be(520);
        }

        // One memory a week per town: a relief delivered in ten trips is one relief, not ten.
        [Test]
        public void GIVEN_a_memory_recorded_days_ago_WHEN_judged_again_THEN_nothing_new_until_the_week_has_passed()
        {
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodSold = 200, DaysSinceLastRemembered = 2}).Should().Be(FoodTradeVerdict.None);
            TownFoodPolicy.Judge(new FoodTradeFacts {TownHungry = true, FoodSold = 200, DaysSinceLastRemembered = TownFoodPolicy.RememberEveryDays}).Should().Be(FoodTradeVerdict.Relief);
        }
    }
}
