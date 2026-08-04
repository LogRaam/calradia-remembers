// Code written by Gabriel Mailhot, 03/08/2026.
// Council bug c: CouncilRosterPolicy seats a member purely on availability (Partie 4 rule, see its own header),
// deliberately without any distance/travel test, because convening your own house is abstract. That is correct
// for WHO is seated, but it left the prompt with nothing true to say about WHERE a seated member actually
// stands, and a companion staying behind (not in the player's party) could then claim in dialogue to be "already
// at your side" on the road (author's bug report). This pure mapping gives the host a short, honest phrase for
// each seated member's real whereabouts, so the prompt can hold every member to the truth instead of leaving it
// to invent one.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   The flattened facts about ONE seated member's whereabouts relative to the player, read from live Hero
    ///   state. The three checks are mutually exclusive by construction (a member in the player's own party
    ///   cannot simultaneously lead their own company), so <see cref="MemberPresenceDescriber.Describe" /> reads
    ///   them in priority order rather than needing a combined case.
    /// </summary>
    public readonly struct MemberPresenceFacts
    {
        public MemberPresenceFacts(bool inMainParty, bool leadsOwnParty, string settlementName)
        {
            InMainParty = inMainParty;
            LeadsOwnParty = leadsOwnParty;
            SettlementName = settlementName;
        }

        /// <summary>The member rides in the player's own party right now.</summary>
        public bool InMainParty { get; }

        /// <summary>The member leads their own separate party (an independent lord, off doing their own business).</summary>
        public bool LeadsOwnParty { get; }

        /// <summary>
        ///   The settlement the member is currently staying in, when they belong to no party of their own
        ///   (a companion or family member without a command, sitting in a town or castle). Null or blank when
        ///   not applicable.
        /// </summary>
        public string SettlementName { get; }
    }

    /// <summary>
    ///   Renders a seated council member's true whereabouts as a short phrase the prompt states plainly, so the
    ///   model is told the truth instead of inferring (or inventing) that a seated member travels with the
    ///   player.
    /// </summary>
    public static class MemberPresenceDescriber
    {
        /// <summary>
        ///   The phrase for <paramref name="facts" />, in priority order: riding in the player's own party wins
        ///   over anything else; failing that, a member who leads their own company is doing so wherever that
        ///   company is, regardless of a settlement they might momentarily be sitting in; failing that, a named
        ///   settlement means they are staying there; otherwise they are simply elsewhere, not travelling with
        ///   the player.
        /// </summary>
        public static string Describe(MemberPresenceFacts facts)
        {
            if (facts.InMainParty) return "at your side in your party";
            if (facts.LeadsOwnParty) return "leading their own company";

            return string.IsNullOrWhiteSpace(facts.SettlementName)
                ? "abroad, not travelling with you"
                : $"keeping to {facts.SettlementName}";
        }
    }
}
