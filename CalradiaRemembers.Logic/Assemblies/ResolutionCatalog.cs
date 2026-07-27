// Code written by Gabriel Mailhot, 20/07/2026.
// ASSEMBLY pillar, Council module: the catalogue of motions a council may raise. Each definition is pure data
// (kind, which council raises it, a menu label, and the WORLD conditions that must hold to offer it). The
// EXECUTOR that applies a passed motion lives in the mod (it touches the engine: assign a role, grant a fief);
// this catalogue only says what exists and when it may be put on the table, so those rules stay unit-tested.
// The catalogue starts with the Companions set (the first variant wired) plus one ClanLords motion, so the
// by-scope query is proven to discriminate; the rest of each scope arrives with its variant.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Every motion the catalogue knows. The specific detail (which role, which fief) is chosen at
    /// proposal time and recorded on the ledger entry; the kind selects the executor.</summary>
    public enum ResolutionKind
    {
        /// <summary>Set a member a task in the world (ride to a town and clear the bandits raiding it). The one
        /// kind with a live lift executor today, and UNIVERSAL to every council, so it is not a scoped menu
        /// motion and carries no <see cref="ResolutionDefinition" />; the scoped kinds below arrive with their
        /// variant (Q5, "extend then migrate").</summary>
        Quest,

        /// <summary>Give a companion a party role (scout, surgeon, engineer, quartermaster).</summary>
        AssignPartyRole,

        /// <summary>Send a companion to govern one of the player's fiefs.</summary>
        AppointGovernor,

        /// <summary>Send a companion out on a companion mission.</summary>
        DispatchOnMission,

        /// <summary>Grant a companion a purse from the player's coffers.</summary>
        GrantStipend,

        /// <summary>Grant a fief to a clan lord (ClanLords council; executor arrives with that variant).</summary>
        GrantFief
    }

    /// <summary>The world conditions a motion needs before it may be offered. Combined per motion.</summary>
    [System.Flags]
    public enum ResolutionRequirement
    {
        None = 0,

        /// <summary>The target travels in the player's own party.</summary>
        TargetInPlayerParty = 1,

        /// <summary>The target is free to be reassigned (not already governing, on a mission, or held).</summary>
        TargetAvailable = 2,

        /// <summary>The player owns a fief with no governor / that can be granted.</summary>
        PlayerHasVacantFief = 4,

        /// <summary>The player has enough gold to fund a stipend.</summary>
        PlayerCanAffordStipend = 8,

        /// <summary>At least one companion mission is available to hand out.</summary>
        MissionAvailable = 16
    }

    /// <summary>One catalogue entry: what the motion is, which council raises it, its label, and its conditions.</summary>
    public readonly struct ResolutionDefinition
    {
        public ResolutionDefinition(ResolutionKind kind, CouncilScope scope, string label, ResolutionRequirement requires)
        {
            Kind = kind;
            Scope = scope;
            Label = label;
            Requires = requires;
        }

        public ResolutionKind Kind { get; }
        public CouncilScope Scope { get; }
        public string Label { get; }
        public ResolutionRequirement Requires { get; }
    }

    /// <summary>
    ///   The flattened world state a council reads to decide which motions it may offer. The mod fills this from
    ///   the engine once per council; the eligibility policy reads it against each definition's requirements.
    /// </summary>
    public readonly struct ResolutionWorldFacts
    {
        public ResolutionWorldFacts(bool targetInPlayerParty,
                                    bool targetAvailable,
                                    bool playerHasVacantFief,
                                    bool playerCanAffordStipend,
                                    bool missionAvailable)
        {
            TargetInPlayerParty = targetInPlayerParty;
            TargetAvailable = targetAvailable;
            PlayerHasVacantFief = playerHasVacantFief;
            PlayerCanAffordStipend = playerCanAffordStipend;
            MissionAvailable = missionAvailable;
        }

        public bool TargetInPlayerParty { get; }
        public bool TargetAvailable { get; }
        public bool PlayerHasVacantFief { get; }
        public bool PlayerCanAffordStipend { get; }
        public bool MissionAvailable { get; }
    }

    /// <summary>The static registry of resolution definitions and the queries over it.</summary>
    public static class ResolutionCatalog
    {
        private static readonly ResolutionDefinition[] All =
        {
            new ResolutionDefinition(ResolutionKind.AssignPartyRole, CouncilScope.Companions,
                "Assign a party role", ResolutionRequirement.TargetInPlayerParty),

            new ResolutionDefinition(ResolutionKind.AppointGovernor, CouncilScope.Companions,
                "Appoint as governor", ResolutionRequirement.TargetAvailable | ResolutionRequirement.PlayerHasVacantFief),

            new ResolutionDefinition(ResolutionKind.DispatchOnMission, CouncilScope.Companions,
                "Send on a mission", ResolutionRequirement.TargetAvailable | ResolutionRequirement.MissionAvailable),

            new ResolutionDefinition(ResolutionKind.GrantStipend, CouncilScope.Companions,
                "Grant a stipend", ResolutionRequirement.PlayerCanAffordStipend),

            new ResolutionDefinition(ResolutionKind.GrantFief, CouncilScope.ClanLords,
                "Grant a fief", ResolutionRequirement.TargetAvailable | ResolutionRequirement.PlayerHasVacantFief)
        };

        /// <summary>The definition for one scoped menu kind. Every menu kind (all but <see cref="ResolutionKind.Quest" />) has exactly one entry.</summary>
        public static ResolutionDefinition Get(ResolutionKind kind) => All.First(d => d.Kind == kind);

        /// <summary>Every motion a council of the given scope may raise, before world eligibility is applied.</summary>
        public static IEnumerable<ResolutionDefinition> ForScope(CouncilScope scope)
            => All.Where(d => d.Scope == scope);

        /// <summary>
        ///   The ledger/prompt string key for a kind, and back (<see cref="TryParseKey" />). Council audit M4:
        ///   the catalogue spoke in the <see cref="ResolutionKind" /> enum while the ledger, prompt, and lift
        ///   spoke in free strings ("quest"), unlinked, so only the string "quest" ever did anything and every
        ///   catalogue kind was dead. This is the one map both sides read, so they can never drift.
        /// </summary>
        public static string KeyOf(ResolutionKind kind)
        {
            switch (kind)
            {
                case ResolutionKind.Quest:             return "quest";
                case ResolutionKind.AssignPartyRole:   return "assign_role";
                case ResolutionKind.AppointGovernor:   return "appoint_governor";
                case ResolutionKind.DispatchOnMission: return "dispatch_mission";
                case ResolutionKind.GrantStipend:      return "grant_stipend";
                case ResolutionKind.GrantFief:         return "grant_fief";
                default:                               return kind.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Resolves a ledger/prompt kind string back to its <see cref="ResolutionKind" />. False (and default) for null/blank/unknown.</summary>
        public static bool TryParseKey(string key, out ResolutionKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(key)) return false;

            string trimmed = key.Trim();
            foreach (ResolutionKind candidate in (ResolutionKind[]) Enum.GetValues(typeof(ResolutionKind)))
                if (string.Equals(KeyOf(candidate), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    kind = candidate;

                    return true;
                }

            return false;
        }

        /// <summary>
        ///   True when a passed motion of this kind has a LIVE lift executor. Council audit M4: today only
        ///   <see cref="ResolutionKind.Quest" /> (a member set a task in the world) is carried out; the scoped
        ///   menu motions are recorded but not yet executed, so they arrive with their variant (Q5). The runtime
        ///   teaches and lifts only executable kinds, so the table never promises a deed it cannot keep.
        /// </summary>
        public static bool IsExecutable(ResolutionKind kind) => kind == ResolutionKind.Quest;

        /// <summary>
        ///   The string keys of the kinds a council of <paramref name="scope" /> may currently ACT on: the
        ///   universal <see cref="ResolutionKind.Quest" /> plus any scoped menu kind whose executor has landed
        ///   (none yet). This is the set the prompt offers, so every scope, Family included, has at least one
        ///   deed the lift can keep, and a scoped executor appears here automatically the day it is wired.
        /// </summary>
        public static IEnumerable<string> ExecutableKindKeysForScope(CouncilScope scope)
        {
            yield return KeyOf(ResolutionKind.Quest);

            foreach (ResolutionDefinition d in ForScope(scope))
                if (IsExecutable(d.Kind))
                    yield return KeyOf(d.Kind);
        }
    }
}
