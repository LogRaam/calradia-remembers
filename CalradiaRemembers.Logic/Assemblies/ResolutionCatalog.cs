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

        /// <summary>
        ///   Send a companion already riding in the player's own party out on one of the EXISTING companion-mission
        ///   errands (CompanionMissionBehavior.Dispatch): they leave and return later on their own, the destination
        ///   is chosen by the mission system, never named by the model. Reuses the TargetInPlayerParty fact
        ///   <see cref="AssignPartyRole" /> already needs (a dispatchable companion travels with you, and
        ///   CompanionMissionBehavior detaches an away companion into their OWN clan party, so PartyBelongedTo
        ///   already excludes them in the normal case); the narrow reconciliation-window edge case (freshly
        ///   rescued but not yet ticked off the mission list) is caught by the lift's own "already away" recheck,
        ///   not the offering, mirroring how every other executor re-validates at the lift rather than trusting
        ///   proposal-time facts alone.
        /// </summary>
        DispatchOnMission,

        /// <summary>Grant a companion a purse from the player's coffers.</summary>
        GrantStipend,

        /// <summary>Grant a fief to a clan lord (ClanLords council; executor arrives with that variant).</summary>
        GrantFief,

        /// <summary>
        ///   A seated clan companion who is NOT currently riding in the player's own party (seated thanks to the
        ///   roster's own availability rule, e.g. while governing a town) pledges to rejoin it. Bug d: the only
        ///   "join" path today is the 1:1 join_party action, which hires a free Wanderer only, so an EXISTING
        ///   clan companion who agrees at the table to come back had no executor at all.
        /// </summary>
        RejoinParty,

        /// <summary>
        ///   The WarCouncil's first motion, and the first FACTION-CENTRIC kind in the catalogue: declares war on
        ///   a faction the player is not already at war with. Unlike every kind above, the pledge does not land
        ///   on a seated member's own fate (a governorship, a role, a purse); it is the table's decision about
        ///   two FACTIONS. See <see cref="IsFactionCentric" />, which the mod's lift consults to skip the
        ///   ordinary seated-member re-validation this kind has no member actor for.
        /// </summary>
        DeclareWar
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
        MissionAvailable = 16,

        /// <summary>
        ///   The target is a seated clan companion currently OUTSIDE the player's own party: the symmetric
        ///   inverse of <see cref="TargetInPlayerParty" />, needed for a companion to pledge a rejoin rather than
        ///   a role held only while already travelling with the player.
        /// </summary>
        TargetOutOfPlayerParty = 32,

        /// <summary>The player leads a faction (kingdom ruler, or an independent clan that IS its own faction) that could declare a new war.</summary>
        PlayerCanDeclareWar = 64
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
                                    bool missionAvailable,
                                    bool targetOutOfPlayerParty = false,
                                    bool playerCanDeclareWar = false)
        {
            TargetInPlayerParty = targetInPlayerParty;
            TargetAvailable = targetAvailable;
            PlayerHasVacantFief = playerHasVacantFief;
            PlayerCanAffordStipend = playerCanAffordStipend;
            MissionAvailable = missionAvailable;
            TargetOutOfPlayerParty = targetOutOfPlayerParty;
            PlayerCanDeclareWar = playerCanDeclareWar;
        }

        public bool TargetInPlayerParty { get; }
        public bool TargetAvailable { get; }
        public bool PlayerHasVacantFief { get; }
        public bool PlayerCanAffordStipend { get; }
        public bool MissionAvailable { get; }
        public bool TargetOutOfPlayerParty { get; }

        /// <summary>The player leads a faction that could declare a new war (declare_war's own fact, see ResolutionRequirement.PlayerCanDeclareWar).</summary>
        public bool PlayerCanDeclareWar { get; }
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
                "Send on a mission", ResolutionRequirement.TargetInPlayerParty),

            new ResolutionDefinition(ResolutionKind.GrantStipend, CouncilScope.Companions,
                "Grant a stipend", ResolutionRequirement.PlayerCanAffordStipend),

            new ResolutionDefinition(ResolutionKind.GrantFief, CouncilScope.ClanLords,
                "Grant a fief", ResolutionRequirement.TargetAvailable | ResolutionRequirement.PlayerHasVacantFief),

            new ResolutionDefinition(ResolutionKind.RejoinParty, CouncilScope.Companions,
                "Rejoin the party", ResolutionRequirement.TargetOutOfPlayerParty),

            new ResolutionDefinition(ResolutionKind.DeclareWar, CouncilScope.WarCouncil,
                "Declare war", ResolutionRequirement.PlayerCanDeclareWar)
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
                case ResolutionKind.AssignPartyRole:   return "assign_party_role";
                case ResolutionKind.AppointGovernor:   return "appoint_governor";
                case ResolutionKind.DispatchOnMission: return "dispatch_mission";
                case ResolutionKind.GrantStipend:      return "grant_stipend";
                case ResolutionKind.GrantFief:         return "grant_fief";
                case ResolutionKind.RejoinParty:       return "rejoin_party";
                case ResolutionKind.DeclareWar:        return "declare_war";
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
        ///   True when a passed motion of this kind has a LIVE lift executor. Council audit M4: <see cref="ResolutionKind.Quest" />
        ///   (a member set a task in the world), the first governance executor <see cref="ResolutionKind.AppointGovernor" />
        ///   (CouncilLift.SettleAppointGovernor), and, mirroring that same wiring, <see cref="ResolutionKind.AssignPartyRole" />
        ///   (CouncilLift.SettleAssignPartyRole) are carried out; closing bug d, <see cref="ResolutionKind.RejoinParty" />
        ///   (CouncilLift.SettleRejoinParty) brings a seated companion who is out of the player's party back into
        ///   it; the FOURTH executor, <see cref="ResolutionKind.DispatchOnMission" /> (CouncilLift.SettleDispatchMission)
        ///   sends a seated companion out on one of the existing companion-mission errands; and the FIFTH executor,
        ///   <see cref="ResolutionKind.GrantStipend" /> (CouncilLift.SettleGrantStipend), and the FIRST that touches
        ///   persisted save state, funds a recurring 30-day purse for a seated companion out of a mod-tracked
        ///   escrow (StipendPolicy, CouncilStipendBehavior). The remaining scoped menu motions are recorded but not
        ///   yet executed, so they arrive with their variant (Q5). The runtime teaches and lifts only executable
        ///   kinds, so the table never promises a deed it cannot keep.
        /// </summary>
        public static bool IsExecutable(ResolutionKind kind)
            => kind == ResolutionKind.Quest || kind == ResolutionKind.AppointGovernor
            || kind == ResolutionKind.AssignPartyRole || kind == ResolutionKind.RejoinParty
            || kind == ResolutionKind.DispatchOnMission || kind == ResolutionKind.GrantStipend
            || kind == ResolutionKind.DeclareWar;

        /// <summary>
        ///   True for a FACTION-CENTRIC kind (today, only <see cref="ResolutionKind.DeclareWar" />): its pledge
        ///   concerns two FACTIONS, not a seated member's own fate, so it has no member actor for the ordinary
        ///   seated-member re-validation every other kind gets at the lift (CouncilLift.Settle resolves
        ///   entry.TargetHeroId and re-checks CouncilRosterResolver.EvaluateNow before dispatch). The mod's lift
        ///   consults this to skip that gate specifically for a faction-centric kind and dispatch straight to its
        ///   own executor, which re-validates the FACTIONS instead (bridge-is-law, just on a different subject).
        /// </summary>
        public static bool IsFactionCentric(ResolutionKind kind)
            => kind == ResolutionKind.DeclareWar;

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
