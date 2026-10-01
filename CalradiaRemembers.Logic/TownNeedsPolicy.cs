// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "the city management screen (mouse overing the basket) shows the city daily needs to
// compare with what is available from the market. Leading to a 'bring me' iron ore, horses, beer, hardwood, whatever
// the city needs." What a town lacks is read the way its own market reads it: the game prices every trade good by
// demand against supply (TradeItemPriceFactorModel.GetBasePriceFactor, 0.1 to 10 times the item's value), so a good
// that costs twice its worth here is a good this town is short of. The governor, lord or notable who answers for the
// town may ask the player to bring it; delivered, it goes into the town's market, not into his cellar.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One trade good as a town's market reads it.</summary>
    public sealed class MarketReading
    {
        /// <summary>The ItemCategory's StringId.</summary>
        public string CategoryId { get; set; }

        /// <summary>Its player-facing name.</summary>
        public string Name { get; set; }

        /// <summary>The market's base price factor for it (1 = its worth; the game clamps trade goods to 0.1-10).</summary>
        public float PriceFactor { get; set; }

        /// <summary>A trade good. Everything else is priced in a narrow band that says nothing of shortage.</summary>
        public bool IsTradeGood { get; set; }
    }

    /// <summary>What a town lacks, read off its own market. Pure.</summary>
    public static class TownNeedsPolicy
    {
        /// <summary>A good dearer than this, against its worth, is one the town is short of: twice the price.</summary>
        public const float LackingPriceFactor = 2f;

        /// <summary>Whether the town is short of this good.</summary>
        public static bool Lacks(MarketReading r)
            => r != null && r.IsTradeGood && !string.IsNullOrEmpty(r.CategoryId) && r.PriceFactor >= LackingPriceFactor;

        /// <summary>The good the town lacks most, or null when it lacks nothing.</summary>
        public static MarketReading MostLacking(IEnumerable<MarketReading> readings)
            => readings?.Where(Lacks).OrderByDescending(r => r.PriceFactor).FirstOrDefault();
    }
}
