// Code written by Gabriel Mailhot, 01/10/2026.
// A hungry town remembers who fed it, and who emptied its market. fkasad (Nexus, 01/10/2026): "a large quantity of grain
// sold at a low price (such as after a siege). That is the sort of thing a starving population would remember. And, of
// course, the reverse applies, such as buying up all the supplies in a city's market." Lordfadooboo agreed the same day.
//
// Hungry is read the way the weekly hardship news reads it (WorldConditionsFeeder: stores low and falling), plus the
// game's own starving flag; the floor lives here now, one home for both. A few sacks are trade, not news: only a large
// load in one visit is remembered, once a week per town.
//
// Increment 2 (same day): grain a character asked for while a place he answers for goes hungry is not his to keep.
// It goes into that place's stores, and the place remembers who brought it.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What a town's granary says.</summary>
    public sealed class TownFoodFacts
    {
        /// <summary>The game's own reckoning (Settlement.IsStarving).</summary>
        public bool IsStarving { get; set; }

        /// <summary>Town.FoodStocks.</summary>
        public float FoodStocks { get; set; }

        /// <summary>Town.FoodChange, per day.</summary>
        public float FoodChange { get; set; }
    }

    /// <summary>One visit's food trade in a town.</summary>
    public sealed class FoodTradeFacts
    {
        public bool TownHungry { get; set; }

        /// <summary>Units of food the player sold into the town's market this visit.</summary>
        public int FoodSold { get; set; }

        /// <summary>Units of food the player bought out of it.</summary>
        public int FoodBought { get; set; }

        /// <summary>Days since this town last remembered a food trade of the player's; null when never.</summary>
        public int? DaysSinceLastRemembered { get; set; }
    }

    /// <summary>A place a character answers for, as a destination for the grain he asked for.</summary>
    public sealed class ReliefSeat
    {
        /// <summary>The settlement's id.</summary>
        public string SettlementId { get; set; }

        /// <summary>Its granary now.</summary>
        public TownFoodFacts Food { get; set; }
    }

    /// <summary>What the town remembers of a visit's food trade.</summary>
    public enum FoodTradeVerdict
    {
        None,

        /// <summary>The player brought food to a hungry town.</summary>
        Relief,

        /// <summary>The player bought up a hungry town's food.</summary>
        Hoarding
    }

    /// <summary>A hungry town's memory of the player's food trade. Pure.</summary>
    public static class TownFoodPolicy
    {
        /// <summary>Stores at or below this, and falling, are short: "Rations run short in X".</summary>
        public const float ShortStocksFloor = 100f;

        /// <summary>Food units sold into a hungry town in one visit that the town remembers as relief.</summary>
        public const int ReliefLoads = 40;

        /// <summary>Food units bought out of a hungry town in one visit that it remembers as hoarding.</summary>
        public const int HoardingLoads = 30;

        /// <summary>One memory a week per town: a relief delivered in several trips is one relief.</summary>
        public const int RememberEveryDays = 7;

        /// <summary>Whether the town is hungry.</summary>
        public static bool IsHungry(TownFoodFacts f)
            => f != null && (f.IsStarving || f.FoodStocks <= ShortStocksFloor && f.FoodChange < 0f);

        /// <summary>
        ///   Where the grain a character asked for goes: the first hungry place among those he answers for, closest
        ///   first (what he governs, then what he holds, then a notable's home), or null when none is hungry and the
        ///   grain is simply his.
        /// </summary>
        public static string ReliefDestination(IEnumerable<ReliefSeat> seatsClosestFirst)
        {
            if (seatsClosestFirst == null) return null;

            foreach (ReliefSeat seat in seatsClosestFirst)
                if (seat != null && !string.IsNullOrEmpty(seat.SettlementId) && IsHungry(seat.Food))
                    return seat.SettlementId;

            return null;
        }

        /// <summary>The stores once the grain is in: never past the granary's own ceiling, never below what was there.</summary>
        public static float StocksAfterRelief(float stocks, int units, float ceiling)
        {
            float after = stocks + (units > 0 ? units : 0);

            return ceiling > 0f && after > ceiling ? (stocks > ceiling ? stocks : ceiling) : after;
        }

        /// <summary>What the town remembers of this visit's food trade, if anything.</summary>
        public static FoodTradeVerdict Judge(FoodTradeFacts f)
        {
            if (f == null || !f.TownHungry) return FoodTradeVerdict.None;
            if (f.DaysSinceLastRemembered is int days && days < RememberEveryDays) return FoodTradeVerdict.None;

            int net = f.FoodSold - f.FoodBought;
            if (net >= ReliefLoads) return FoodTradeVerdict.Relief;

            return -net >= HoardingLoads ? FoodTradeVerdict.Hoarding : FoodTradeVerdict.None;
        }
    }
}
