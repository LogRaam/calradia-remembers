// Code written by Gabriel Mailhot, 25/08/2026.
// New letter reason ServiceOffer: a lord writes to poach the player ("leave your lord, serve me"), dangling a
// concrete enticement. This pure policy picks WHICH enticement fits the lord's means and holds the light warm-up
// accepting the courtship gives. The hook is deliberately light (Gabriel): accepting warms the bond and signals
// interest, it does NOT change the player's allegiance or hand over the prize, which stays the letter's PROMISE.

namespace CalradiaRemembers.Logic
{
    /// <summary>The kind of enticement a poaching lord dangles in a ServiceOffer letter.</summary>
    public enum ServiceEnticement
    {
        /// <summary>A purse of gold (the fallback when the lord has neither a spare fief nor men to promise).</summary>
        Gold,

        /// <summary>A castle of the player's own (only a lord with a fief to spare can promise it).</summary>
        Castle,

        /// <summary>A company of soldiers (a lord who leads men in the field).</summary>
        Troops
    }

    /// <summary>Chooses the ServiceOffer enticement and holds the light accept warm-up. See the file header.</summary>
    public static class ServiceOfferPolicy
    {
        /// <summary>
        ///   The modest regard warm-up accepting the offer gives the poaching lord. Light on purpose: accepting is a
        ///   signal of interest and courtesy, not a defection, so it nudges the bond rather than buying allegiance.
        /// </summary>
        public const int AcceptRelationWarmup = 5;

        /// <summary>
        ///   Which enticement the lord dangles, by their means: a castle if they have a spare fief to grant, else a
        ///   company of soldiers if they lead men in the field, else a purse of gold. Deterministic on facts so it
        ///   is unit-tested; the letter's Context turns the choice into prose.
        /// </summary>
        public static ServiceEnticement ChooseEnticement(bool canOfferCastle, bool canOfferTroops)
            => canOfferCastle ? ServiceEnticement.Castle
             : canOfferTroops ? ServiceEnticement.Troops
             : ServiceEnticement.Gold;
    }
}
