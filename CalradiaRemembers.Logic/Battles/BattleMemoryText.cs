// Code written by Gabriel Mailhot, 08/08/2026.
// The words a battle leaves in the profile of someone who stood on the player's side, and in the player's own
// chronicle. Shared by the places that write it: the lords who led their own party in the fight, the companions who
// rode inside the player's own party (a player report, 08/08/2026), and the world-event/chronicle feeder. Pure so
// the phrasing stays identical everywhere and is pinned by tests.
// 17/08/2026 (player report): a siege was recorded as an "open battle near" the settlement, and a battle fought as
// part of an army read as though the player won it alone. The place clause is now SETTING-aware (siege / raid /
// open field) and the ally line reflects a shared effort when the player fought within a larger host.

namespace CalradiaRemembers.Logic.Battles
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
        ///   <para>
        ///     tashmetu (Nexus, 13/10/2026, via cr.prompt_dump): "Fought at Can's side when they broke the enemy"
        ///     left "they" reading as either the player or the army. When the effort was NOT shared, the one who
        ///     broke the enemy is the very person just named, so the solo branches name them again rather than
        ///     reusing "they".
        ///   </para>
        /// </summary>
        public static string AllySummary(bool playerWon, string placeClause, bool sharedEffort, string playerName = null, string foe = null)
        {
            string who = NameOrPlayer(playerName);
            // Thoragoros1 (Nexus, 21/11/2026, 2.8.19): "This leaves out the single most important fact - who we fought."
            string enemy = string.IsNullOrWhiteSpace(foe) ? "the enemy" : foe.Trim();

            if (playerWon)
                return sharedEffort
                    ? $"Fought at {who}'s side when we broke {enemy} together{placeClause}."
                    : $"Fought at {who}'s side when {who} broke {enemy}{placeClause}.";

            return sharedEffort
                ? $"Fought at {who}'s side when we stood against {enemy}{placeClause}."
                : $"Fought at {who}'s side when {who} stood against {enemy}{placeClause}.";
        }

        /// <summary>
        ///   The enemy as an ally's memory names it: "the host of Count Aldric (of Vlandia)", "a band of Looters", "men of
        ///   Vlandia", the party's own name, or null when nothing is known (the line then keeps "the enemy").
        ///   <para>
        ///     Thoragoros1 (Nexus, 21/11/2026): companions remembered "Fought at Hex's side when Hex broke the enemy near
        ///     Usanc" with the losses, and never whom: "the 'who' shapes everything they say about the battle, even more
        ///     than the numbers."
        ///   </para>
        /// </summary>
        public static string Foe(BattleSideFacts side)
        {
            if (side == null) return null;
            string realm = string.IsNullOrWhiteSpace(side.FactionName) || side.IsBandit ? null : side.FactionName.Trim();

            if (!string.IsNullOrWhiteSpace(side.LeaderName))
                return realm == null ? $"the host of {side.LeaderName.Trim()}" : $"the host of {side.LeaderName.Trim()} (of {realm})";

            if (side.IsBandit) return string.IsNullOrWhiteSpace(side.PartyName) ? "a band of outlaws" : $"a band of {side.PartyName.Trim()}";
            if (realm != null) return $"men of {realm}";

            return string.IsNullOrWhiteSpace(side.PartyName) ? null : side.PartyName.Trim();
        }

        /// <summary>
        ///   The player as a memory should name them: their own name, or "the player" when the host cannot supply it.
        ///   <para>
        ///     reminensce (Nexus, 19/09/2026) watched Chief Rolan claim the PLAYER's defeat at Vladiv as his own and
        ///     throw the player's words back at them. These lines are rendered under "YOUR HISTORY WITH THIS PLAYER"
        ///     in a prompt whose every other "you" is the character themselves, so a memory reading "Fought at your
        ///     side when you broke the enemy" asked the model to flip the meaning of "you" for three lines and flip
        ///     it back afterwards. Naming the player costs a few characters and removes the question.
        ///   </para>
        /// </summary>
        public static string NameOrPlayer(string playerName)
            => string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();
    }
}
