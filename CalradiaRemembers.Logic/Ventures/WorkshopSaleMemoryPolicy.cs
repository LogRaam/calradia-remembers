// Code written by Gabriel Mailhot, 14/11/2026.
// Windmolen (Nexus, 14/11/2026): "When I spoke to the brothel keeper, the character I had purchased the business from, he
// contested my ownership, wanted me to produce a sealed document or a witness ... given that the purchase is a click without
// formalities or proof it doesn't work well in game." His brothel was Captivity Events' own (that mod can tell CR through
// CrNpcMemory.AddMemory); the same gap exists for the game's own workshops: nothing told the seller he had sold. Gabriel,
// same day: "go". When the player buys a workshop, the seller keeps the memory of the sale, so he may regret it but never
// denies it: the game declares, the model does not contradict it.

namespace CalradiaRemembers.Logic.Ventures
{
    /// <summary>What the game says about one change of a workshop's owner.</summary>
    public sealed class WorkshopSaleFacts
    {
        /// <summary>The workshop now belongs to the player. The game gives the player a workshop only when they buy it.</summary>
        public bool NewOwnerIsPlayer { get; init; }

        /// <summary>The workshop belonged to the player (they sold it, lost it to a war, or died): not a purchase by the player.</summary>
        public bool OldOwnerIsPlayer { get; init; }

        /// <summary>There was a character who owned it before; with no seller there is nobody to remember.</summary>
        public bool HasOldOwner { get; init; }

        /// <summary>The kind of workshop, as the game names it ("Brewery"); blank reads "workshop".</summary>
        public string Trade { get; init; }

        /// <summary>The town it stands in; blank leaves the place out.</summary>
        public string Town { get; init; }

        /// <summary>The buyer's name; blank reads "the player".</summary>
        public string PlayerName { get; init; }
    }

    /// <summary>The memory a seller keeps of selling a workshop to the player. Pure.</summary>
    public static class WorkshopSaleMemoryPolicy
    {
        /// <summary>
        ///   The line the seller remembers, or null when this change is not the player buying from a character (the player
        ///   selling, a war, an inheritance, a bankruptcy between others).
        /// </summary>
        public static string SellerMemory(WorkshopSaleFacts facts)
        {
            if (facts == null || !facts.NewOwnerIsPlayer || facts.OldOwnerIsPlayer || !facts.HasOldOwner) return null;

            string trade = string.IsNullOrWhiteSpace(facts.Trade) ? "workshop" : facts.Trade.Trim().ToLowerInvariant();
            string town = string.IsNullOrWhiteSpace(facts.Town) ? "" : " in " + facts.Town.Trim();
            string player = string.IsNullOrWhiteSpace(facts.PlayerName) ? "the player" : facts.PlayerName.Trim();

            return $"Sold my {trade}{town} to {player}, paid in full. It is theirs now.";
        }
    }
}
