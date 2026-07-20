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

        // The single place the requirement flags map to world facts. Each required flag must find its fact true;
        // an unrequired flag is ignored. A requirement of None is trivially met (an unconditional motion).
        private static bool Meets(ResolutionWorldFacts f, ResolutionRequirement required)
        {
            if (required.HasFlag(ResolutionRequirement.TargetInPlayerParty) && !f.TargetInPlayerParty) return false;
            if (required.HasFlag(ResolutionRequirement.TargetAvailable) && !f.TargetAvailable) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerHasVacantFief) && !f.PlayerHasVacantFief) return false;
            if (required.HasFlag(ResolutionRequirement.PlayerCanAffordStipend) && !f.PlayerCanAffordStipend) return false;
            if (required.HasFlag(ResolutionRequirement.MissionAvailable) && !f.MissionAvailable) return false;

            return true;
        }
    }
}
