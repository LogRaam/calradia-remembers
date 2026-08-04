// Code written by Gabriel Mailhot, 27/07/2026.
// The council's LIFT, extracted from ChatViewModel so it has ONE home (council audit C4). Two callers run the
// exact same settlement: the chat view-model at a normal close (reporting through the chat window), and
// CouncilSessionBehavior on load, when a council was in flight at save time and its window is gone (reporting
// through a quick-info banner). Both walk only AGREED pledges (AssemblyLedger.PendingCommit), which a normal
// close would already have turned into Applied/Failed, so a reloaded council can never double-execute a pledge
// the window already settled: only a genuinely uncommitted one is lifted here.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Settles a council's agreed pledges against the world as it stands now, reporting through a callback.</summary>
   internal static class CouncilLift
   {
      /// <summary>
      ///   Walks every AGREED pledge and settles it, writing an unkept-pledge memory (M7) on any that fails.
      ///   <paramref name="report" /> receives a player-facing line per pledge (message, isPositive); a headless
      ///   caller may pass a banner reporter, or null to stay silent. <paramref name="parleyTargetFactionId" /> is
      ///   the Parley's own carried state (null for every other scope): the StringId of the enemy faction this
      ///   council was convened against, threaded through to <see cref="SettleMakePeace" /> as its PREFERRED
      ///   target (see that method's own doc for why it still falls back to entry.TargetHint).
      /// </summary>
      public static void Finalize(AssemblyLedger ledger, CouncilScope scope, NpcMemoryCampaignBehavior behavior, int gameDay, Action<string, bool> report, string parleyTargetFactionId = null)
      {
         if (ledger == null || behavior?.Store == null) return;

         List<AssemblyResolutionEntry> pending = ledger.PendingCommit().ToList();

         if (pending.Count == 0) return;

         foreach (AssemblyResolutionEntry entry in pending)
         {
            try
            {
               Settle(ledger, entry, scope, behavior, gameDay, report, parleyTargetFactionId);
            }
            catch (Exception ex)
            {
               ModLog.Error($"[COUNCIL] lift: resolution {entry.Id} threw and was skipped: {ex.GetType().Name}: {ex.Message}");
               ledger.MarkFailed(entry.Id, "Something went wrong settling this.");
               report?.Invoke("A pledge from the council could not be settled: something went wrong.", false);
            }

            // M7: a pledge that could not be kept still leaves the promiser a memory of having given their word
            // for nothing (the KEPT case already gets its own quest memory). Skips a dead/profileless member.
            if (entry.Status == ResolutionStatus.Failed)
               RememberPledge(behavior, entry.TargetHeroId, entry.Detail, CouncilPledgeOutcome.Failed, gameDay);
         }
      }

      /// <summary>
      ///   Writes the member's own first-person memory of a council pledge that was NOT kept (withdrawn or
      ///   failed). Public so the withdraw path (still in the view-model) shares this one writer. Skips a dead or
      ///   profileless member and persists, since the profile is the durable record.
      /// </summary>
      public static void RememberPledge(NpcMemoryCampaignBehavior behavior, string heroId, string detail, CouncilPledgeOutcome outcome, int gameDay)
      {
         if (behavior?.Store == null || string.IsNullOrEmpty(heroId)) return;

         Hero hero = Hero.FindFirst(h => h.StringId == heroId);
         if (hero is not {IsAlive: true}) return;

         NpcProfile profile = behavior.Store.Get(heroId);
         if (profile == null) return;

         profile.Events.Add(new NotableEvent(gameDay, NotableEventType.Other, CouncilPledgeMemory.Summary(detail, outcome)));
         behavior.Store.Set(profile);
      }

      #region private

      private static void Settle(AssemblyLedger ledger, AssemblyResolutionEntry entry, CouncilScope scope, NpcMemoryCampaignBehavior behavior, int gameDay, Action<string, bool> report, string parleyTargetFactionId = null)
      {
         // Faction-centric kinds (declare_war and its symmetric counterpart make_peace, ResolutionCatalog.IsFactionCentric)
         // are dispatched BEFORE the ordinary seated-member gate below: their pledge concerns two FACTIONS, not
         // the fate of entry.TargetHeroId (that field still carries the PROPOSING member's id, for the M7
         // broken-pledge memory on failure, see Finalize), so re-validating THAT member's own council eligibility
         // here would wrongly block a table decision that was never actually about them (e.g. a proposer who has
         // since left the roster must not sink a war, or a peace, the rest of the table still agreed to).
         // SettleDeclareWar/SettleMakePeace re-validate the actual subject of the pledge instead: the FACTIONS.
         if (ResolutionCatalog.TryParseKey(entry.Kind, out ResolutionKind peekedKind) && ResolutionCatalog.IsFactionCentric(peekedKind))
         {
            if (peekedKind == ResolutionKind.MakePeace)
               SettleMakePeace(ledger, entry, report, parleyTargetFactionId);
            else
               SettleDeclareWar(ledger, entry, report);

            return;
         }

         Hero actorHero = Hero.FindFirst(h => h.StringId == entry.TargetHeroId);
         string actorName = actorHero?.Name?.ToString() ?? "One of the council";

         CouncilEligibility verdict = CouncilRosterResolver.EvaluateNow(scope, actorHero);

         if (actorHero == null || !verdict.Eligible)
         {
            string reason = DescribeUnavailability(verdict.Reason, actorHero == null);
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // B2/M4: the registry, not a magic string, decides what the table can carry out. Only executable kinds
         // reach an executor; anything else (an unmapped kind, or a catalogue kind whose executor has not landed
         // yet, Q5 "extend then migrate") is reported honestly instead of silently applied or dropped. Quest,
         // AppointGovernor, AssignPartyRole, RejoinParty, DispatchOnMission, GrantStipend (the FIRST executor
         // that touches persisted save state), DeclareWar/MakePeace (the FACTION-CENTRIC pair, dispatched above
         // before this gate) and, Partie 1's own, GiveGold (SettleGiveGold, a seated lord's own purse pledged TO
         // the player) and GiveInfluence (SettleGiveInfluence, a seated ALLY's own clan influence pledged TO the
         // player's clan), Partie 2's GrantFief/RevokeFief, Partie 8's ExpelFromClan (SettleExpelFromClan, a
         // seated companion cast out of the player's own clan), and Partie 1's schemes kind PledgeAgainst
         // (SettlePledgeAgainst, a seated member's own scheme against a rival, reusing the 1:1 pledge_against
         // system end to end) are executable today, so a passing entry here is one of those; the switch below
         // this gate is the per-kind dispatch, extended the day a new executor is wired.
         if (!ResolutionCatalog.TryParseKey(entry.Kind, out ResolutionKind kind) || !ResolutionCatalog.IsExecutable(kind))
         {
            ledger.MarkFailed(entry.Id, "This kind of decision is not yet settled by the council.");
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) was not carried out: the table cannot yet act on this kind of decision.", false);

            return;
         }

         NpcProfile giver = behavior.Store.Get(entry.TargetHeroId);

         if (giver == null)
         {
            ledger.MarkFailed(entry.Id, "No record of them could be found.");
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: no record of them was found.", false);

            return;
         }

         // Council audit M4 -> B2: the executable gate above proves kind is Quest, AppointGovernor,
         // AssignPartyRole, or one of the later executors (RejoinParty, DispatchOnMission, GrantStipend,
         // DeclareWar/MakePeace, GiveGold/GiveInfluence, Partie 2's GrantFief/RevokeFief, and Partie 8's
         // ExpelFromClan) today; this switch is the per-kind dispatch that plug point replaces. Each case owns
         // its own guards and reporting so a new executor never has to thread through another kind's assumptions.
         switch (kind)
         {
            case ResolutionKind.AppointGovernor:
               SettleAppointGovernor(ledger, entry, actorHero, actorName, giver, report);

               return;

            case ResolutionKind.AssignPartyRole:
               SettleAssignPartyRole(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.RejoinParty:
               SettleRejoinParty(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.DispatchOnMission:
               SettleDispatchMission(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.GrantStipend:
               SettleGrantStipend(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.GiveGold:
               SettleGiveGold(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.GiveInfluence:
               SettleGiveInfluence(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.GrantFief:
               SettleGrantFief(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.RevokeFief:
               SettleRevokeFief(ledger, entry, actorHero, actorName, report, gameDay);

               return;

            case ResolutionKind.ExpelFromClan:
               SettleExpelFromClan(ledger, entry, actorHero, actorName, report, gameDay);

               return;

            case ResolutionKind.PledgeAgainst:
               SettlePledgeAgainst(ledger, entry, actorHero, actorName, giver, gameDay, report);

               return;

            case ResolutionKind.ArrangeMarriage:
               SettleArrangeMarriage(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.Quest:
            default:
               SettleQuest(ledger, entry, actorHero, actorName, giver, behavior, gameDay, report);

               return;
         }
      }

      /// <summary>
      ///   The original (and, until AppointGovernor, only) executor: sets the pledging member a bandit-clear
      ///   task grounded on <see cref="AssemblyResolutionEntry.TargetHint" /> (the settlement named at proposal
      ///   time), exactly as an ordinary [QUEST] would be issued. Extracted verbatim from the pre-dispatch
      ///   <see cref="Settle" /> body: this is a refactor, not a behavior change.
      /// </summary>
      private static void SettleQuest(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, NpcProfile giver, NpcMemoryCampaignBehavior behavior, int gameDay, Action<string, bool> report)
      {
         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = entry.Detail,
            TargetSettlement = entry.TargetHint
         };

         var failed = false;
         string failureMessage = null;

         QuestService.IssueQuest(giver, actorHero, proposal, gameDay, behavior.Store, (text, positive) => {
            // m5: only a NEGATIVE notification means the quest could not be issued. Keying "failed" off any
            // invocation would silently flip a pledge to failed the day IssueQuest gains a success-path notice.
            if (positive) return;
            failed = true;
            failureMessage = text;
         });

         if (failed)
         {
            ledger.MarkFailed(entry.Id, failureMessage ?? "This could not be carried out.");
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failureMessage}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} makes good on the council's word: {entry.Detail}.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName}'s resolution settled as a real task ({entry.Detail}).");
      }

      /// <summary>
      ///   The FIRST governance executor (as opposed to the universal Quest task) and the pattern the rest of
      ///   ASSEMBLY reuses. Author's conditions, adopted as-is: the player must still lead the clan; the deed is
      ///   instant at the lift; it is revocable later (a plain <see cref="ChangeGovernorAction.RemoveGovernorOf" />
      ///   call elsewhere, not built here); and the prospective governor MAY refuse, but only MECHANICALLY, from
      ///   their own standing, decided BEFORE the appointment is ever applied - never narrated as a refusal
      ///   after the post already took effect (the phantom-deed class this project is eliminating).
      /// </summary>
      private static void SettleAppointGovernor(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, NpcProfile giver, Action<string, bool> report)
      {
         // Author's condition: only a player who still leads his own clan may hand out a governorship at the
         // lift. A pledge can sit Agreed for turns before the council closes, so this is re-checked here rather
         // than trusted from proposal time (a clan-leadership change in that window must not silently execute).
         if (Clan.PlayerClan?.Leader != Hero.MainHero)
         {
            const string reason = "the player no longer leads the clan.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // TargetHint (m2) carries the FIEF NAME for this kind (the same field a Quest pledge uses for a
         // settlement). Town or castle only, and still ours: a village has no Governor slot, and re-checking
         // ownership here (not just at proposal time) is the same bridge-is-law discipline the quest path
         // already gets from CouncilRosterResolver.EvaluateNow above.
         Settlement fief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == Clan.PlayerClan
            && string.Equals(s.Name?.ToString(), entry.TargetHint, StringComparison.OrdinalIgnoreCase));

         if (fief == null)
         {
            const string reason = "there is no fief of ours by that name left to govern.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // The refusal itself: standing toward the player is the mod's OWN personal regard
         // (NpcProfile.ReputationWithPlayer), the same source AssemblyInvitationFactsBuilder.RegardToInitiator
         // reads for a player-initiated summons, never the vanilla clan relation.
         GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(giver.ReputationWithPlayer);

         if (!verdict.Accepted)
         {
            ledger.MarkFailed(entry.Id, $"{actorName} declined the post");
            report?.Invoke($"{actorName} declines the council's offer to govern {fief.Name}.", false);

            return;
         }

         ChangeGovernorAction.Apply(fief.Town, actorHero);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} now governs {fief.Name}.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} appointed governor of {fief.Name}.");
      }

      /// <summary>
      ///   The SECOND governance executor, mirroring AppointGovernor's own wiring exactly. Author's stance
      ///   (Kimi default, ratified 2026-08-03): instant at the lift, no cost, no trust gate - unlike a
      ///   governorship this is a purely INTERNAL attribution (who rides doing what within the player's own
      ///   party), so there is no one to refuse it and no separate refusal policy to consult. The only guards
      ///   are mechanical, bridge-is-law re-checks of what the pledge actually needs to still be true: the
      ///   member is alive, still rides in the player's OWN party (a role held outside it is meaningless - Scout,
      ///   Engineer, Quartermaster and Surgeon all act on the party the member travels with), and the role named
      ///   in <see cref="AssemblyResolutionEntry.TargetHint" /> (the same single grounding slot AppointGovernor
      ///   uses for a fief name, here carrying the ROLE name instead, see <see cref="PartyRoleParser" />)
      ///   resolves to one of the four the council supports.
      /// </summary>
      private static void SettleAssignPartyRole(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.PartyBelongedTo != MobileParty.MainParty)
         {
            const string reason = "they no longer ride in the player's own party.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         CouncilPartyRole role = PartyRoleParser.Parse(entry.TargetHint);

         if (role == CouncilPartyRole.None)
         {
            const string reason = "no valid party role was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         MobileParty.MainParty.SetHeroPartyRole(actorHero, ToEnginePartyRole(role));

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} now serves as your {role}.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} assigned party role {role}.");
      }

      /// <summary>
      ///   The THIRD executable menu motion, closing bug d: a clan companion seated at a council while NOT
      ///   currently riding in the player's own party (the roster's own availability rule allows this seat, e.g.
      ///   while governing a town, see anySeatedMemberAvailable's doc) pledges to rejoin it. Author's stance,
      ///   mirroring AssignPartyRole and AppointGovernor's own wiring: instant at the lift, no price, no
      ///   companion-limit check (they are already a companion), and no separate refusal policy (a member
      ///   pledging their own return is not a favour asked of a third party). The guards are mechanical,
      ///   bridge-is-law re-checks of what the pledge still needs to be true: alive, not held prisoner, still a
      ///   clan companion (a clan LORD leads their own party and is out of scope here), and genuinely outside the
      ///   main party. Two edge cases get their own explicit handling rather than a silent skip: a companion who
      ///   LEADS their own separate party is refused in v1 (disbanding that party is the player's own call, not
      ///   this lift's), and a companion currently holding a settlement GOVERNOR post has that post released
      ///   first (the inverse of SettleAppointGovernor's own grant), reported alongside the rejoin.
      /// </summary>
      private static void SettleRejoinParty(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.IsPrisoner)
         {
            const string reason = "they are held prisoner and cannot travel.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!actorHero.IsPlayerCompanion)
         {
            const string reason = "they no longer belong to the player's clan as a companion.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.PartyBelongedTo == MobileParty.MainParty)
         {
            const string reason = "they already ride in the player's own party.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // A companion who leads their OWN party (not the main one) cannot simply be lifted out of it without
         // orphaning whoever they command: refuse in v1 rather than silently disbanding a party the player never
         // asked to break up.
         if (actorHero.PartyBelongedTo?.LeaderHero == actorHero)
         {
            const string reason = "they lead their own company; disband it first.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // The inverse of SettleAppointGovernor's own grant: a governor rejoining the party must first be
         // relieved of their post, or the fief would be left claiming a governor who has physically left it.
         bool wasGovernor = actorHero.GovernorOf != null;
         string governedFief = wasGovernor ? actorHero.GovernorOf.Name?.ToString() : null;
         if (wasGovernor)
         {
            ChangeGovernorAction.RemoveGovernorOf(actorHero);
            ModLog.Info($"[COUNCIL] lift: released {actorName} from governing {governedFief} so they could rejoin the party.");
         }

         AddHeroToPartyAction.Apply(actorHero, MobileParty.MainParty, false);

         ledger.MarkApplied(entry.Id);
         report?.Invoke(wasGovernor
            ? $"{actorName} steps down from governing {governedFief} and rejoins your party."
            : $"{actorName} rejoins your party.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} rejoined the player's party.");
      }

      /// <summary>
      ///   The FOURTH executable menu motion, mirroring AssignPartyRole and RejoinParty's own wiring: instant at
      ///   the lift, REUSES the existing companion-mission system end to end (CompanionMissionBehavior.Dispatch)
      ///   rather than inventing a new one. The mission TYPE is the single value <see cref="MissionTypeParser" />
      ///   resolves from <see cref="AssemblyResolutionEntry.TargetHint" /> (mirroring PartyRoleParser for a role
      ///   name); the DESTINATION is never named here, Dispatch's own <c>ResolveDestination</c> picks the nearest
      ///   suitable town, exactly the "system chooses, the model never names a place" design call. The guards
      ///   here are mechanical, bridge-is-law re-checks (alive, still a companion, still in the player's own
      ///   party, not already away): Dispatch itself re-validates every one of these again internally
      ///   (IsUsableCompanion, IsAway, CompanionMissions.CanAttempt), so a pledge that slips past this lift's own
      ///   guards is still caught, never silently mis-dispatched.
      /// </summary>
      private static void SettleDispatchMission(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!actorHero.IsPlayerCompanion)
         {
            const string reason = "they no longer belong to the player's clan as a companion.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.PartyBelongedTo != MobileParty.MainParty)
         {
            const string reason = "they no longer ride in the player's own party.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CompanionMissionBehavior.Instance == null)
         {
            const string reason = "no mission system is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CompanionMissionBehavior.Instance.IsAway(actorHero.StringId))
         {
            const string reason = "they are already away on an errand.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         CouncilMissionType type = MissionTypeParser.Parse(entry.TargetHint);

         if (type == CouncilMissionType.None)
         {
            const string reason = "no valid errand was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         (bool dispatched, string message) = CompanionMissionBehavior.Instance.Dispatch(actorHero, ToEngineMissionType(type), null);

         if (!dispatched)
         {
            ledger.MarkFailed(entry.Id, message ?? "This could not be carried out.");
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {message}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         report?.Invoke(message, true);
         ModLog.Info($"[COUNCIL] lift: {actorName} dispatched on a {type} errand.");
      }

      /// <summary>
      ///   The FIFTH executable menu motion, and the FIRST that touches PERSISTED SAVE STATE (a mod-tracked
      ///   escrow, CouncilStipendBehavior) rather than merely mutating a live campaign object. Author's design
      ///   (Kimi review, adopted as-is, COUNCIL_ACTIONS.md "grant_stipend"): a recurring payment to a seated clan
      ///   companion, FIXED at a 30-day tranche, funded entirely up front so the promise can never fail mid-term
      ///   (CouncilStipendBehavior.TryStart deducts the whole tranche from the player's gold into escrow here,
      ///   then a daily tick pays it out). The amount the LLM proposed (AssemblyResolutionEntry.TargetHint, this
      ///   kind's own grounding slot, mirroring how assign_party_role grounds on a role name) is re-clamped to
      ///   [50, 500] and the player's gold re-checked against the FULL tranche at settlement time (bridge is law):
      ///   an offering that only proved the MINIMUM tranche affordable must still be refused here if the ACTUAL
      ///   proposed amount now outruns the player's purse.
      /// </summary>
      private static void SettleGrantStipend(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!actorHero.IsPlayerCompanion)
         {
            const string reason = "they no longer belong to the player's clan as a companion.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!int.TryParse(entry.TargetHint, out int amount))
         {
            const string reason = "no valid stipend amount was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CouncilStipendBehavior.Instance == null)
         {
            const string reason = "no stipend ledger is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!CouncilStipendBehavior.Instance.TryStart(actorHero, amount, out string failReason))
         {
            ledger.MarkFailed(entry.Id, failReason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failReason}", false);

            return;
         }

         int clamped = StipendPolicy.ClampAmountPerDay(amount);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} will draw {clamped} denars a day from your coffers for the next {StipendPolicy.TrancheDays} days.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} granted a stipend of {clamped}/day for {StipendPolicy.TrancheDays} days.");
      }

      /// <summary>
      ///   Partie 1 (bringing the existing 1:1 resource verbs to the council): a seated LORD pledges to give the
      ///   PLAYER gold from their OWN purse, the mirror image of SettleGrantStipend's money direction. Mirrors
      ///   the existing 1:1 give_gold bridge's own solvency clamp exactly (BannerlordGameStateBridge.ExecuteGiveGold):
      ///   the amount the LLM proposed (AssemblyResolutionEntry.TargetHint, REUSING grant_stipend's own
      ///   TargetAmount field/parse rather than growing a new SDK field, see RecordCouncilResolution) is
      ///   re-clamped here against the giver's REAL gold (GoldPledgePolicy.ClampPayable), bridge is law, never
      ///   trusted from proposal/offer time. A giver who holds no gold at all is refused outright rather than
      ///   applied for zero denars while still reporting the pledge kept.
      /// </summary>
      private static void SettleGiveGold(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!int.TryParse(entry.TargetHint, out int proposed))
         {
            const string reason = "no valid sum was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         int payable = GoldPledgePolicy.ClampPayable(proposed, actorHero.Gold);

         if (payable < 1)
         {
            const string reason = "they hold no gold to give.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         GiveGoldAction.ApplyBetweenCharacters(actorHero, Hero.MainHero, payable);

         ledger.MarkApplied(entry.Id);
         report?.Invoke(payable < proposed
            ? $"{actorName} pledges gold to you: {payable} denars, all they could spare (less than the {proposed} promised)."
            : $"{actorName} pledges {payable} denars to you.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} gave the player {payable} denars (proposed {proposed}).");
      }

      /// <summary>
      ///   Partie 1 (bringing the existing 1:1 resource verbs to the council): a seated ALLIED CLAN LEADER
      ///   pledges their own clan's political influence to the player's clan, WarCouncil-only. Unlike
      ///   SettleGiveGold, there is no LLM-proposed amount to clamp (entry.TargetHint carries no grounding for
      ///   this kind, see RecordCouncilResolution): the amount is computed here fresh, entirely by the SAME
      ///   pure policy the 1:1 give_influence verb already uses (CourtActionPolicy.InfluenceGrant, gated by
      ///   CanAffordInfluenceGrant), never trusted or figured at proposal/offer time. Every gate the 1:1 verb
      ///   checks (CalradiaRemembers.Verbs.GiveInfluenceVerb / CourtActionResolver.TryGiveInfluence) is
      ///   re-verified here against CURRENT state (bridge is law): the giver still leads a clan distinct from
      ///   the player's (COUNCIL_ACTIONS.md's Kimi review: within the player's own clan this would be
      ///   mechanically vacant), both clans still seated in a kingdom, the relation floor still met, and the
      ///   giver's clan still holding comfortably more influence than the grant costs. The 1:1 verb's own
      ///   per-NPC cooldown is deliberately NOT re-checked here: a council pledge is a fresh commitment made at
      ///   this table, not a repeat of the 1:1 exchange, and the reserve/relation gates already guard against an
      ///   emptied treasury.
      /// </summary>
      private static void SettleGiveInfluence(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true, IsLord: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         Clan giver = actorHero.Clan;

         if (giver == null || giver == Clan.PlayerClan)
         {
            const string reason = "their own house is the player's own; there is no influence to lend.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (giver.Kingdom == null || Clan.PlayerClan?.Kingdom == null)
         {
            const string reason = "influence carries no weight outside a kingdom's court.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         int relation = actorHero.GetRelation(Hero.MainHero);

         if (relation < CourtActionPolicy.InfluenceMinRelation)
         {
            const string reason = "they no longer hold the player in high enough regard.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         int amount = CourtActionPolicy.InfluenceGrant(relation);

         if (amount <= 0 || !CourtActionPolicy.CanAffordInfluenceGrant(giver.Influence, amount))
         {
            const string reason = "their house cannot spare the influence just now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ChangeClanInfluenceAction.Apply(giver, -amount);
         ChangeClanInfluenceAction.Apply(Clan.PlayerClan, amount);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} lends their clan's influence to yours (+{amount}).", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} lent the player {amount} influence at court.");
      }

      /// <summary>
      ///   Partie 2 (COUNCIL_ACTIONS.md): the FIRST GOVERNANCE executor, granting a crown fief to a seated
      ///   WarCouncil vassal. A fief is CLAN-owned in vanilla (ChangeOwnerOfSettlementAction moves a hero's
      ///   whole CLAN in), so this is inherently cross-clan: the player's OWN clan gives one of its towns or
      ///   castles to the vassal's clan. Author's condition (Gabriel, "on ne veut pas que ce soit facile",
      ///   COUNCIL_ACTIONS.md): only the actual kingdom SOVEREIGN may do this (FiefTransferPolicy.CanGrant),
      ///   re-checked here rather than trusted from proposal time, exactly like every other executor. Applied
      ///   via <see cref="ChangeOwnerOfSettlementAction.ApplyByKingDecision" />, the same detail vanilla's own
      ///   fief councils use for a sovereign's own decision (never ApplyBySiege/ApplyByRebellion, which carry
      ///   conquest-specific side effects this deed does not have).
      /// </summary>
      private static void SettleGrantFief(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // Defensive, mirroring SettleGiveInfluence's own re-check: the beneficiary must be a genuine OTHER
         // house. A true PlayerIsKingdomLeader already implies this (WarCouncilCandidates only falls back to the
         // player's own clan lords when NO kingdom exists), but re-checking here costs nothing and keeps this
         // executor honest even if the roster gate is ever relaxed.
         if (actorHero.Clan == null || actorHero.Clan == Clan.PlayerClan)
         {
            const string reason = "they belong to the player's own house; there is no fief to grant across houses.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;

         // TargetHint (m2) carries the FIEF NAME, the same grounding slot appoint_governor's own pledge uses.
         // Town or castle only (a village is not cedable on its own, COUNCIL_ACTIONS.md), and STILL a crown fief
         // the player's own clan holds right now: a fief already given or lost between the pledge and the lift
         // must refuse rather than conjure a transfer from nothing.
         Settlement fief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == Clan.PlayerClan
            && string.Equals(s.Name?.ToString(), entry.TargetHint, StringComparison.OrdinalIgnoreCase));

         if (!FiefTransferPolicy.CanGrant(playerIsKingdomLeader, fief != null))
         {
            string reason = !playerIsKingdomLeader
               ? "the player no longer rules a kingdom to grant a crown fief from."
               : "there is no crown fief of ours by that name left to grant.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // Kimi review (COUNCIL_ACTIONS.md, grant_fief cas limites): a fief ceded while a governor is in post
         // would otherwise leave that governor a wandering hero in what is now foreign land. Release the post
         // first (mirroring SettleRejoinParty's own governor-release), and send a released PLAYER companion
         // back to the main party so they are never simply stranded.
         Hero formerGovernor = fief.Town?.Governor;
         if (formerGovernor != null)
         {
            ChangeGovernorAction.RemoveGovernorOf(formerGovernor);

            if (formerGovernor.IsPlayerCompanion && formerGovernor.PartyBelongedTo != MobileParty.MainParty)
               AddHeroToPartyAction.Apply(formerGovernor, MobileParty.MainParty, false);
         }

         ChangeOwnerOfSettlementAction.ApplyByKingDecision(actorHero, fief);

         // Author's condition: "on ne veut pas que ce soit facile". A fixed, unconditional influence cost
         // (FiefTransferPolicy.GrantInfluenceCost), never gated on the player's current reserve: the sovereign's
         // OWN prerogative to grant is never blocked by a thin treasury, but it is never a free favour either.
         if (Clan.PlayerClan != null) ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -FiefTransferPolicy.GrantInfluenceCost);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{fief.Name} passes to {actorName}'s house by the crown's own decision (-{FiefTransferPolicy.GrantInfluenceCost} influence).", true);
         ModLog.Info($"[COUNCIL] lift: {fief.Name} granted to {actorName}'s clan by royal decision.");
      }

      /// <summary>
      ///   Partie 2 (COUNCIL_ACTIONS.md): the SECOND GOVERNANCE executor, and grant_fief's grim mirror: strips a
      ///   crown fief BACK from a seated WarCouncil vassal. Author's condition (Gabriel: "une des pires
      ///   punitions qui puisse être faite"), corrected by the Kimi review from "chef de clan" to the true
      ///   kingdom SOVEREIGN (a clan leader has no fief of a clanmate's to reclaim, a clan's own fiefs already
      ///   belong to it): re-checked here via FiefTransferPolicy.CanRevoke, bridge is law. On success, plants a
      ///   MAXIMUM <see cref="GrudgeSource.FiefRevoked" /> grudge via the REAL gate (GrudgeBehavior.TryPlant,
      ///   never a bare relation hit alone, so the loss surfaces in conversation and the encyclopedia like every
      ///   other tier-2/3 source) plus a real relation hit (FiefTransferPolicy.RevokeRelationHit). Defection
      ///   ("il est fort probable qu'un NPC quitte le clan dans les jours qui suivent") is DELIBERATELY NOT
      ///   wired: vanilla's own ChangeKingdomAction.ApplyByLeaveKingdom is immediate and unconditional, not the
      ///   probabilistic, delayed departure Gabriel described, and building that properly (a tracked timer
      ///   weighted by standing) is a real new feature, not a one-line reuse; see the TODO below.
      /// </summary>
      private static void SettleRevokeFief(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report, int gameDay)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.Clan == null || actorHero.Clan == Clan.PlayerClan)
         {
            const string reason = "they hold no separate house whose fief could be revoked.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;

         Clan spoliatedClan = actorHero.Clan;

         // TargetHint (m2) carries the FIEF NAME, exactly like grant_fief. STILL owned by the SPOLIATED vassal's
         // own clan right now: naming a fief they never held, or already lost, would confiscate nothing.
         Settlement fief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == spoliatedClan
            && string.Equals(s.Name?.ToString(), entry.TargetHint, StringComparison.OrdinalIgnoreCase));

         if (!FiefTransferPolicy.CanRevoke(playerIsKingdomLeader, fief != null))
         {
            string reason = !playerIsKingdomLeader
               ? "the player no longer rules a kingdom to revoke a fief in its name."
               : $"{actorName} holds no fief of that name to revoke.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ChangeOwnerOfSettlementAction.ApplyByKingDecision(Hero.MainHero, fief);

         // Author's condition: one of the gravest punishments the table can hand down. Planted through the
         // REAL grudge gate (anti-noise guardrails, per-holder cap) rather than force-inserted, so a maximum
         // magnitude pledge still respects the same admission rule every other grudge in the game does.
         GrudgeBehavior.Instance?.TryPlant(actorHero, Hero.MainHero, GrudgeSource.FiefRevoked,
            $"stripped of {fief.Name} by the crown's own decision", FiefTransferPolicy.RevokeGrudgeMagnitude,
            knownToPlayer: true, gameDay);

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(actorHero, Hero.MainHero, FiefTransferPolicy.RevokeRelationHit, false);

         // TODO (deferred, see this method's own doc): a real probabilistic, delayed defection
         // (ChangeKingdomAction.ApplyByLeaveKingdom, weighted by the spoliated lord's standing, some days after
         // the confiscation) is a follow-up build, not hand-rolled here to avoid a fragile, unconditional exit
         // that does not match Gabriel's own "fort probable... dans les jours qui suivent" description.

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{fief.Name} is stripped from {actorName} and returns to the crown.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} was stripped of {fief.Name} by royal decision; a grudge is planted.");
      }

      /// <summary>
      ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review): the council's harshest INTERNAL sanction, casting a
      ///   seated COMPANION out of the player's own clan entirely. Guards re-checked here rather than trusted
      ///   from proposal/offer time (bridge is law), matching <see cref="ExpulsionPolicy.CanExpel" /> exactly:
      ///   the player must still lead the clan (a companion's hire/fire is a genuine CLAN-internal decision,
      ///   unlike grant_fief/revoke_fief's cross-clan sovereignty bar), the actor must still be a live COMPANION
      ///   of that same clan (never a family member or a fief-holding lord, both out of scope for this build,
      ///   see ExpulsionPolicy's own header), and never the player's own spouse (that bond has its own
      ///   dissolution channel, end_marriage/divorce, never this one). An additional mechanical guard beyond the
      ///   pure policy: a companion AWAY on a companion-mission errand (<see cref="CompanionMissionBehavior.IsAway" />)
      ///   is refused outright rather than expelled out from under an in-flight errand, mirroring
      ///   SettleDispatchMission's own re-check of the same state. On success, uses the SAME native path the
      ///   mod's own voluntary-departure flow already uses for an unhappy companion leaving
      ///   (<see cref="RemoveCompanionAction.ApplyByFire" />, see CompanionHappinessBehavior.Depart /
      ///   WarWearinessBehavior): the companion becomes a Fugitive (decompiled TaleWorlds.CampaignSystem.Actions.
      ///   RemoveCompanionAction: MakeHeroFugitiveAction.Apply under RemoveCompanionDetail.Fire), never quietly
      ///   returned to a hireable Wanderer in a tavern, a fitting consequence for a chief casting someone out
      ///   rather than a costless dismissal. ApplyByFire ALSO releases a held governorship internally
      ///   (companion.GovernorOf != null -> ChangeGovernorAction.RemoveGovernorOf), so the explicit check below
      ///   is redundant for correctness and kept only so the report line can name the vacated fief, exactly like
      ///   SettleRejoinParty's and SettleGrantFief's own governor-release pattern. Plants a
      ///   <see cref="GrudgeSource.Expelled" /> grudge through the REAL gate (GrudgeBehavior.TryPlant, anti-noise
      ///   guardrails, per-holder cap) plus a real relation hit (ExpulsionPolicy.RelationHit), so the deed is
      ///   never a consequence-free button.
      /// </summary>
      private static void SettleExpelFromClan(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report, int gameDay)
      {
         if (actorHero is not {IsAlive: true})
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         bool playerIsClanLeader = Clan.PlayerClan?.Leader == Hero.MainHero;
         bool isSpouse = actorHero == Hero.MainHero?.Spouse;

         if (!ExpulsionPolicy.CanExpel(playerIsClanLeader, actorHero.IsPlayerCompanion, isSpouse, actorHero.IsAlive))
         {
            string reason = !playerIsClanLeader
               ? "the player no longer leads the clan."
               : !actorHero.IsPlayerCompanion
                  ? "they no longer belong to the player's clan as a companion."
                  : "they are the player's own spouse; that bond can only be ended another way.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CompanionMissionBehavior.Instance != null && CompanionMissionBehavior.Instance.IsAway(actorHero.StringId))
         {
            const string reason = "they are away on an errand; recall them before casting them out.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         bool wasGovernor = actorHero.GovernorOf != null;
         string governedFief = wasGovernor ? actorHero.GovernorOf.Name?.ToString() : null;

         RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, actorHero);

         GrudgeBehavior.Instance?.TryPlant(actorHero, Hero.MainHero, GrudgeSource.Expelled,
            "cast out of the clan by its own chief", ExpulsionPolicy.GrudgeMagnitude,
            knownToPlayer: true, gameDay);

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(actorHero, Hero.MainHero, ExpulsionPolicy.RelationHit, false);

         ledger.MarkApplied(entry.Id);
         report?.Invoke(wasGovernor
            ? $"{actorName} is stripped of the governorship of {governedFief} and cast out of your clan."
            : $"{actorName} is cast out of your clan.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} was expelled from the player's clan; a grudge is planted.");
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's schemes block): a seated member pledges to move against a named RIVAL,
      ///   REUSING the existing 1:1 pledge_against system end to end via the ONE shared code path both callers
      ///   now run, <see cref="CourtActionResolver.LaunchPledge" /> (the same real scheme launched through
      ///   SchemeStore.ForcePlot and the same TrackedCommitment recorded the 1:1 action already produces). Every
      ///   1:1 gate is re-checked here (bridge is law): the pledger is a living, free lord, not the player; the
      ///   rival is re-resolved BY NAME from <see cref="AssemblyResolutionEntry.TargetHint" /> (this kind's own
      ///   grounding slot, the SDK's dedicated ResolutionProposal.TargetRival field, see RecordCouncilResolution)
      ///   via <see cref="CourtActionResolver.ResolvePledgeTarget" />, which already excludes the player, the
      ///   pledger's own kin, and requires a living lord; a lord may hold only one pledge at a time
      ///   (CommitmentBehavior.HasActive). The COUNCIL'S OWN caveat beyond the reused 1:1 gates
      ///   (COUNCIL_ACTIONS.md's Kimi review, "trahison a table"): a pledge against a hero SEATED AT THIS SAME
      ///   COUNCIL is refused outright rather than allowed-and-taxed - Gabriel's v1 pick, simpler and safer than
      ///   pricing a public betrayal at the table. <see cref="AssemblyResolutionEntry.WitnessIds" /> (audit M6,
      ///   the other seats present when the pledge was recorded) is the cheap, no-new-field way to know who else
      ///   sat here, read by the pure <see cref="PledgeAgainstPolicy.IsRivalSeatedAtCouncil" />.
      /// </summary>
      private static void SettlePledgeAgainst(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, NpcProfile giver, int gameDay, Action<string, bool> report)
      {
         if (actorHero is not {IsLord: true, IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         var commitmentBehavior = CommitmentBehavior.Instance;

         if (commitmentBehavior == null)
         {
            const string reason = "no commitment ledger is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (commitmentBehavior.HasActive(actorHero.StringId))
         {
            const string reason = "they already have a pledge outstanding, one vow at a time.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         Hero target = CourtActionResolver.ResolvePledgeTarget(entry.TargetHint, actorHero);

         if (target == null)
         {
            const string reason = "no rival by that name is known to pledge against.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CourtActionResolver.IsCloseKin(actorHero, target))
         {
            const string reason = "they will not move against their own kin.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (PledgeAgainstPolicy.IsRivalSeatedAtCouncil(target.StringId, entry.WitnessIds))
         {
            const string reason = "the rival named sits at this very council; that treachery is refused at this table.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         CourtActionResolver.LaunchPledge(actorHero, target, giver, commitmentBehavior, gameDay);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} pledges to move against {target.Name}.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} pledged to move against {target.Name}.");
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md, Gabriel: "dans le conseil, le mariage peut etre une monnaie
      ///   d'echange... probablement juste arrange_mariage qui est valide"): a seated ALLIED CLAN LEADER
      ///   consents to wed a kin of the player's own house to one of their own (or themselves), REUSING the
      ///   existing 1:1 arrange_marriage system end to end via the ONE shared code path both callers now run,
      ///   <see cref="CourtActionResolver.TryArrangeMarriage" /> (the same real consent+eligibility+apply chain
      ///   the 1:1 action already ran). <paramref name="actorHero" /> IS the authorizer (the seated allied clan
      ///   leader whose authority over their own kin is checked exactly like the 1:1 conversation NPC's, since
      ///   WarCouncil always seats a clan's own head, <c>CouncilRosterResolver.WarCouncilCandidates</c>);
      ///   <see cref="AssemblyResolutionEntry.TargetHint" /> carries BOTH kin names composed together
      ///   (<see cref="ArrangeMarriageGroundingCodec" />, this kind's own grounding, since a single TargetHint
      ///   slot cannot otherwise hold two names). The COUNCIL'S OWN caveat beyond the reused 1:1 gates
      ///   (COUNCIL_ACTIONS.md's Kimi review, "l'effet de bord majeur a expliciter dans la resolution"): a
      ///   marriage moves one spouse's clan, so the report explicitly names who now belongs to which house
      ///   (<see cref="DescribeArrangeMarriagePrice" />) rather than leaving the player to discover the "price" of
      ///   this alliance-currency only after the fact.
      /// </summary>
      private static void SettleArrangeMarriage(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.Clan == null || actorHero.Clan == Clan.PlayerClan)
         {
            const string reason = "they belong to the player's own house; there is no cross-clan match to arrange.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!ArrangeMarriageGroundingCodec.TryDecompose(entry.TargetHint, out string playerKinName, out string targetKinName))
         {
            const string reason = "no two kin were named plainly enough to arrange a match.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ArrangeMarriageVerdict verdict = CourtActionResolver.TryArrangeMarriage(playerKinName, targetKinName, actorHero);

         if (verdict.Result != ArrangeMarriageResult.Wed)
         {
            string reason = DescribeArrangeMarriageFailure(verdict.Result, actorName);
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         string priceLine = DescribeArrangeMarriagePrice(verdict);
         report?.Invoke($"{verdict.A.Name} of the player's clan weds {verdict.B.Name} of {actorName}'s house, sealing the alliance. {priceLine}", true);
         ModLog.Info($"[COUNCIL] lift: {verdict.A.Name} (player clan) wed {verdict.B.Name} ({actorName}'s house), consent by {actorName}.");
      }

      /// <summary>Human-readable reason for every non-Wed <see cref="ArrangeMarriageResult" />, mirroring the 1:1 action's own refusal wording.</summary>
      private static string DescribeArrangeMarriageFailure(ArrangeMarriageResult result, string actorName)
      {
         switch (result)
         {
            case ArrangeMarriageResult.NoPlayerKin: return "no unwed kin of the player's own house could be named to this match.";
            case ArrangeMarriageResult.NoTargetKin: return $"{actorName} names no one of their house to give.";
            case ArrangeMarriageResult.NoAuthority: return $"{actorName} has no authority to promise that kin in marriage.";
            case ArrangeMarriageResult.NotSuitable: return "the two named could not lawfully wed (age, kinship, or one is already promised elsewhere).";
            case ArrangeMarriageResult.NotCrossClan: return "the kin named is already of the player's own clan.";
            case ArrangeMarriageResult.RefusedStanding: return $"{actorName}'s house will not give their kin to the player's house on its present standing.";
            default: return "the match could not be arranged.";
         }
      }

      /// <summary>
      ///   The "visible price" COUNCIL_ACTIONS.md's Kimi review demands: names whichever of the two newlyweds
      ///   actually changed clan (comparing each hero's clan AFTER MarriageAction.Apply against the clan
      ///   <see cref="CourtActionResolver.TryArrangeMarriage" /> captured just BEFORE it), so the report never
      ///   leaves the player to discover a lost companion-governor moved to the other house only later.
      /// </summary>
      private static string DescribeArrangeMarriagePrice(ArrangeMarriageVerdict verdict)
      {
         if (verdict.A.Clan != verdict.AClanBefore)
            return $"{verdict.A.Name} now belongs to {verdict.A.Clan?.Name}.";

         if (verdict.B.Clan != verdict.BClanBefore)
            return $"{verdict.B.Name} now belongs to {verdict.B.Clan?.Name}.";

         return "Neither joins the other's house by this match.";
      }

      /// <summary>
      ///   The WarCouncil's own executor, and the FIRST FACTION-CENTRIC one: declares war on the faction named in
      ///   <see cref="AssemblyResolutionEntry.TargetHint" /> (this kind's own grounding slot, mirroring how
      ///   assign_party_role grounds on a role name, see RecordCouncilResolution). Author's guards (bridge is
      ///   law, re-checked here rather than trusted from proposal/offer time, exactly like every other
      ///   executor): the player must still lead their own faction (kingdom ruler, or an independent clan that
      ///   IS its own faction, Hero.IsFactionLeader), the named faction must resolve among live Kingdoms and
      ///   independent Clans, it must not be the player's own faction, and the player must not already be at war
      ///   with it. (Discovery note: vanilla Bannerlord v1.3.15 has no formal "allied faction" stance to guard
      ///   against, ilspycmd-decompiled TaleWorlds.CampaignSystem.FactionManager exposes only IsAtWarAgainstFaction
      ///   / IsAtConstantWarAgainstFaction / IsNeutralWithFaction, and StanceType is Neutral/War only, so there is
      ///   no IsAlliedWithFaction to call.) Any failing guard MarkFails with an honest reason; success calls the
      ///   same native <see cref="DeclareWarAction.ApplyByDefault" /> the scenario system and cr.test both already use.
      /// </summary>
      private static void SettleDeclareWar(AssemblyLedger ledger, AssemblyResolutionEntry entry, Action<string, bool> report)
      {
         const string label = "The council's decision to declare war";

         if (Hero.MainHero is not {IsFactionLeader: true})
         {
            const string reason = "the player no longer leads a faction that can declare war.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         IFaction playerFaction = Hero.MainHero.MapFaction;

         if (playerFaction == null)
         {
            const string reason = "the player belongs to no faction.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         IFaction target = ResolveFactionByName(entry.TargetHint);

         if (target == null)
         {
            const string reason = "no faction by that name could be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         if (target == playerFaction)
         {
            const string reason = "that is the player's own faction.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         if (FactionManager.IsAtWarAgainstFaction(playerFaction, target))
         {
            string reason = $"the player is already at war with {target.Name}.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         DeclareWarAction.ApplyByDefault(playerFaction, target);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"Your council declares war on {target.Name}.", true);
         ModLog.Info($"[COUNCIL] lift: war council declared war on {target.Name}.");
      }

      /// <summary>
      ///   The Parley's own executor, declare_war's symmetric counterpart: ends the war between the player's
      ///   faction and the enemy faction the parley was convened against. <paramref name="parleyTargetFactionId" />
      ///   (the stored parley target, CouncilSessionBehavior's carried state) is PREFERRED over <see cref="AssemblyResolutionEntry.TargetHint" />
      ///   (the LLM's own free-text target_faction, resolved by name like declare_war's): the whole point of a
      ///   parley is that its target is already fixed by how it was convened, so the stored id is the more
      ///   trustworthy source, and the two should agree in the ordinary case; TargetHint remains the fallback for
      ///   the rare edge where no parley target was stored (e.g. a pre-Parley save recovering an in-flight
      ///   session, see CouncilSessionSnapshot's own additive-field doc) but the model still named one plainly.
      ///   Author's guards (bridge is law, re-checked here rather than trusted from proposal/offer time, exactly
      ///   like every other executor): the player must still lead their own faction (Hero.IsFactionLeader), the
      ///   target faction must resolve among live Kingdoms and independent Clans, and the player's faction must
      ///   still be at war with it. Any failing guard MarkFails with an honest reason; success calls the same
      ///   native <see cref="MakePeaceAction.Apply" /> the scenario system (ScenarioConsequenceExecutor.MakePeace)
      ///   already uses.
      /// </summary>
      private static void SettleMakePeace(AssemblyLedger ledger, AssemblyResolutionEntry entry, Action<string, bool> report, string parleyTargetFactionId)
      {
         const string label = "The council's decision to make peace";

         if (Hero.MainHero is not {IsFactionLeader: true})
         {
            const string reason = "the player no longer leads a faction that can make peace.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         IFaction playerFaction = Hero.MainHero.MapFaction;

         if (playerFaction == null)
         {
            const string reason = "the player belongs to no faction.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         IFaction target = ParleyFactionLookup.ByStringId(parleyTargetFactionId) ?? ResolveFactionByName(entry.TargetHint);

         if (target == null)
         {
            const string reason = "no faction by that name could be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         if (!FactionManager.IsAtWarAgainstFaction(playerFaction, target))
         {
            string reason = $"the player is not at war with {target.Name}.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{label} could not be carried out: {reason}", false);

            return;
         }

         MakePeaceAction.Apply(playerFaction, target);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"Your council makes peace with {target.Name}.", true);
         ModLog.Info($"[COUNCIL] lift: parley made peace with {target.Name}.");
      }

      /// <summary>
      ///   Resolves a faction by name (case-insensitive) among live Kingdoms and independent Clans, the same pool
      ///   SettleDeclareWar's/SettleMakePeace's target must come from. Internal (not private): also reused by
      ///   ModCheats.Council for the "cr.council parley &lt;faction name&gt;" convening syntax, so the console
      ///   command and the lift resolve a typed faction name identically.
      /// </summary>
      internal static IFaction ResolveFactionByName(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;

         string trimmed = name.Trim();

         IFaction kingdom = Kingdom.All?.FirstOrDefault(k =>
            string.Equals(k.Name?.ToString(), trimmed, StringComparison.OrdinalIgnoreCase));
         if (kingdom != null) return kingdom;

         return Clan.All?.FirstOrDefault(c =>
            c.Kingdom == null && string.Equals(c.Name?.ToString(), trimmed, StringComparison.OrdinalIgnoreCase));
      }

      /// <summary>Maps the Logic-side <see cref="CouncilMissionType" /> to the mod's own <see cref="CompanionMissionType" />. None never reaches here (guarded in <see cref="SettleDispatchMission" />).</summary>
      private static CompanionMissionType ToEngineMissionType(CouncilMissionType type)
      {
         switch (type)
         {
            case CouncilMissionType.GatherNews: return CompanionMissionType.GatherNews;
            case CouncilMissionType.Spy:        return CompanionMissionType.Spy;
            case CouncilMissionType.Steal:      return CompanionMissionType.Steal;
            case CouncilMissionType.Barter:     return CompanionMissionType.Barter;
            case CouncilMissionType.Envoy:      return CompanionMissionType.Envoy;
            default:                            throw new ArgumentOutOfRangeException(nameof(type), type, "CouncilMissionType.None must be guarded before this is called.");
         }
      }

      /// <summary>Maps the Logic-side <see cref="CouncilPartyRole" /> to the engine's own <see cref="PartyRole" />. None never reaches here (guarded in <see cref="SettleAssignPartyRole" />).</summary>
      private static PartyRole ToEnginePartyRole(CouncilPartyRole role)
      {
         switch (role)
         {
            case CouncilPartyRole.Scout:         return PartyRole.Scout;
            case CouncilPartyRole.Engineer:      return PartyRole.Engineer;
            case CouncilPartyRole.Quartermaster: return PartyRole.Quartermaster;
            case CouncilPartyRole.Surgeon:       return PartyRole.Surgeon;
            default:                             throw new ArgumentOutOfRangeException(nameof(role), role, "CouncilPartyRole.None must be guarded before this is called.");
         }
      }

      private static string DescribeUnavailability(CouncilExclusionReason reason, bool heroGone)
      {
         if (heroGone) return "they could not be found.";

         switch (reason)
         {
            case CouncilExclusionReason.Dead: return "they have since died.";
            case CouncilExclusionReason.Imprisoned: return "they are now held prisoner.";
            case CouncilExclusionReason.InBattle: return "they are caught in a battle right now.";
            case CouncilExclusionReason.InArmy: return "they are marching with an army right now.";
            case CouncilExclusionReason.OnMission: return "they are away on a mission right now.";
            case CouncilExclusionReason.NotOfThisCouncil: return "they no longer belong to this house.";
            default: return "they are no longer available.";
         }
      }

      #endregion
   }
}
