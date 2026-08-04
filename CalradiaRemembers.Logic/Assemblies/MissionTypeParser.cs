// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: dispatch_mission grounds its resolution the same way assign_party_role grounds
// a party role, as a single string in AssemblyResolutionEntry.TargetHint (no new save field). The lift must
// resolve that string to a real CompanionMissionBehavior errand type before it can call the existing Dispatch
// API, and it must do so from a STRUCTURED value never prose-parsed at the lift: an unrecognised mission type
// has to fail cleanly (None), never guess an errand the model did not actually name. CouncilMissionType mirrors
// CalradiaRemembers.Logic.CompanionMissionType member-for-member (this Logic project has no engine-facing
// CompanionMissionBehavior to reference directly, so CouncilLift maps a None-guarded value across at the lift).

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The companion-mission errands a council may dispatch a seated companion on.</summary>
    public enum CouncilMissionType
    {
        /// <summary>Unrecognised, blank, or malformed TargetHint. The lift treats this as a guard failure, never a guess.</summary>
        None,

        GatherNews,
        Spy,
        Steal,
        Barter,
        Envoy
    }

    /// <summary>Resolves a council resolution's TargetHint string to the companion-mission errand it names.</summary>
    public static class MissionTypeParser
    {
        /// <summary>
        ///   Case-insensitive, tolerant of surrounding whitespace. Returns <see cref="CouncilMissionType.None" /> for
        ///   null, blank, or unrecognised text, so an unresolvable mission type fails the lift's guard instead of
        ///   being silently dispatched as a guess.
        /// </summary>
        public static CouncilMissionType Parse(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return CouncilMissionType.None;

            switch (type.Trim().ToLowerInvariant())
            {
                case "gathernews":   return CouncilMissionType.GatherNews;
                case "spy":          return CouncilMissionType.Spy;
                case "steal":        return CouncilMissionType.Steal;
                case "barter":       return CouncilMissionType.Barter;
                case "envoy":        return CouncilMissionType.Envoy;
                default:             return CouncilMissionType.None;
            }
        }
    }
}
