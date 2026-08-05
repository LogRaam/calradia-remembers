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
        GiveInfluence,

        /// <summary>
        ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review): the council's harshest INTERNAL sanction, the
        ///   player-chief casting a seated COMPANION out of their own clan entirely. Companions scope only (see
        ///   <see cref="CalradiaRemembers.Logic.Assemblies.ExpulsionPolicy" />'s own header for why Family and
        ///   ClanLords, this build's original brief, would be permanently unofferable dead code for a
        ///   companions-only kind: neither roster ever seats a companion). No target grounding beyond the actor:
        ///   the deed falls entirely on the seated companion named, nothing else to record.
        /// </summary>
        ExpelFromClan,

        /// <summary>
        ///   Partie 1 (COUNCIL_ACTIONS.md's schemes block): a seated member pledges to move against a named
        ///   RIVAL, REUSING the existing 1:1 pledge_against system end to end (the same real scheme launched
        ///   through <c>CalradiaRemembers.WorldEvents.SchemeStore.ForcePlot</c>, and the same
        ///   <c>CalradiaRemembers.Logic.TrackedCommitment</c> tracked afterward), both callers now running a
        ///   SINGLE shared code path (<c>CourtActionResolver.LaunchPledge</c>) so the pledge behaves identically
        ///   whichever table struck it. Scoped to ClanLords and Family (see <see cref="ResolutionCatalog.All" />):
        ///   a seated lord's own standing rivalries are exactly as plausible at the table as within the player's
        ///   own inner circle, unlike GiveGold/GiveInfluence's moneyed-lord-only scoping; never WarCouncil (which
        ///   already carries its own faction-scale motions, DeclareWar/MakePeace, for cross-house conflict) nor
        ///   Companions (a hired hand's own rivalry has no lord's standing behind it to make a scheme credible).
        ///   A pledge against a hero SEATED AT THIS SAME COUNCIL is refused outright at the lift
        ///   (COUNCIL_ACTIONS.md's Kimi review flags this "trahison a table" case explicitly; Gabriel's v1 pick
        ///   is to refuse it rather than allow-and-tax a public betrayal, see CouncilLift.SettlePledgeAgainst's
        ///   own doc and <see cref="PledgeAgainstPolicy" />).
        /// </summary>
        PledgeAgainst,

        /// <summary>
        ///   Partie 1 (COUNCIL_ACTIONS.md, Gabriel: "dans le conseil, le mariage peut etre une monnaie
        ///   d'echange... probablement juste arrange_mariage qui est valide"): a seated ALLIED CLAN LEADER
        ///   consents to wed one of their own kin (or themselves) to a kin of the player's own house, REUSING the
        ///   existing 1:1 arrange_marriage system end to end (the same shared core both callers now run,
        ///   <c>CalradiaRemembers.GameState.CourtActionResolver.TryArrangeMarriage</c>). WarCouncil ONLY
        ///   (COUNCIL_ACTIONS.md's Kimi review): a marriage alliance is a genuine CROSS-CLAN currency, exactly
        ///   the pool WarCouncil already seats (the OTHER clan's own leader, <c>CouncilRosterResolver.WarCouncilCandidates</c>),
        ///   never ClanLords/Companions/Family, where a seated member is either the player's own house already
        ///   or a hired hand with no house to offer. The one kind that grounds on TWO names rather than one (the
        ///   player-clan kin AND the ally's own kin): see <see cref="ArrangeMarriageGroundingCodec" /> for how
        ///   both are carried through the single <see cref="AssemblyResolutionEntry.TargetHint" /> slot.
        ///   grant_blessing is deliberately NOT brought to the council (COUNCIL_ACTIONS.md's Kimi review: its own
        ///   consent is already subsumed by ArrangedMatchPolicy's standing check), nor are marry/take_as_consort/
        ///   take_as_secret_lover (personal player-NPC acts, never a table decision).
        /// </summary>
        ArrangeMarriage,

        /// <summary>
        ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "swap_fiefs"): the player and a seated WarCouncil vassal
        ///   ATOMICALLY EXCHANGE one fief each, REUSING grant_fief's and revoke_fief's own machinery end to end
        ///   (Partie 2's fief transfer, <see cref="CalradiaRemembers.Logic.Assemblies.FiefTransferPolicy" />):
        ///   the player's own clan gives up one town/castle to the seated vassal's clan, and takes one of the
        ///   vassal's own back, in the SAME lift call, both or neither (CouncilLift.SettleSwapFiefs). WarCouncil
        ///   ONLY, mirroring GrantFief/RevokeFief's own scoping (a fief is clan-owned, so a swap within the
        ///   player's own clan, ClanLords' own scope, is mechanically vacant): <see cref="FiefSwapGroundingCodec" />
        ///   carries the two fief names the same way <see cref="ArrangeMarriageGroundingCodec" /> carries two kin
        ///   names, since a single <see cref="AssemblyResolutionEntry.TargetHint" /> slot cannot otherwise hold
        ///   both. Its own <see cref="ResolutionDefinition.Requires" /> is deliberately the literal AND of
        ///   GrantFief's and RevokeFief's own requirement flags (PlayerIsKingdomLeader | PlayerHasGiveableFief |
        ///   SeatedVassalOwnsFief): exactly the fact a swap needs (the player rules a kingdom, owns a giveable
        ///   fief, AND a seated vassal owns one too), already fully wired by Partie 2, so this kind needs NO new
        ///   <see cref="ResolutionRequirement" /> flag and NO new <see cref="ResolutionWorldFacts" /> field at
        ///   all.
        /// </summary>
        SwapFiefs,

        /// <summary>
        ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): the Parley's OWN submission/buy-off motion,
        ///   Parley-scoped ONLY (the one council seating an enemy faction leader to negotiate with at all). The
        ///   SEATED ENEMY leader pledges a recurring DAILY payment to the player from their own coffers, the
        ///   money moving the OPPOSITE direction of GrantStipend (funded by the player) and the SAME direction as
        ///   GiveGold (funded by the giver), but recurring rather than a one-off sum, and PAY-AS-YOU-GO rather
        ///   than escrowed (TributePolicy, CouncilTributeBehavior): unlike GrantStipend's whole-tranche escrow, a
        ///   tribute is checked against the payer's live gold fresh every day, so it can run dry mid-term and
        ///   must degrade gracefully rather than fail all at once. Not FACTION-CENTRIC (see
        ///   <see cref="IsFactionCentric" />): the pledge is the seated leader's OWN promise to pay, exactly like
        ///   GiveGold, never a decision about the two factions themselves the way MakePeace is, so it keeps the
        ///   ordinary seated-member re-validation at the lift (CouncilLift.SettleTribute).
        /// </summary>
        Tribute,

        /// <summary>
        ///   Rounds out the Parley toolkit (make_peace + tribute + release_prisoner, 2026-08-04): as a
        ///   concession in the SAME negotiation, the PLAYER releases an enemy lord they currently hold
        ///   prisoner back to freedom, REUSING the existing 1:1 free_prisoner mechanic end to end
        ///   (EndCaptivityAction.ApplyByReleasedByChoice, the same call BannerlordGameStateBridge.ExecuteFreePrisoner
        ///   and the 1:1 release_prisoner verb already use) plus the SAME +relation bump the release_prisoner
        ///   verb already gives a freed captive toward the player (CourtActionPolicy.ReleasePrisonerRelationBump).
        ///   Parley-scoped ONLY (the one roster that ever seats an enemy faction leader to negotiate a
        ///   concession from at all, mirroring Tribute's own single-scope entry): the seated ENEMY leader is the
        ///   actor whose own house benefits, never the freed captive themselves (a hero of that SAME faction,
        ///   re-validated at the lift so only a genuine concession to THIS counterpart, never an unrelated
        ///   captive of some third house, is ever freed here). Not FACTION-CENTRIC (see
        ///   <see cref="IsFactionCentric" />): the pledge is the seated leader's own ASK of the player, not a
        ///   decision about the two factions themselves the way MakePeace is, so it keeps the ordinary
        ///   seated-member re-validation at the lift (CouncilLift.SettleReleasePrisoner).
        /// </summary>
        ReleasePrisoner,

        /// <summary>
        ///   Partie 1 (COUNCIL_ACTIONS.md's lend_troops, Gabriel's own CONDITIONS, the highest-detail item in the
        ///   spec): a seated lord (ClanLords or WarCouncil, mirroring GiveGold's own two scopes) commits real
        ///   soldiers from their own party to the player. THREE ways this deliberately differs from the existing
        ///   1:1 lend_troops verb (<see cref="CalradiaRemembers.Verbs.LendTroopsVerb" />/
        ///   <c>CourtActionResolver.TryLendTroops</c>, cap 10, relation >= 20, lowest tier first): (A) Gabriel's
        ///   condition A, "il ne faut pas qu'un Lord du conseil se deplace lui-meme... il envoie une delegation":
        ///   the troops travel with a TEMPORARY delegation party the mod spawns and steers
        ///   (CalradiaRemembers.Assemblies.CouncilTroopDeliveryBehavior), never the donor lord in person, so this
        ///   is an ETALED (delayed) executor, unlike every other member-pledge kind above which settles instantly
        ///   at the lift; (B) the gift is PERMANENT (Gabriel: "le don de troupes est permanent"), never a loan
        ///   that returns; (C) never more than 25% of the donor's own party, split across tiers (Gabriel: "doit
        ///   avoir une distribution de tiers avec faible, moyen, haut"), computed by the dedicated
        ///   <see cref="CouncilTroopLoanPolicy" /> rather than the 1:1 verb's own cap/tier rule. No LLM-chosen amount
        ///   (mirrors <see cref="GiveInfluence" />'s own doc): the split is computed fresh at the lift from the
        ///   donor's LIVE roster, never a figure the model proposes.
        /// </summary>
        LendTroops,

        /// <summary>
        ///   Partie 1 (COUNCIL_ACTIONS.md's give_troops, the reciprocal of <see cref="LendTroops" />): the
        ///   PLAYER reinforces a seated lord (ClanLords or WarCouncil, mirroring LendTroops' own two scopes)
        ///   with real soldiers from the PLAYER's OWN party. Gabriel's own written condition (COUNCIL_ACTIONS.md,
        ///   Partie 1 give_troops) is that the player should pick the troops through the native party-transfer
        ///   SCREEN; reopening that screen at the lift, after the council window has already closed, is a
        ///   fiddly UX sequencing problem this v1 build does NOT solve (a DOCUMENTED FOLLOW-UP). v1 instead
        ///   reuses <see cref="CouncilTroopLoanPolicy.ComputeLoan" /> on the PLAYER's OWN live roster (the SAME
        ///   pure 25%-cap, tier-mixed math LendTroops already uses, never a re-tuning), so the count and split
        ///   are chosen by the SYSTEM, not the player, at settlement time. Unlike LendTroops, the deed is
        ///   INSTANT at the lift (CouncilLift.SettleGiveTroops): the player's own troops stand right there at
        ///   the council table, so no travelling delegation (CouncilTroopDeliveryBehavior) is needed for this
        ///   direction. No LLM-chosen amount (mirrors LendTroops'/GiveInfluence's own doc): the split is
        ///   computed fresh at the lift from the PLAYER's LIVE roster, never a figure the model proposes. Not
        ///   FACTION-CENTRIC (see <see cref="IsFactionCentric" />): the seated RECIPIENT lord is the actor, so
        ///   this keeps the ordinary seated-member re-validation, exactly like LendTroops does for its own
        ///   donor.
        /// </summary>
        GiveTroops,

        /// <summary>
        ///   R7-light (COUNCIL_ACTIONS.md's Partie 8, "swear_oath"): a seated member swears a VERIFIABLE oath
        ///   with a deadline, tracked by the GENERALIZED <see cref="CalradiaRemembers.Logic.TrackedCommitment" />/
        ///   CommitmentBehavior engine that pledge_against already built (now carrying pay_gold/keep_peace/protect
        ///   alongside move_against, see <see cref="CalradiaRemembers.Logic.CommitmentKind" />'s own doc for why
        ///   each of the three is cheaply verifiable by a real event). THREE catalogue entries (ClanLords, Family,
        ///   WarCouncil, see <see cref="ResolutionCatalog.All" />), one PER SCOPE rather than per oath kind: the
        ///   catalogue only proves SOME oath is plausible for SOME seated member (mirroring
        ///   <see cref="ResolutionKind.PledgeAgainst" />'s own loose gate), never which of the three the model
        ///   will actually name, so a single requirement flag,
        ///   <see cref="ResolutionRequirement.AnySeatedMemberCanSwearOath" />, covers all three kinds at once; the
        ///   WRONG kind for a given swearer (e.g. a companion naming keep_peace, which only a faction leader can
        ///   swear) is refused honestly at the lift (CouncilLift.SettleSwearOath), never taught or blocked here.
        ///   A PUBLIC oath (this council's own witnesses) costs more to break than a private one: see
        ///   <see cref="CalradiaRemembers.Logic.CommitmentPolicy.WitnessBreachPenalty" /> and
        ///   <see cref="CalradiaRemembers.Logic.Grudges.GrudgeSource.BrokenOath" />.
        /// </summary>
        SwearOath,

        /// <summary>
        ///   Kimi's "v1 invite d'honneur" (COUNCIL_ACTIONS.md's Partie 8, "give_hostage"), rounding out the
        ///   Parley toolkit with a FOURTH concession alongside MakePeace, Tribute, and ReleasePrisoner:
        ///   Parley-scoped ONLY (mirrors Tribute's/ReleasePrisoner's own single-scope entry, the one roster that
        ///   ever seats an enemy faction leader to negotiate a concession with at all). The seated enemy leader
        ///   gives an ELIGIBLE RELATIVE of their own (<see cref="HostageEligibilityPolicy" />: an adult child,
        ///   sibling, or spouse, never a minor, never a clan chief, never their clan's sole remaining heir) to
        ///   reside at the player's own court as a living guarantee, for a FIXED term
        ///   (<see cref="HostagePolicy.TermDays" />), then returns home. Grounds on the relative's OWN NAME (the
        ///   SDK's dedicated ResolutionProposal.TargetHostageName field, mirroring TargetPrisonerName's own
        ///   dedicated-field reasoning: a hostage is neither a captive the player holds nor a rival, so folding
        ///   the name into either existing field would be the same confusing lie those fields' own docs already
        ///   warn against). Not FACTION-CENTRIC (see <see cref="IsFactionCentric" />): the pledge is the seated
        ///   leader's own kinsman given up, not a decision about the two factions themselves the way MakePeace
        ///   is, so it keeps the ordinary seated-member re-validation at the lift (CouncilLift.SettleGiveHostage).
        ///   v2 (execute/imprison the hostage on a broken accord, COUNCIL_ACTIONS.md's own "captif contraint") is
        ///   a DOCUMENTED FOLLOW-UP, deliberately not built this slice: v1 is honest as long as the hostage is
        ///   never narrated as chained or guarded, since the engine genuinely gives them no such order.
        /// </summary>
        GiveHostage,

        /// <summary>
        ///   Rounds out the Parley toolkit with a FIFTH concession (COUNCIL_ACTIONS.md's Partie 8,
        ///   "non_aggression_pact"), Parley-scoped ONLY (mirrors Tribute's/ReleasePrisoner's/GiveHostage's own
        ///   single-scope entry): the seated enemy leader and the player agree their two FACTIONS will not raise
        ///   arms against one another for a FIXED term (<see cref="CalradiaRemembers.Logic.Assemblies.NonAggressionPactPolicy.TermDays" />).
        ///   HONEST-LIMITED BY DESIGN (see <see cref="CalradiaRemembers.Logic.Assemblies.NonAggressionPactPolicy" />'s
        ///   own header, Kimi's warning read first): vanilla has NO native inter-clan non-aggression, and the
        ///   vanilla AI will NOT respect one - this pact only ever binds (a) the mod's OWN hostile-quest emission
        ///   against the partner faction, refused outright while the pact holds
        ///   (<c>CalradiaRemembers.Quests.QuestFactory.Create</c>), and (b) a detected breach, sanctioned
        ///   (<c>CalradiaRemembers.Assemblies.CouncilPactBehavior.OnMapEventEnded</c>). Never phrase it as "there
        ///   will be peace": the prompt teaching (SDK PromptBuilder) must state the exact scope ("our companies
        ///   will not attack each other for N days"). Not FACTION-CENTRIC (see <see cref="IsFactionCentric" />):
        ///   the pledge is the seated leader's own promise struck at the table, not a decision about the two
        ///   factions the way MakePeace is, so it keeps the ordinary seated-member re-validation at the lift
        ///   (CouncilLift.SettleNonAggressionPact), which then resolves the ACTUAL faction pair (the player's
        ///   live faction and the parley's own stored target) the same way SettleReleasePrisoner already does.
        /// </summary>
        NonAggressionPact
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
        SeatedVassalOwnsFief = 4096,

        /// <summary>
        ///   At least one seated companion is currently expellable per <see cref="CalradiaRemembers.Logic.Assemblies.ExpulsionPolicy.CanExpel" />
        ///   (the player leads their own clan, the seat is a live companion of it, not the player's own
        ///   spouse). expel_from_clan's own fact.
        /// </summary>
        AnySeatedCompanionExpellable = 8192,

        /// <summary>
        ///   At least one seated member currently holds NO pledge already outstanding (CommitmentBehavior.HasActive
        ///   false): pledge_against's own fact. An APPROXIMATION (COUNCIL_ACTIONS.md's schemes block): proving a
        ///   genuine RIVAL also exists (standing enmity, non-kin, non-faction-leader) would need each seat's own
        ///   NpcProfile plus a full Hero.AllAliveHeroes scan per seat, real cost for a fact that only gates
        ///   TEACHING and never the exact rival (unknown until the LLM names one); the full validation still
        ///   happens at the lift regardless (CourtActionResolver.ResolvePledgeTarget, bridge is law), mirroring
        ///   the same "prove SOME pledge is possible, never the exact rival" discipline
        ///   <see cref="MemberHasGold" /> already uses for give_gold.
        /// </summary>
        AnySeatedMemberCanPledgeAgainst = 16384,

        /// <summary>
        ///   At least one seated ally's own house (a genuine OTHER clan) offers an unwed kin (or is themselves
        ///   unwed) AND the player's own clan holds an unwed kin of its own: arrange_marriage's own fact. An
        ///   APPROXIMATION, mirroring <see cref="MemberHasGold" />'s own loose gate: proves only that SOME match
        ///   could exist, never the exact pairing, hard eligibility (age, kinship, marital status), or family
        ///   consent (ArrangedMatchPolicy), the full validation still runs at the lift regardless
        ///   (<c>CalradiaRemembers.GameState.CourtActionResolver.TryArrangeMarriage</c>, bridge is law).
        /// </summary>
        AnySeatedAllyHasMarriageableKin = 32768,

        /// <summary>
        ///   At least one seated Parley counterpart is alive and leads a clan distinct from the player's own:
        ///   tribute's own gate. Parley-scoped only (the one roster that ever seats an enemy faction leader at
        ///   all), see <see cref="ResolutionCatalog" />'s single Tribute entry.
        /// </summary>
        AnySeatedTributePayerAvailable = 65536,

        /// <summary>
        ///   The player currently holds, in their own party, at least one hero prisoner belonging to the SAME
        ///   faction the parley was convened against: release_prisoner's own gate. Parley-scoped only, mirroring
        ///   <see cref="AnySeatedTributePayerAvailable" />'s own single-scope entry (the one roster where a
        ///   concession to a named enemy faction is meaningful at all).
        /// </summary>
        AnySeatedPrisonerAvailableToRelease = 131072,

        /// <summary>
        ///   At least one seated lord (ClanLords or WarCouncil, mirroring MemberHasGold's own two-scope entry)
        ///   currently leads their own party, is not at war with the player, and fields enough healthy troops that
        ///   <see cref="CouncilTroopLoanPolicy.ComputeLoan" /> would loan more than zero: lend_troops' own gate. An
        ///   APPROXIMATION, mirroring MemberHasGold's own loose gate: proves only that SOME loan is possible, never
        ///   the exact tier split, which is computed fresh at the lift from the donor's LIVE roster regardless
        ///   (CouncilLift.SettleLendTroops, bridge is law).
        /// </summary>
        AnySeatedMemberCanLendTroops = 262144,

        /// <summary>
        ///   At least one seated member (ClanLords/Family/WarCouncil, mirroring MemberHasGold's/AnySeatedMemberCanPledgeAgainst's
        ///   own loose gate) currently holds no commitment already outstanding and plausibly qualifies for AT
        ///   LEAST ONE of the three whitelisted oath kinds (holds gold, leads their own party, or leads their own
        ///   faction). swear_oath's own fact, see ResolutionKind.SwearOath.
        /// </summary>
        AnySeatedMemberCanSwearOath = 524288,

        /// <summary>
        ///   At least one seated Parley counterpart currently has AT LEAST ONE eligible relative to give as a
        ///   hostage (<see cref="HostageEligibilityPolicy" />: alive, free, adult, not a clan chief, not their
        ///   clan's sole remaining heir). give_hostage's own gate, Parley-scoped only, mirroring
        ///   <see cref="AnySeatedTributePayerAvailable" />'s own single-scope entry. Unlike that loose gate
        ///   (which only proves SOME payer plausibly exists), this one is EXACT: every relative the mod's own
        ///   HostageCandidateResolver enumerates is fully checked against the same policy the lift re-runs, so
        ///   there is no separate approximation to narrow down at settlement time, only the world moving between
        ///   offer and lift (a relative dying, being captured, or the pledger's own clan losing every other
        ///   adult member in between).
        /// </summary>
        AnySeatedHostageEligibleRelativeAvailable = 1048576,

        /// <summary>
        ///   At least one seated lord (ClanLords or WarCouncil, mirroring AnySeatedMemberCanLendTroops' own
        ///   two-scope entry) currently leads their own party with room to receive more troops, is alive, is
        ///   not the player, and is not at war with the player, AND the PLAYER's own party currently fields
        ///   enough healthy troops that <see cref="CouncilTroopLoanPolicy.ComputeLoan" /> would yield more than
        ///   zero from the PLAYER's roster: give_troops' own gate, the reciprocal of AnySeatedMemberCanLendTroops
        ///   (direction reversed: the player is the DONOR here, not a seated member). An APPROXIMATION, mirroring
        ///   AnySeatedMemberCanLendTroops' own loose gate: proves only that SOME gift is possible, never the
        ///   exact tier split or which seated lord ends up chosen, both computed fresh at the lift from LIVE
        ///   state regardless (CouncilLift.SettleGiveTroops, bridge is law).
        /// </summary>
        AnySeatedLordCanReceiveTroops = 2097152,

        /// <summary>
        ///   At least one seated Parley counterpart is alive and leads a clan distinct from the player's own, AND
        ///   the two are not already bound by a live pact: non_aggression_pact's own gate, Parley-scoped only,
        ///   mirroring <see cref="AnySeatedTributePayerAvailable" />'s own single-scope entry (the one roster that
        ///   ever seats an enemy faction leader to negotiate a concession with at all). The "not already under
        ///   pact" half (beyond Tribute's own bare loose gate) avoids teaching a redundant second pact while one
        ///   is already in flight between the same two factions - a real concession the table has ALREADY struck
        ///   is not a NEW one to offer again.
        /// </summary>
        AnySeatedPactPartnerAvailable = 4194304
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
                                    bool seatedVassalOwnsFief = false,
                                    bool anySeatedCompanionExpellable = false,
                                    bool anySeatedMemberCanPledgeAgainst = false,
                                    bool anySeatedAllyHasMarriageableKin = false,
                                    bool anySeatedTributePayerAvailable = false,
                                    bool anySeatedPrisonerAvailableToRelease = false,
                                    bool anySeatedMemberCanLendTroops = false,
                                    bool anySeatedMemberCanSwearOath = false,
                                    bool anySeatedHostageEligibleRelativeAvailable = false,
                                    bool anySeatedLordCanReceiveTroops = false,
                                    bool anySeatedPactPartnerAvailable = false)
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
            AnySeatedCompanionExpellable = anySeatedCompanionExpellable;
            AnySeatedMemberCanPledgeAgainst = anySeatedMemberCanPledgeAgainst;
            AnySeatedAllyHasMarriageableKin = anySeatedAllyHasMarriageableKin;
            AnySeatedTributePayerAvailable = anySeatedTributePayerAvailable;
            AnySeatedPrisonerAvailableToRelease = anySeatedPrisonerAvailableToRelease;
            AnySeatedMemberCanLendTroops = anySeatedMemberCanLendTroops;
            AnySeatedMemberCanSwearOath = anySeatedMemberCanSwearOath;
            AnySeatedHostageEligibleRelativeAvailable = anySeatedHostageEligibleRelativeAvailable;
            AnySeatedLordCanReceiveTroops = anySeatedLordCanReceiveTroops;
            AnySeatedPactPartnerAvailable = anySeatedPactPartnerAvailable;
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

        /// <summary>At least one seated companion is currently expellable. expel_from_clan's own fact, see ResolutionRequirement.AnySeatedCompanionExpellable.</summary>
        public bool AnySeatedCompanionExpellable { get; }

        /// <summary>At least one seated member holds no pledge already outstanding. pledge_against's own fact (an approximation, see ResolutionRequirement.AnySeatedMemberCanPledgeAgainst).</summary>
        public bool AnySeatedMemberCanPledgeAgainst { get; }

        /// <summary>At least one seated ally's own house offers an unwed kin AND the player's own clan holds one too. arrange_marriage's own fact (an approximation, see ResolutionRequirement.AnySeatedAllyHasMarriageableKin).</summary>
        public bool AnySeatedAllyHasMarriageableKin { get; }

        /// <summary>At least one seated Parley counterpart is alive and leads a clan distinct from the player's own. tribute's own fact (an approximation, see ResolutionRequirement.AnySeatedTributePayerAvailable).</summary>
        public bool AnySeatedTributePayerAvailable { get; }

        /// <summary>The player holds a hero prisoner of the parley enemy's own faction. release_prisoner's own fact, see ResolutionRequirement.AnySeatedPrisonerAvailableToRelease.</summary>
        public bool AnySeatedPrisonerAvailableToRelease { get; }

        /// <summary>At least one seated lord currently leads a party with troops to spare and is not at war with the player. lend_troops' own fact, see ResolutionRequirement.AnySeatedMemberCanLendTroops.</summary>
        public bool AnySeatedMemberCanLendTroops { get; }

        /// <summary>At least one seated member holds no commitment already outstanding and plausibly qualifies for one whitelisted oath kind. swear_oath's own fact, see ResolutionRequirement.AnySeatedMemberCanSwearOath.</summary>
        public bool AnySeatedMemberCanSwearOath { get; }

        /// <summary>At least one seated Parley counterpart has at least one eligible relative to give as a hostage. give_hostage's own fact, see ResolutionRequirement.AnySeatedHostageEligibleRelativeAvailable.</summary>
        public bool AnySeatedHostageEligibleRelativeAvailable { get; }

        /// <summary>At least one seated lord could receive troops from the PLAYER's own party, and the player's own party has troops to spare. give_troops' own fact, see ResolutionRequirement.AnySeatedLordCanReceiveTroops.</summary>
        public bool AnySeatedLordCanReceiveTroops { get; }

        /// <summary>At least one seated Parley counterpart is alive, leads a clan distinct from the player's own, and is not already bound by a live pact. non_aggression_pact's own fact, see ResolutionRequirement.AnySeatedPactPartnerAvailable.</summary>
        public bool AnySeatedPactPartnerAvailable { get; }
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
                "Revoke a fief", ResolutionRequirement.PlayerIsKingdomLeader | ResolutionRequirement.SeatedVassalOwnsFief),

            // Partie 8 (COUNCIL_ACTIONS.md's Kimi review, expel_from_clan): Companions scope ONLY, a deliberate
            // deviation from this build's original brief (Family/ClanLords), see ExpulsionPolicy's own header:
            // neither of those rosters ever seats a live companion (Family = blood/marriage kin, ClanLords =
            // Hero.IsLord members), so a companions-only kind offered there would never turn eligible. Companions
            // is the one scope every other companion-fate kind above (AssignPartyRole, AppointGovernor,
            // DispatchOnMission, GrantStipend, RejoinParty) already uses for exactly this reason.
            new ResolutionDefinition(ResolutionKind.ExpelFromClan, CouncilScope.Companions,
                "Expel from the clan", ResolutionRequirement.AnySeatedCompanionExpellable),

            // Partie 1 (COUNCIL_ACTIONS.md's schemes block): pledge_against, REUSING the existing 1:1 system end
            // to end. TWO entries, ClanLords and Family (never Companions/WarCouncil: a companion's own rivalry
            // has no lord's standing behind it to make a scheme credible, and WarCouncil already carries its own
            // faction-scale motions, declare_war/make_peace, for cross-house conflict).
            new ResolutionDefinition(ResolutionKind.PledgeAgainst, CouncilScope.ClanLords,
                "Pledge to move against a rival", ResolutionRequirement.AnySeatedMemberCanPledgeAgainst),

            new ResolutionDefinition(ResolutionKind.PledgeAgainst, CouncilScope.Family,
                "Pledge to move against a rival", ResolutionRequirement.AnySeatedMemberCanPledgeAgainst),

            // Partie 1 (COUNCIL_ACTIONS.md, "juste arrange_mariage qui est valide"): WarCouncil ONLY, the one
            // scope where a seated member's own house is a genuine OTHER clan (CouncilRosterResolver.WarCouncilCandidates),
            // never ClanLords/Companions/Family, where a seated member is either the player's own house already
            // (nothing to marry cross-clan) or a hired hand with no house of their own to offer.
            new ResolutionDefinition(ResolutionKind.ArrangeMarriage, CouncilScope.WarCouncil,
                "Arrange a marriage alliance", ResolutionRequirement.AnySeatedAllyHasMarriageableKin),

            // Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "swap_fiefs"): WarCouncil ONLY, mirroring GrantFief/
            // RevokeFief's own scoping exactly (a fief is clan-owned, so a swap within the player's own clan is
            // mechanically vacant). The requirement is the literal AND of GrantFief's and RevokeFief's own two
            // requirements: the player must rule a kingdom, hold a giveable fief of their own, AND the seated
            // vassal must hold a fief of their own to trade back. No new ResolutionRequirement flag needed: the
            // three flags already exist and already compose correctly through ResolutionEligibilityPolicy.Meets.
            new ResolutionDefinition(ResolutionKind.SwapFiefs, CouncilScope.WarCouncil,
                "Swap a fief", ResolutionRequirement.PlayerIsKingdomLeader | ResolutionRequirement.PlayerHasGiveableFief | ResolutionRequirement.SeatedVassalOwnsFief),

            // Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): the Parley's own submission/buy-off motion,
            // Parley-scoped ONLY (the one roster that ever seats an enemy faction leader to pay from their own
            // coffers). Mirrors MakePeace's own scope exactly, since a tribute is negotiated at the very same
            // table a peace would be, but is NOT faction-centric (see ResolutionKind.Tribute's own doc): it is
            // the seated leader's OWN promise, so it keeps the ordinary seated-member re-validation, unlike
            // MakePeace/DeclareWar.
            new ResolutionDefinition(ResolutionKind.Tribute, CouncilScope.Parley,
                "Pledge a daily tribute", ResolutionRequirement.AnySeatedTributePayerAvailable),

            // Rounds out the Parley toolkit (make_peace + tribute + release_prisoner, 2026-08-04): Parley-scoped
            // ONLY, mirroring Tribute's own single-scope entry (the one roster that ever seats an enemy faction
            // leader to negotiate a concession with at all). Unlike Tribute (funded from the SEATED leader's own
            // purse), the concession here is the PLAYER's own: an enemy lord they hold prisoner is set free.
            new ResolutionDefinition(ResolutionKind.ReleasePrisoner, CouncilScope.Parley,
                "Free a captive lord", ResolutionRequirement.AnySeatedPrisonerAvailableToRelease),

            // Kimi's "v1 invite d'honneur" (COUNCIL_ACTIONS.md's Partie 8, "give_hostage"): Parley-scoped ONLY,
            // mirroring Tribute's/ReleasePrisoner's own single-scope entry (the one roster that ever seats an
            // enemy faction leader to negotiate a concession with at all). A FOURTH Parley motion, rounding out
            // the toolkit alongside MakePeace, Tribute, and ReleasePrisoner.
            new ResolutionDefinition(ResolutionKind.GiveHostage, CouncilScope.Parley,
                "Give a hostage of good faith", ResolutionRequirement.AnySeatedHostageEligibleRelativeAvailable),

            // Partie 1 (COUNCIL_ACTIONS.md's lend_troops, Gabriel's own CONDITIONS): TWO entries, mirroring
            // GiveGold's own ClanLords/WarCouncil scoping exactly (a seated lord's own party is exactly as
            // plausible a source of spare troops in either council; never Companions or Family, where a seated
            // member is a hired hand or dynastic kin, not a lord fielding a real party of his own).
            new ResolutionDefinition(ResolutionKind.LendTroops, CouncilScope.ClanLords,
                "Lend you troops", ResolutionRequirement.AnySeatedMemberCanLendTroops),

            new ResolutionDefinition(ResolutionKind.LendTroops, CouncilScope.WarCouncil,
                "Lend you troops", ResolutionRequirement.AnySeatedMemberCanLendTroops),

            // Partie 1 (COUNCIL_ACTIONS.md's give_troops, the reciprocal of lend_troops): TWO entries, mirroring
            // LendTroops' own ClanLords/WarCouncil scoping exactly (a seated lord is exactly as plausible a
            // recipient of the player's own spare troops in either council; never Companions or Family, where a
            // seated member is a hired hand or dynastic kin, not a lord fielding a real party worth reinforcing).
            new ResolutionDefinition(ResolutionKind.GiveTroops, CouncilScope.ClanLords,
                "Reinforce them with troops", ResolutionRequirement.AnySeatedLordCanReceiveTroops),

            new ResolutionDefinition(ResolutionKind.GiveTroops, CouncilScope.WarCouncil,
                "Reinforce them with troops", ResolutionRequirement.AnySeatedLordCanReceiveTroops),

            // R7-light (COUNCIL_ACTIONS.md's Partie 8, "swear_oath"): THREE entries, one per scope where a
            // seated member could plausibly swear at least one of the three whitelisted oath kinds (pay_gold,
            // keep_peace, protect); ClanLords and Family mirror PledgeAgainst's own two scopes (a personal
            // vow of coin or protection is exactly as credible in either), WarCouncil adds the one scope where
            // keep_peace makes sense (a genuine faction leader is seated there). Never Companions: a hired
            // hand has no standing purse, party, or faction to swear any of the three against.
            new ResolutionDefinition(ResolutionKind.SwearOath, CouncilScope.ClanLords,
                "Swear an oath", ResolutionRequirement.AnySeatedMemberCanSwearOath),

            new ResolutionDefinition(ResolutionKind.SwearOath, CouncilScope.Family,
                "Swear an oath", ResolutionRequirement.AnySeatedMemberCanSwearOath),

            new ResolutionDefinition(ResolutionKind.SwearOath, CouncilScope.WarCouncil,
                "Swear an oath", ResolutionRequirement.AnySeatedMemberCanSwearOath),

            // Rounds out the Parley toolkit with a FIFTH concession (COUNCIL_ACTIONS.md's Partie 8,
            // "non_aggression_pact"): Parley-scoped ONLY, mirroring Tribute's/ReleasePrisoner's/GiveHostage's own
            // single-scope entry (the one roster that ever seats an enemy faction leader to negotiate a
            // concession with at all).
            new ResolutionDefinition(ResolutionKind.NonAggressionPact, CouncilScope.Parley,
                "Pledge a non-aggression pact", ResolutionRequirement.AnySeatedPactPartnerAvailable)
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
                case ResolutionKind.ExpelFromClan:      return "expel_from_clan";
                case ResolutionKind.PledgeAgainst:      return "pledge_against";
                case ResolutionKind.ArrangeMarriage:    return "arrange_marriage";
                case ResolutionKind.SwapFiefs:          return "swap_fiefs";
                case ResolutionKind.Tribute:             return "tribute";
                case ResolutionKind.ReleasePrisoner:     return "release_prisoner";
                case ResolutionKind.LendTroops:          return "lend_troops";
                case ResolutionKind.GiveTroops:          return "give_troops";
                case ResolutionKind.SwearOath:           return "swear_oath";
                case ResolutionKind.GiveHostage:          return "give_hostage";
                case ResolutionKind.NonAggressionPact:    return "non_aggression_pact";
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
        ///   hit on the spoliated lord, one of the gravest deeds the table can hand down. ExpelFromClan
        ///   (CouncilLift.SettleExpelFromClan), Partie 8's own harshest INTERNAL sanction, casts a seated
        ///   companion out of the player's own clan, planting a maximum-tier
        ///   <see cref="CalradiaRemembers.Logic.Grudges.GrudgeSource.Expelled" /> grudge and a real relation hit.
        ///   The remaining scoped menu motions are recorded but not yet executed, so they arrive with their
        ///   variant (Q5). PledgeAgainst (CouncilLift.SettlePledgeAgainst), Partie 1's schemes kind, reuses the
        ///   existing 1:1 pledge_against system end to end (a real scheme launched via SchemeStore.ForcePlot and
        ///   a TrackedCommitment recorded, the SAME shared code path, CourtActionResolver.LaunchPledge, the 1:1
        ///   action itself calls) on a seated ClanLords/Family member. ArrangeMarriage (CouncilLift.SettleArrangeMarriage),
        ///   Partie 1's own marriage-as-currency kind (COUNCIL_ACTIONS.md, "juste arrange_mariage qui est
        ///   valide"), WarCouncil-only, REUSES the existing 1:1 arrange_marriage system end to end (the SAME
        ///   shared core, CourtActionResolver.TryArrangeMarriage). Tribute (CouncilLift.SettleTribute), Partie 8's
        ///   own submission/buy-off motion, Parley-only, binds the seated ENEMY leader to a recurring DAILY
        ///   payment out of their own coffers (CouncilTributeBehavior, TributePolicy), pay-as-you-go rather than
        ///   escrowed like GrantStipend, so it degrades gracefully (a run of missed days ends it early) rather
        ///   than failing all at once. SwapFiefs (CouncilLift.SettleSwapFiefs), Partie 8's other own kind,
        ///   ATOMICALLY exchanges one fief between the player's own clan and a seated WarCouncil vassal's,
        ///   REUSING GrantFief's and RevokeFief's own machinery (FiefTransferPolicy.CanSwap,
        ///   ChangeOwnerOfSettlementAction.ApplyByKingDecision) both directions in the same lift call: every
        ///   guard is validated for BOTH fiefs before either transfer is applied, so a swap never moves one side
        ///   of the trade while leaving the other in place. ReleasePrisoner (CouncilLift.SettleReleasePrisoner),
        ///   rounding out the Parley toolkit alongside MakePeace and Tribute, REUSES the existing 1:1
        ///   free_prisoner mechanic end to end (EndCaptivityAction.ApplyByReleasedByChoice plus the SAME
        ///   +relation bump the 1:1 release_prisoner verb already gives a freed captive,
        ///   CourtActionPolicy.ReleasePrisonerRelationBump): the player frees an enemy lord they hold prisoner
        ///   as a concession to the seated counterpart's own house. LendTroops (CouncilLift.SettleLendTroops),
        ///   the highest-detail item in COUNCIL_ACTIONS.md's Partie 1, is the FIRST executor whose deed does not
        ///   settle at the lift itself: it hands the tier-split loan (CouncilTroopLoanPolicy) to a temporary delegation
        ///   party (CalradiaRemembers.Assemblies.CouncilTroopDeliveryBehavior) that physically travels to the
        ///   player and transfers the troops (a PERMANENT gift, Gabriel's condition B) on arrival, never the
        ///   donor lord in person (condition A). GiveTroops (CouncilLift.SettleGiveTroops), the reciprocal of
        ///   LendTroops, is the mirror-image direction: the PLAYER's OWN party reinforces a seated lord,
        ///   computed by the SAME CouncilTroopLoanPolicy but INSTANT at the lift (no travelling delegation,
        ///   since the player's own troops are already at the table); Gabriel's own condition that the player
        ///   choose the troops through the party-transfer screen is a documented v1 follow-up, not built this
        ///   slice (system-selected instead). The runtime teaches and lifts only
        ///   executable kinds, so the table never promises a deed it cannot keep.
        /// </summary>
        public static bool IsExecutable(ResolutionKind kind)
            => kind == ResolutionKind.Quest || kind == ResolutionKind.AppointGovernor
            || kind == ResolutionKind.AssignPartyRole || kind == ResolutionKind.RejoinParty
            || kind == ResolutionKind.DispatchOnMission || kind == ResolutionKind.GrantStipend
            || kind == ResolutionKind.DeclareWar || kind == ResolutionKind.MakePeace
            || kind == ResolutionKind.GiveGold || kind == ResolutionKind.GiveInfluence
            || kind == ResolutionKind.GrantFief || kind == ResolutionKind.RevokeFief
            || kind == ResolutionKind.ExpelFromClan || kind == ResolutionKind.PledgeAgainst
            || kind == ResolutionKind.ArrangeMarriage || kind == ResolutionKind.SwapFiefs
            || kind == ResolutionKind.Tribute || kind == ResolutionKind.ReleasePrisoner
            || kind == ResolutionKind.LendTroops || kind == ResolutionKind.SwearOath
            || kind == ResolutionKind.GiveHostage || kind == ResolutionKind.GiveTroops
            || kind == ResolutionKind.NonAggressionPact;

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
