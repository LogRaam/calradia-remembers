// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: assign_party_role grounds its resolution the same way appoint_governor
// grounds a fief name, as a single string in AssemblyResolutionEntry.TargetHint (no new save field). The lift
// must resolve that string to a real party role before it can call the engine's role-assignment API, and it
// must do so from a STRUCTURED value never prose-parsed at the lift: an unrecognised role has to fail cleanly
// (None), never guess a role the model did not actually name.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The four party roles a council may assign a seated companion to.</summary>
    public enum CouncilPartyRole
    {
        /// <summary>Unrecognised, blank, or malformed TargetHint. The lift treats this as a guard failure, never a guess.</summary>
        None,

        Scout,
        Engineer,
        Quartermaster,
        Surgeon
    }

    /// <summary>Resolves a council resolution's TargetHint string to the party role it names.</summary>
    public static class PartyRoleParser
    {
        /// <summary>
        ///   Case-insensitive, tolerant of surrounding whitespace. Returns <see cref="CouncilPartyRole.None" /> for
        ///   null, blank, or unrecognised text, so an unresolvable role fails the lift's guard instead of being
        ///   silently assigned as a guess.
        /// </summary>
        public static CouncilPartyRole Parse(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return CouncilPartyRole.None;

            switch (role.Trim().ToLowerInvariant())
            {
                case "scout":         return CouncilPartyRole.Scout;
                case "engineer":      return CouncilPartyRole.Engineer;
                case "quartermaster": return CouncilPartyRole.Quartermaster;
                case "surgeon":       return CouncilPartyRole.Surgeon;
                default:              return CouncilPartyRole.None;
            }
        }
    }
}
