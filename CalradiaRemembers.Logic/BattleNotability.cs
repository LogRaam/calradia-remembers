// Code written by Gabriel Mailhot, 21/06/2026.
// What counts as a battle worth remembering — recorded into world-events, the chronicle, and the
// profiles of lords who were there. Only a clash of two armies, or a siege, qualifies; a field
// skirmish (a lord vs a lone party, a bandit chase) is noise that would bloat the save. Pure so the
// rule that bounds memory growth stays explicit and tested.
// 04/07/2026: a PLAYER battle also earns its place in the chronicle when it was simply large (many
// combatants), even without armies on both sides — a great scrap the player led is worth a line even
// when neither side happened to be marching as a formal army.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a battle is notable enough to record. The host reads the army/siege facts.</summary>
    public static class BattleNotability
    {
        /// <summary>Total combatants across both sides at/above which a player battle is chronicle-worthy on size alone.</summary>
        public const int PlayerBattleSizeThreshold = 150;

        /// <summary>
        ///   True only for a siege, or a battle in which BOTH sides field an army — the engagements a
        ///   lord (and the chronicle) would actually remember. Everything else is skipped.
        /// </summary>
        public static bool IsNotable(bool isSiege, bool attackerHasArmy, bool defenderHasArmy)
            => isSiege || (attackerHasArmy && defenderHasArmy);

        /// <summary>
        ///   As <see cref="IsNotable" />, but for a battle the PLAYER fought in: a field clash large enough
        ///   (<paramref name="totalCombatants" /> at/above <see cref="PlayerBattleSizeThreshold" />) is also
        ///   chronicle-worthy, even without armies on both sides.
        /// </summary>
        public static bool IsNotableForPlayer(bool isSiege, bool attackerHasArmy, bool defenderHasArmy, int totalCombatants)
            => IsNotable(isSiege, attackerHasArmy, defenderHasArmy) || totalCombatants >= PlayerBattleSizeThreshold;
    }
}
