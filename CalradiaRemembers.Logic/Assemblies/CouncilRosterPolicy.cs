// Code written by Gabriel Mailhot, 20/07/2026.
// Pure decision: is a given hero seated at a council of the given scope. Deterministic so the seating rules are
// documented by tests. Order matters: availability gates first (a prisoner cannot sit even if he is your
// brother), then membership in the chosen roster. There is no distance test: convening your own house is
// abstract (your kin answer the ban wherever they are), which is what keeps this first slice free of the
// fragile party-travel layer reserved for the Political mode.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Reads a <see cref="CouncilMembershipFacts" /> for one hero into a seated/excluded verdict.</summary>
    public static class CouncilRosterPolicy
    {
        public static CouncilEligibility Evaluate(CouncilScope scope, CouncilMembershipFacts f)
        {
            // Availability first: a summons to your own house still cannot seat the dead, the imprisoned, or a
            // hero pinned in a battle. These bar the seat regardless of which roster is being gathered.
            if (!f.IsAlive) return new CouncilEligibility(false, CouncilExclusionReason.Dead);
            if (f.IsPrisoner) return new CouncilEligibility(false, CouncilExclusionReason.Imprisoned);
            if (f.IsInBattle) return new CouncilEligibility(false, CouncilExclusionReason.InBattle);

            // Membership: the chosen scope decides which flag seats the hero. The flags overlap by design (a
            // sibling-lord is both Family and ClanLord), so a hero can be eligible for one council and not another.
            bool belongs;
            switch (scope)
            {
                case CouncilScope.Companions:
                    belongs = f.IsCompanion;
                    break;
                case CouncilScope.Family:
                    belongs = f.IsImmediateFamily;
                    break;
                case CouncilScope.ClanLords:
                    belongs = f.IsClanLord;
                    break;
                default:
                    belongs = false;
                    break;
            }

            return belongs
                ? new CouncilEligibility(true, CouncilExclusionReason.None)
                : new CouncilEligibility(false, CouncilExclusionReason.NotOfThisCouncil);
        }
    }
}
