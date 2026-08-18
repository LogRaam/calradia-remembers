// Code written by Gabriel Mailhot, 08/08/2026.
// The words a battle leaves in the profile of someone who stood on the player's side, and in the player's own
// chronicle. Shared by the places that write it: the lords who led their own party in the fight, the companions who
// rode inside the player's own party (a player report, 08/08/2026), and the world-event/chronicle feeder. Pure so
// the phrasing stays identical everywhere and is pinned by tests.
// 17/08/2026 (player report): a siege was recorded as an "open battle near" the settlement, and a battle fought as
// part of an army read as though the player won it alone. The place clause is now SETTING-aware (siege / raid /
// open field) and the ally line reflects a shared effort when the player fought within a larger host.

namespace CalradiaRemembers.Logic
{
    /// <summary>What kind of engagement a battle was, for naming it correctly (a siege is not a field battle).</summary>
    public enum BattleSetting
    {
        /// <summary>A field battle (possibly near a settlement, but not an assault on it).</summary>
        OpenField,

        /// <summary>An assault on a besieged settlement.</summary>
        Siege,

        /// <summary>A raid on a village or caravan.</summary>
        Raid
    }

    /// <summary>Builds the "fought beside you" memory line and the place/engagement phrasing. The host supplies the facts.</summary>
    public static class BattleMemoryText
    {
        /// <summary>
        ///   A trailing place clause shaped for a summary or memory line: " at the siege of X", " in the raid on X",
        ///   or " near X" for an ordinary field battle beside a settlement. Empty when no settlement is involved.
        /// </summary>
        public static string PlaceClause(BattleSetting setting, string place)
        {
            if (string.IsNullOrWhiteSpace(place)) return string.Empty;

            switch (setting)
            {
                case BattleSetting.Siege: return $" at the siege of {place}";
                case BattleSetting.Raid: return $" in the raid on {place}";
                default: return $" near {place}";
            }
        }

        /// <summary>
        ///   The engagement named for the player's chronicle: "the siege of X", "the raid on X", "open battle near
        ///   X", or plain "open battle" when no settlement is involved. Used where a noun phrase reads better than a
        ///   trailing clause.
        /// </summary>
        public static string ChronicleEngagement(BattleSetting setting, string place)
        {
            bool hasPlace = !string.IsNullOrWhiteSpace(place);

            switch (setting)
            {
                case BattleSetting.Siege: return hasPlace ? $"the siege of {place}" : "a siege";
                case BattleSetting.Raid: return hasPlace ? $"the raid on {place}" : "a raid";
                default: return hasPlace ? $"open battle near {place}" : "open battle";
            }
        }

        /// <summary>
        ///   The line left in an ally's (or companion's) profile after a notable battle the player fought in.
        ///   <paramref name="placeClause" /> is already shaped by <see cref="PlaceClause" /> (or empty).
        ///   <paramref name="sharedEffort" /> is true when the player fought as part of a larger host (an army, or
        ///   allies beside them), so the memory reads as a shared victory rather than crediting the player alone,
        ///   the player report where lords congratulated a solo triumph after an army siege.
        /// </summary>
        public static string AllySummary(bool playerWon, string placeClause, bool sharedEffort)
        {
            if (playerWon)
                return sharedEffort
                    ? $"Fought at your side when we broke the enemy together{placeClause}."
                    : $"Fought at your side when you broke the enemy{placeClause}.";

            return sharedEffort
                ? $"Fought at your side when we stood against the enemy{placeClause}."
                : $"Fought at your side when you stood against the enemy{placeClause}.";
        }
    }
}
