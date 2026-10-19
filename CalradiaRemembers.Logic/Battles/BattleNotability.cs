// Code written by Gabriel Mailhot, 21/06/2026.
// What counts as a battle worth remembering — recorded into world-events, the chronicle, and the
// profiles of lords who were there. Only a clash of two armies, or a siege, qualifies; a field
// skirmish (a lord vs a lone party, a bandit chase) is noise that would bloat the save. Pure so the
// rule that bounds memory growth stays explicit and tested.
// 04/07/2026: a PLAYER battle also earns its place in the chronicle when it was simply large (many
// combatants), even without armies on both sides — a great scrap the player led is worth a line even
// when neither side happened to be marching as a formal army.

namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>Decides whether a battle is notable enough to record. The host reads the army/siege facts.</summary>
    public static class BattleNotability
    {
        /// <summary>Total combatants across both sides at/above which a player battle is chronicle-worthy on size alone.</summary>
        public const int PlayerBattleSizeThreshold = 150;

        /// <summary>
        ///   What kind of engagement this was. Three booleans in a row of which two are the two SIDES: crossing
        ///   them changes nothing here, since the rule wants armies on BOTH, but it would the moment anybody
        ///   asked about one side alone. Shared with <see cref="IsNotableForPlayer" />, which adds the size.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class BattleFacts
        {
           /// <summary>A siege, which is remembered whatever the numbers.</summary>
           public bool IsSiege { get; set; }

           /// <summary>The attacking side fielded a full army, not a lone party.</summary>
           public bool AttackerHasArmy { get; set; }

           /// <summary>The defending side fielded a full army.</summary>
           public bool DefenderHasArmy { get; set; }

           /// <summary>
           ///   Everyone who fought, read only by <see cref="IsNotableForPlayer" />: a field clash the player was
           ///   in is chronicle-worthy once it is large enough, armies or not.
           /// </summary>
           public int TotalCombatants { get; set; }
        }

        /// <summary>
        ///   True only for a siege, or a battle in which BOTH sides field an army — the engagements a
        ///   lord (and the chronicle) would actually remember. Everything else is skipped.
        /// </summary>
        public static bool IsNotable(BattleFacts facts)
        {
           if (facts == null) return false;

           bool isSiege = facts.IsSiege;
           bool attackerHasArmy = facts.AttackerHasArmy;
           bool defenderHasArmy = facts.DefenderHasArmy;

           return isSiege || (attackerHasArmy && defenderHasArmy);
        }

        /// <summary>
        ///   As <see cref="IsNotable" />, but for a battle the PLAYER fought in: a field clash large enough
        ///   (<c>TotalCombatants</c> at/above <see cref="PlayerBattleSizeThreshold" />) is also
        ///   chronicle-worthy, even without armies on both sides.
        /// </summary>
        public static bool IsNotableForPlayer(BattleFacts facts)
        {
           if (facts == null) return false;

           bool isSiege = facts.IsSiege;
           bool attackerHasArmy = facts.AttackerHasArmy;
           bool defenderHasArmy = facts.DefenderHasArmy;
           int totalCombatants = facts.TotalCombatants;

           return BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = isSiege, AttackerHasArmy = attackerHasArmy, DefenderHasArmy = defenderHasArmy}) || totalCombatants >= PlayerBattleSizeThreshold;
        }
    }
}
