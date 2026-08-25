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
        /// <summary>
        ///   The youngest age a hero may take a council seat, in ANY scope (family, clan lords, and the war
        ///   council's own-house fallback alike). A teen (13+) may sit and speak true to their years; a younger
        ///   child is present in the household but is never a candidate for the table. Kept here, pure, so the one
        ///   age rule is shared by every roster and unit-tested rather than re-derived (or forgotten) per scope,
        ///   which is exactly how toddlers were seating at a war / clan-lords council.
        /// </summary>
        public const int MinSeatAge = 13;

        /// <summary>True when a hero is old enough to take a council seat (see <see cref="MinSeatAge" />).</summary>
        public static bool IsOldEnoughToSit(int age) => age >= MinSeatAge;

        public static CouncilEligibility Evaluate(CouncilScope scope, CouncilMembershipFacts f)
        {
            // Availability first: a summons to your own house still cannot seat the dead, the imprisoned, or a
            // hero pinned in a battle. These bar the seat regardless of which roster is being gathered.
            if (!f.IsAlive) return new CouncilEligibility(false, CouncilExclusionReason.Dead);
            if (f.IsPrisoner) return new CouncilEligibility(false, CouncilExclusionReason.Imprisoned);

            // A member marching with an army or away on a companion errand cannot physically sit at the table
            // either, for the same reason as the battle gate: seating them anyway would let them agree to deeds
            // they cannot perform, recreating the "absent member acts as if present" bug under another form.
            // These three also bar the seat regardless of scope for every OTHER council, so they sit here rather
            // than in the membership switch below. A Parley's counterpart is deliberately excluded (author's
            // decision): they are the LEADER of an enemy faction the player is at war with, not one of YOUR
            // people subject to army/mission gates, an enemy leader is routinely leading their own army, and
            // that is exactly why a parley is being sought. Only alive + not prisoner gate them.
            if (scope != CouncilScope.Parley)
            {
                if (f.IsInBattle) return new CouncilEligibility(false, CouncilExclusionReason.InBattle);
                if (f.IsInArmy) return new CouncilEligibility(false, CouncilExclusionReason.InArmy);
                if (f.IsOnMission) return new CouncilEligibility(false, CouncilExclusionReason.OnMission);
            }

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
                case CouncilScope.WarCouncil:
                    belongs = f.IsAlliedLeader;
                    break;
                case CouncilScope.Parley:
                    belongs = f.IsParleyCounterpart;
                    break;
                case CouncilScope.Party:
                    belongs = f.IsPartyMember;
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
