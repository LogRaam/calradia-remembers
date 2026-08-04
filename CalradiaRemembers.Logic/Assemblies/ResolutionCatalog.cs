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

        /// <summary>
        ///   Grant a crown fief to a seated WarCouncil vassal (the OTHER clan's own leader, never the player's
        ///   own house, see COUNCIL_ACTIONS.md's Kimi review under grant_fief: a fief belongs to the CLAN, so
        ///   "granting" one within the player's own clan is mechanically vacant). Re-scoped from an earlier
        ///   ClanLords-only scaffold to WarCouncil, its only mechanically meaningful scope, the day the executor
        ///   landed (CouncilLift.SettleGrantFief).
        /// </summary>
        GrantFief,

        /// <summary>
        ///   Strips a crown fief BACK from a seated WarCouncil vassal, sovereign-only (COUNCIL_ACTIONS.md,
        ///   Gabriel: "uniquement si on est le chef du clan", corrected by the Kimi review to KINGDOM sovereign,
        ///   since a fief the player's own clan already holds cannot meaningfully be revoked from a clanmate).
        ///   One of the gravest punishments the table can hand down: plants a maximum
        ///   <see cref="CalradiaRemembers.Logic.Grudges.GrudgeSource.FiefRevoked" /> grudge on the spoliated lord
        ///   and a real relation hit (CouncilLift.SettleRevokeFief).
        /// </summary>
        RevokeFief,

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
        DeclareWar,

        /// <summary>
        ///   The Parley's own motion, and DeclareWar's symmetric counterpart: ends the war between the player's
        ///   faction and the enemy faction the parley was convened against. FACTION-CENTRIC like DeclareWar (see
        ///   <see cref="IsFactionCentric" />): its subject is two FACTIONS, not a seated member's own fate, even
        ///   though a parley seats only one counterpart (the enemy leader).
        /// </summary>
        MakePeace,

        /// <summary>
        ///   Partie 1 (bringing the existing 1:1 resource verbs to the council): a seated LORD pledges to give
        ///   the PLAYER gold from their own purse. Member-pledge shaped like GrantStipend (not faction-centric,
        ///   see <see cref="IsFactionCentric" />), but the money moves the OPPOSITE direction: a companion's
        ///   stipend is funded FROM the player, this is funded FROM the giver TO the player. Scoped to ClanLords
        ///   and WarCouncil only (two catalogue entries, see <see cref="ResolutionCatalog.All" />): a seated
        ///   member plausibly has coin of their own to give in those councils, never Companions or Family.
        /// </summary>
        GiveGold,

        /// <summary>
        ///   Partie 1 (bringing the existing 1:1 resource verbs to the council): a seated ALLIED CLAN LEADER
        ///   pledges their own clan's political influence to the player's clan (the 1:1 give_influence verb,
        ///   see CalradiaRemembers.Verbs.GiveInfluenceVerb / CourtActionResolver.TryGiveInfluence). Unlike
        ///   GiveGold, this is scoped to WarCouncil ONLY (COUNCIL_ACTIONS.md, Kimi review): influence is a
        ///   CLAN-level resource, meaningless within the player's own clan (the 1:1 verb already excludes
        ///   giver == Clan.PlayerClan) and meaningless outside a kingdom (a clan holding no seat at any
        ///   court has no influence worth pledging), so ClanLords/Companions/Family, where a seated member
        ///   IS the player's own clan or a hired hand, never carry this motion. The amount is NEVER chosen by
        ///   the LLM: it is computed at the lift by the reused CourtActionPolicy.InfluenceGrant, so this
        ///   kind's ResolutionDefinition carries no amount-shaped requirement, only the fact that some
        ///   seated ally's clan could grant it at all.
        /// </summary>
        GiveInfluence
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
        PlayerCanDeclareWar = 64,

        /// <summary>The player leads their own faction AND that faction is currently at war with the parley's target faction.</summary>
        PlayerCanMakePeace = 128,

        /// <summary>At least one seated member currently holds gold they could pledge to the player (give_gold's own fact).</summary>
        MemberHasGold = 256,

        /// <summary>
        ///   At least one seated allied clan leader's OWN clan could currently grant influence to the player's
        ///   clan under the reused 1:1 give_influence gates (both clans in a kingdom, giver's clan holding
        ///   comfortably more influence than the computed grant, and the relation floor met). give_influence's
        ///   own fact.
        /// </summary>
        MemberCanGrantInfluence = 512,

        /// <summary>
        ///   The player actually RULES a kingdom (Kingdom.Leader == the player), not merely a member of one. Both
        ///   grant_fief and revoke_fief's own gate (COUNCIL_ACTIONS.md's Kimi review): a fief is clan-owned, so
        ///   only the true sovereign can move one between the crown and a vassal at all; an independent clan (no
        ///   kingdom) or a vassal clan among peers has no crown fief to give or take.
        /// </summary>
        PlayerIsKingdomLeader = 1024,

        /// <summary>The player's own clan currently owns at least one town or castle it could give away as a crown fief. grant_fief's own fact.</summary>
        PlayerHasGiveableFief = 2048,

        /// <summary>At least one seated WarCouncil vassal's own clan currently owns a town or castle that could be stripped back to the crown. revoke_fief's own fact.</summary>
        SeatedVassalOwnsFief = 4096
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
                                    bool playerCanDeclareWar = false,
                                    bool playerCanMakePeace = false,
                                    bool memberHasGold = false,
                                    bool memberCanGrantInfluence = false,
                                    bool playerIsKingdomLeader = false,
                                    bool playerHasGiveableFief = false,
                                    bool seatedVassalOwnsFief = false)
        {
            TargetInPlayerParty = targetInPlayerParty;
            TargetAvailable = targetAvailable;
            PlayerHasVacantFief = playerHasVacantFief;
            PlayerCanAffordStipend = playerCanAffordStipend;
            MissionAvailable = missionAvailable;
            TargetOutOfPlayerParty = targetOutOfPlayerParty;
            PlayerCanDeclareWar = playerCanDeclareWar;
            PlayerCanMakePeace = playerCanMakePeace;
            MemberHasGold = memberHasGold;
            MemberCanGrantInfluence = memberCanGrantInfluence;
            PlayerIsKingdomLeader = playerIsKingdomLeader;
            PlayerHasGiveableFief = playerHasGiveableFief;
            SeatedVassalOwnsFief = seatedVassalOwnsFief;
        }

        public bool TargetInPlayerParty { get; }
        public bool TargetAvailable { get; }
        public bool PlayerHasVacantFief { get; }
        public bool PlayerCanAffordStipend { get; }
        public bool MissionAvailable { get; }
        public bool TargetOutOfPlayerParty { get; }

        /// <summary>The player leads a faction that could declare a new war (declare_war's own fact, see ResolutionRequirement.PlayerCanDeclareWar).</summary>
        public bool PlayerCanDeclareWar { get; }

        /// <summary>The player leads their faction and it is at war with the parley's target faction (make_peace's own fact, see ResolutionRequirement.PlayerCanMakePeace).</summary>
        public bool PlayerCanMakePeace { get; }

        /// <summary>At least one seated member holds gold they could pledge to the player (give_gold's own fact, see ResolutionRequirement.MemberHasGold).</summary>
        public bool MemberHasGold { get; }

        /// <summary>At least one seated allied clan leader's own clan could grant influence to the player's clan (give_influence's own fact, see ResolutionRequirement.MemberCanGrantInfluence).</summary>
        public bool MemberCanGrantInfluence { get; }

        /// <summary>The player actually rules a kingdom (Kingdom.Leader == the player). Both grant_fief's and revoke_fief's own fact, see ResolutionRequirement.PlayerIsKingdomLeader.</summary>
        public bool PlayerIsKingdomLeader { get; }

        /// <summary>The player's own clan currently owns a town or castle it could give away. grant_fief's own fact, see ResolutionRequirement.PlayerHasGiveableFief.</summary>
        public bool PlayerHasGiveableFief { get; }

        /// <summary>At least one seated WarCouncil vassal's own clan currently owns a town or castle. revoke_fief's own fact, see ResolutionRequirement.SeatedVassalOwnsFief.</summary>
        public bool SeatedVassalOwnsFief { get; }
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

            new ResolutionDefinition(ResolutionKind.RejoinParty, CouncilScope.Companions,
                "Rejoin the party", ResolutionRequirement.TargetOutOfPlayerParty),

            new ResolutionDefinition(ResolutionKind.DeclareWar, CouncilScope.WarCouncil,
                "Declare war", ResolutionRequirement.PlayerCanDeclareWar),

            new ResolutionDefinition(ResolutionKind.MakePeace, CouncilScope.Parley,
                "Make peace", ResolutionRequirement.PlayerCanMakePeace),

            // Partie 1 (bringing the existing 1:1 resource verbs to the council): TWO entries for the same kind,
            // one per scope where a seated member plausibly has coin of their own to give (never Companions or
            // Family, where a member is either a hired hand or dynastic kin, not a moneyed lord). ForScope's own
            // Where-filter (below) discriminates correctly across duplicate-kind entries; Get(kind), which does
            // not filter by scope, returns whichever entry appears first here, but since both carry the IDENTICAL
            // requirement (MemberHasGold) this is harmless.
            new ResolutionDefinition(ResolutionKind.GiveGold, CouncilScope.ClanLords,
                "Pledge gold to you", ResolutionRequirement.MemberHasGold),

            new ResolutionDefinition(ResolutionKind.GiveGold, CouncilScope.WarCouncil,
                "Pledge gold to you", ResolutionRequirement.MemberHasGold),

            // Partie 1 (bringing the existing 1:1 resource verbs to the council): WarCouncil ONLY, unlike
            // GiveGold's two scopes. COUNCIL_ACTIONS.md's Kimi review is explicit: influence is a CLAN-level
            // resource, and the 1:1 verb already excludes giver == Clan.PlayerClan, so ClanLords (where every
            // seated lord IS the player's own clan) would be mechanically vacant, a false promise the LLM could
            // still voice ("I put my weight behind you") for a transfer that lifts to nothing. WarCouncil seats
            // OTHER clans' own leaders (CouncilRosterResolver.WarCouncilCandidates), the one scope where a
            // seated member's clan is genuinely a different house that can pledge its own influence.
            new ResolutionDefinition(ResolutionKind.GiveInfluence, CouncilScope.WarCouncil,
                "Pledge your clan's influence", ResolutionRequirement.MemberCanGrantInfluence),

            // Partie 2 (COUNCIL_ACTIONS.md, grant_fief/revoke_fief): the FIRST two governance kinds to gain a
            // real executor. Re-scoped to WarCouncil ONLY (a fief is clan-owned, so "granting"/"revoking" one
            // within the player's own clan, ClanLords' own scope, is mechanically vacant, exactly the empty-
            // promise class the Kimi review flagged for the earlier ClanLords scaffold); the seated vassal is
            // the OTHER clan's own leader (CouncilRosterResolver.WarCouncilCandidates), a genuine crown/vassal
            // pair. Both also require ResolutionRequirement.PlayerIsKingdomLeader: a fief moves between the
            // crown and a vassal only under a true sovereign, never an independent clan (whose WarCouncil
            // fallback seats its OWN clan lords, see WarCouncilCandidates' own doc) or a vassal among peers.
            new ResolutionDefinition(ResolutionKind.GrantFief, CouncilScope.WarCouncil,
                "Grant a fief", ResolutionRequirement.PlayerIsKingdomLeader | ResolutionRequirement.PlayerHasGiveableFief),

            new ResolutionDefinition(ResolutionKind.RevokeFief, CouncilScope.WarCouncil,
                "Revoke a fief", ResolutionRequirement.PlayerIsKingdomLeader | ResolutionRequirement.SeatedVassalOwnsFief)
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
                case ResolutionKind.RevokeFief:        return "revoke_fief";
                case ResolutionKind.RejoinParty:       return "rejoin_party";
                case ResolutionKind.DeclareWar:        return "declare_war";
                case ResolutionKind.MakePeace:          return "make_peace";
                case ResolutionKind.GiveGold:           return "give_gold";
                case ResolutionKind.GiveInfluence:      return "give_influence";
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
        ///   escrow (StipendPolicy, CouncilStipendBehavior). DeclareWar and its symmetric counterpart MakePeace
        ///   are the first FACTION-CENTRIC executors. GiveGold, Partie 1's first VERB brought to the council
        ///   (CouncilLift.SettleGiveGold, GoldPledgePolicy), is a seated LORD's own gold pledged TO the player,
        ///   the mirror image of GrantStipend's money direction. GiveInfluence (CouncilLift.SettleGiveInfluence,
        ///   reusing CourtActionPolicy.InfluenceGrant/CanAffordInfluenceGrant, the same math the 1:1
        ///   give_influence verb already uses) is a seated ALLIED CLAN LEADER's own clan influence pledged TO
        ///   the player's clan, WarCouncil-only and carrying no LLM-chosen amount (the policy computes it).
        ///   GrantFief and RevokeFief (CouncilLift.SettleGrantFief/SettleRevokeFief), Partie 2's first two
        ///   GOVERNANCE executors (COUNCIL_ACTIONS.md), move a crown fief between the player's own clan and a
        ///   seated WarCouncil vassal's; RevokeFief additionally plants a maximum
        ///   <see cref="CalradiaRemembers.Logic.Grudges.GrudgeSource.FiefRevoked" /> grudge and a real relation
        ///   hit on the spoliated lord, one of the gravest deeds the table can hand down. The remaining scoped
        ///   menu motions are recorded but not yet executed, so they arrive with their variant (Q5). The runtime
        ///   teaches and lifts only executable kinds, so the table never promises a deed it cannot keep.
        /// </summary>
        public static bool IsExecutable(ResolutionKind kind)
            => kind == ResolutionKind.Quest || kind == ResolutionKind.AppointGovernor
            || kind == ResolutionKind.AssignPartyRole || kind == ResolutionKind.RejoinParty
            || kind == ResolutionKind.DispatchOnMission || kind == ResolutionKind.GrantStipend
            || kind == ResolutionKind.DeclareWar || kind == ResolutionKind.MakePeace
            || kind == ResolutionKind.GiveGold || kind == ResolutionKind.GiveInfluence
            || kind == ResolutionKind.GrantFief || kind == ResolutionKind.RevokeFief;

        /// <summary>
        ///   True for a FACTION-CENTRIC kind (<see cref="ResolutionKind.DeclareWar" /> and its symmetric
        ///   counterpart <see cref="ResolutionKind.MakePeace" />): its pledge concerns two FACTIONS, not a seated
        ///   member's own fate, so it has no member actor for the ordinary seated-member re-validation every
        ///   other kind gets at the lift (CouncilLift.Settle resolves entry.TargetHeroId and re-checks
        ///   CouncilRosterResolver.EvaluateNow before dispatch). The mod's lift consults this to skip that gate
        ///   specifically for a faction-centric kind and dispatch straight to its own executor, which
        ///   re-validates the FACTIONS instead (bridge-is-law, just on a different subject).
        /// </summary>
        public static bool IsFactionCentric(ResolutionKind kind)
            => kind == ResolutionKind.DeclareWar || kind == ResolutionKind.MakePeace;

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
