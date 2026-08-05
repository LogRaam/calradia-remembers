// Code written by Gabriel Mailhot, 20/07/2026.
// Pure eligibility: which catalogue motions a council may actually put on the table right now, given the world.
// A motion is offerable only when EVERY world condition it requires holds (an appointment needs both an
// available companion AND a vacant fief). Kept out of the mod so the requirement-to-fact mapping is documented
// by tests: a motion that slips through with an unmet condition would let the council pass a resolution the
// bridge then cannot apply, which is exactly the empty-promise class the whole assembly design guards against.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Reads a <see cref="ResolutionWorldFacts" /> against catalogue requirements.</summary>
    public static class ResolutionEligibilityPolicy
    {
        /// <summary>True when every condition the motion requires is satisfied by the current world facts.</summary>
        public static bool CanOffer(ResolutionDefinition definition, ResolutionWorldFacts facts)
            => Meets(facts, definition.Requires);

        /// <summary>The motions a council of this scope may offer right now: catalogue-for-scope, then eligible.</summary>
        public static IEnumerable<ResolutionDefinition> OffersFor(CouncilScope scope, ResolutionWorldFacts facts)
            => ResolutionCatalog.ForScope(scope).Where(d => CanOffer(d, facts));

        /// <summary>
        ///   The string keys of the kinds a council of <paramref name="scope" /> may currently TEACH the LLM:
        ///   <see cref="ResolutionCatalog.ExecutableKindKeysForScope" /> (the LIVE-executor set) narrowed to the
        ///   ones <paramref name="facts" /> actually satisfies. Quest carries no <see cref="ResolutionDefinition" />
        ///   (it is universal, not a scoped menu motion, see <see cref="ResolutionKind.Quest" />) so it is always
        ///   kept; every other executable kind must pass <see cref="CanOffer" /> like any catalogue motion. This
        ///   is the one place the prompt-offering feature reads, so a kind the lift cannot yet honour (an unmet
        ///   requirement) is withheld from the teaching instead of promising a deed that fails at the lift.
        /// </summary>
        public static IEnumerable<string> OfferedExecutableKeysForScope(CouncilScope scope, ResolutionWorldFacts facts)
        {
            foreach (string key in ResolutionCatalog.ExecutableKindKeysForScope(scope))
            {
                if (!ResolutionCatalog.TryParseKey(key, out ResolutionKind kind)) continue;

                if (kind == ResolutionKind.Quest || CanOffer(ResolutionCatalog.Get(kind), facts))
                    yield return key;
            }
        }

        // The single place the requirement flags map to world facts. Each required flag must find its fact true;
        // an unrequired flag is ignored. A requirement of None is trivially met (an unconditional motion).
        private static bool Meets(ResolutionWorldFacts f, ResolutionRequirement required)
        {
            if (required.HasFlag(ResolutionRequirement.TargetInPlayerParty) && !f.TargetInPlayerParty) return false;
            if (required.HasFlag(ResolutionRequirement.TargetAvailable) && !f.TargetAvailable) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerHasVacantFief) && !f.PlayerHasVacantFief) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerCanAffordStipend) && !f.PlayerCanAffordStipend) return false;
            if (required.HasFlag(ResolutionRequirement.MissionAvailable) && !f.MissionAvailable) return false;
            if (required.HasFlag(ResolutionRequirement.TargetOutOfPlayerParty) && !f.TargetOutOfPlayerParty) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerCanDeclareWar) && !f.PlayerCanDeclareWar) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerCanMakePeace) && !f.PlayerCanMakePeace) return false;
            if (required.HasFlag(ResolutionRequirement.MemberHasGold) && !f.MemberHasGold) return false;
            if (required.HasFlag(ResolutionRequirement.MemberCanGrantInfluence) && !f.MemberCanGrantInfluence) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerIsKingdomLeader) && !f.PlayerIsKingdomLeader) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerHasGiveableFief) && !f.PlayerHasGiveableFief) return false;
            if (required.HasFlag(ResolutionRequirement.SeatedVassalOwnsFief) && !f.SeatedVassalOwnsFief) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedCompanionExpellable) && !f.AnySeatedCompanionExpellable) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedMemberCanPledgeAgainst) && !f.AnySeatedMemberCanPledgeAgainst) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedAllyHasMarriageableKin) && !f.AnySeatedAllyHasMarriageableKin) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedTributePayerAvailable) && !f.AnySeatedTributePayerAvailable) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedPrisonerAvailableToRelease) && !f.AnySeatedPrisonerAvailableToRelease) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedMemberCanLendTroops) && !f.AnySeatedMemberCanLendTroops) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedMemberCanSwearOath) && !f.AnySeatedMemberCanSwearOath) return false;
            if (required.HasFlag(ResolutionRequirement.AnySeatedHostageEligibleRelativeAvailable) && !f.AnySeatedHostageEligibleRelativeAvailable) return false;

            return true;
        }
    }
}
