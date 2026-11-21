// Code written by Gabriel Mailhot, 21/11/2026.
// The men a character may call HIS OWN in the prompt's YOUR OWN COMMAND section. JackJack (Discord, 21/11/2026, 2.8.17):
// his companion Isilflaed, riding in his party, was told "You command a company of a few dozen soldiers. These are your
// own men", while the same prompt said she holds no rank and the player is her captain. The count was the party she
// BELONGS to, the player's, with no check that she leads it. Only a party's leader commands it: a companion in the
// player's party, or a spouse riding in her husband's, commands nobody. The shape of the count itself (captive, army
// leader) is PlayerForcePolicy's, reused rather than forked.

namespace CalradiaRemembers.Logic
{
    /// <summary>The engine facts about the party a character rides in.</summary>
    public sealed class OwnCommandFacts
    {
        /// <summary>The character is held prisoner.</summary>
        public bool IsCaptive { get; init; }

        /// <summary>The character is the leader of the party they ride in (not merely a member of it).</summary>
        public bool LeadsTheParty { get; init; }

        /// <summary>Men in that party.</summary>
        public int PartyTroops { get; init; }

        /// <summary>The party leads an army.</summary>
        public bool LeadsArmy { get; init; }

        /// <summary>Men across every party of the army that party leads.</summary>
        public int LedArmyTroops { get; init; }
    }

    /// <summary>Decides how many men a character commands, for the YOUR OWN COMMAND section (0: no section).</summary>
    public static class OwnCommandPolicy
    {
        /// <summary>
        ///   The men the character commands: none unless they lead the party they ride in; then the party, or the
        ///   whole army it leads, and none in a cell (<see cref="PlayerForcePolicy.Resolve" />).
        /// </summary>
        public static int Resolve(OwnCommandFacts facts)
        {
            if (facts == null || !facts.LeadsTheParty) return 0;

            return PlayerForcePolicy.Resolve(facts.IsCaptive, facts.PartyTroops, facts.LeadsArmy, facts.LedArmyTroops);
        }
    }
}
