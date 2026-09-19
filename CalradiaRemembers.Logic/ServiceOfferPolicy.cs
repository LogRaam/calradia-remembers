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

        /// <summary>
        ///   How long the ruler of a realm the player has just left stays silent before writing to court them.
        ///   tashmetu (Nexus, 19/09/2026) was expelled from his kingdom after a lost war, and letters kept arriving
        ///   "as if I were still part of it". A king who has just put the player out does not write within weeks to
        ///   poach them back as though nothing had happened; after a season, an overture is a real turn of events.
        /// </summary>
        public const int FormerLiegeSilenceDays = 120;

        /// <summary>
        ///   Whether the ruler of a realm may write. <paramref name="daysSincePlayerLeftThisRealm" /> is null when
        ///   the player never left it (or never belonged), which is every realm but a former one.
        /// </summary>
        public static bool RulerMayWrite(int? daysSincePlayerLeftThisRealm)
            => daysSincePlayerLeftThisRealm == null || daysSincePlayerLeftThisRealm.Value >= FormerLiegeSilenceDays;

        /// <summary>
        ///   The premise the letter is written on, from the player's LIVE allegiance. The old wording always said
        ///   "leave the lord they now serve", so a player who served no one read a letter from a ruler who plainly
        ///   believed they still did. <paramref name="playerRealm" /> is the realm the player serves now, or null.
        /// </summary>
        public static string Premise(string rulerRealm, string playerRealm)
            => string.IsNullOrWhiteSpace(playerRealm)
                ? $"You are the ruler of {rulerRealm}. The player serves no realm at present, sworn to no liege, and you would have them enter YOUR service."
                : $"You are the ruler of {rulerRealm}, and you would have the player leave {playerRealm}, which they now serve, and enter YOUR service instead.";
    }
}
