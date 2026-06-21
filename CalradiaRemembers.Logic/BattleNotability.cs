// Code written by Gabriel Mailhot, 21/06/2026.
// What counts as a battle worth remembering — recorded into world-events, the chronicle, and the
// profiles of lords who were there. Only a clash of two armies, or a siege, qualifies; a field
// skirmish (a lord vs a lone party, a bandit chase) is noise that would bloat the save. Pure so the
// rule that bounds memory growth stays explicit and tested.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a battle is notable enough to record. The host reads the army/siege facts.</summary>
    public static class BattleNotability
    {
        /// <summary>
        ///   True only for a siege, or a battle in which BOTH sides field an army — the engagements a
        ///   lord (and the chronicle) would actually remember. Everything else is skipped.
        /// </summary>
        public static bool IsNotable(bool isSiege, bool attackerHasArmy, bool defenderHasArmy)
            => isSiege || (attackerHasArmy && defenderHasArmy);
    }
}
