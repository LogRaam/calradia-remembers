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
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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

         // parleyTargetFactionId is passed through here so a genuine Parley member (e.g. tribute's own payer)
         // re-validates correctly: without it FactsFor can never prove IsParleyCounterpart true, and every
         // Parley re-check would wrongly fail (see CouncilRosterResolver.EvaluateNow's own doc). Harmless for
         // every other scope, which never reads this parameter.
         CouncilEligibility verdict = CouncilRosterResolver.EvaluateNow(scope, actorHero, parleyTargetFactionId);

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
         // the player), GiveInfluence (SettleGiveInfluence, a seated ALLY's own clan influence pledged TO the
         // player's clan), and GiveItem (SettleGiveItem, a seated lord gives the player a named item from THEIR
         // OWN party's stores, the REVERSE of the 1:1 give_item action), Partie 2's GrantFief/RevokeFief, Partie 8's ExpelFromClan (SettleExpelFromClan, a
         // seated companion cast out of the player's own clan), Partie 1's schemes kind PledgeAgainst
         // (SettlePledgeAgainst, a seated member's own scheme against a rival, reusing the 1:1 pledge_against
         // system end to end), and Partie 8's LAST catalogue kind, SupportClaimant (SettleSupportClaimant, the
         // SAFE v1 over a risky live KingdomDecision/Election hook: a seated WarCouncil vassal's own clan
         // influence pledged TO the player's clan, REUSING GiveInfluence's own machinery end to end) are
         // executable today, so a passing entry here is one of those; the switch below
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
         // DeclareWar/MakePeace, GiveGold/GiveInfluence, Partie 2's GrantFief/RevokeFief, Partie 8's
         // ExpelFromClan/SwapFiefs/Tribute, and ReleasePrisoner, rounding out the Parley toolkit alongside
         // MakePeace and Tribute) today; this switch is the per-kind dispatch that plug point replaces. Each
         // case owns its own guards and reporting so a new executor never has to thread through another kind's
         // assumptions.
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

            case ResolutionKind.GiveItem:
               SettleGiveItem(ledger, entry, actorHero, actorName, report);

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

            case ResolutionKind.SwapFiefs:
               SettleSwapFiefs(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.Tribute:
               SettleTribute(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.ReleasePrisoner:
               SettleReleasePrisoner(ledger, entry, actorHero, actorName, report, parleyTargetFactionId);

               return;

            case ResolutionKind.LendTroops:
               SettleLendTroops(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.GiveTroops:
               SettleGiveTroops(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.SwearOath:
               SettleSwearOath(ledger, entry, actorHero, actorName, gameDay, report);

               return;

            case ResolutionKind.GiveHostage:
               SettleGiveHostage(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.NonAggressionPact:
               SettleNonAggressionPact(ledger, entry, actorHero, actorName, report, parleyTargetFactionId);

               return;

            case ResolutionKind.SponsorWard:
               SettleSponsorWard(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.SupportClaimant:
               SettleSupportClaimant(ledger, entry, actorHero, actorName, report);

               return;

            case ResolutionKind.Quest:
            default:
               SettleQuest(ledger, entry, actorHero, actorName, giver, behavior, gameDay, report);

               return;
         }
      }

      /// <summary>
      ///   The original (and, until AppointGovernor, only) executor: sets the pledging member a task grounded on
      ///   <see cref="AssemblyResolutionEntry.TargetHint" />, exactly as an ordinary [QUEST] would be issued.
      ///   Since 2026-08-26 the hint is a <see cref="QuestGroundingCodec" /> composite carrying WHICH deed is
      ///   pledged (quest_type) alongside the settlement: before that, every pledge grounded as a bandit-clear,
      ///   so a single bandit fight near the target completed the whole table's tasks at once (player report:
      ///   eight siege-council pledges stamped "Done" by clearing one bandit camp). Differently-typed pledges
      ///   now complete independently, each on its own real game event (QuestDeedTracker). A hint that carries
      ///   no readable type (legacy entries, an omitted or unsupported token) keeps the legacy bandit-clear
      ///   grounding via <see cref="CouncilQuestTypePolicy" />'s fallback.
      /// </summary>
      private static void SettleQuest(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, NpcProfile giver, NpcMemoryCampaignBehavior behavior, int gameDay, Action<string, bool> report)
      {
         string settlement = entry.TargetHint;
         string questTypeToken = null;

         if (QuestGroundingCodec.TryDecompose(entry.TargetHint, out string token, out string place))
         {
            questTypeToken = token;
            settlement = place;
         }

         var proposal = new QuestProposal {
            Type = CouncilQuestTypePolicy.Resolve(questTypeToken),
            Description = entry.Detail,
            TargetSettlement = settlement
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryAppointGovernor), the
         // SAME one the 1:1 appoint_governor action runs (2026-08-05, the reference personal-command slice), so a
         // governorship behaves identically whichever path granted it. Every guard this executor used to check
         // inline (player still leads the clan, the fief is still one of ours, the appointee's standing clears the
         // floor) is re-checked there in the same order; this method only maps the verdict back onto the council's
         // own ledger + reporting. TargetHint (m2) carries the FIEF NAME for this kind (the same field a Quest
         // pledge uses for a settlement); giver.ReputationWithPlayer is the mod's OWN personal regard, the source
         // AssemblyInvitationFactsBuilder.RegardToInitiator reads too, exactly as this executor read it before.
         AppointGovernorVerdict verdict = CourtActionResolver.TryAppointGovernor(actorHero, giver.ReputationWithPlayer, entry.TargetHint);

         switch (verdict.Result)
         {
            case AppointGovernorResult.PlayerNotClanLeader:
            {
               const string reason = "the player no longer leads the clan.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AppointGovernorResult.NoSuchFief:
            {
               const string reason = "there is no fief of ours by that name left to govern.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AppointGovernorResult.AppointeeUnavailable:
            {
               // Unreachable on the council path: CouncilRosterResolver.EvaluateNow already proved the member
               // alive and free before dispatch (see Settle's own eligibility gate). Handled for completeness so
               // the shared core, which the 1:1 path relies on for this guard, stays honest.
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AppointGovernorResult.PostOccupied:
            {
               // v1.34.2 governance hardening (finding 1): the named fief already has a governor seated. Refuse
               // rather than silently evict them, and report it as a failed pledge, never a success.
               const string reason = "that fief already has a governor seated.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AppointGovernorResult.AppointeeAlreadyGoverns:
            {
               // v1.34.2 governance hardening (finding 2): one governor, one seat. A hero already governing another
               // fief cannot be double-posted, so this is a failed pledge, not a success.
               const string reason = "they already govern another fief.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AppointGovernorResult.AppointeeAway:
            {
               // v1.34.2 governance hardening (finding 2): a companion away on an errand cannot be seated in
               // absentia. Reported as a failed pledge so it never falls through to the success path.
               const string reason = "they are away on an errand.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AppointGovernorResult.Declined:
               ledger.MarkFailed(entry.Id, $"{actorName} declined the post");
               report?.Invoke($"{actorName} declines the council's offer to govern {verdict.Fief.Name}.", false);

               return;

            case AppointGovernorResult.Appointed:
               ledger.MarkApplied(entry.Id);
               report?.Invoke($"{actorName} now governs {verdict.Fief.Name}.", true);
               ModLog.Info($"[COUNCIL] lift: {actorName} appointed governor of {verdict.Fief.Name}.");

               return;
         }
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryAssignPartyRole), the
         // SAME one the 1:1 assign_party_role action runs (2026-08-05, the party-command batch), so a role behaves
         // identically whichever path granted it. Every guard this executor used to check inline (alive, still in
         // the player's own party, the role resolves) is re-checked there in the same order; this method only maps
         // the verdict back onto the council's own ledger + reporting. TargetHint carries the ROLE name for this
         // kind (the same single grounding slot AppointGovernor uses for a fief name).
         AssignPartyRoleVerdict verdict = CourtActionResolver.TryAssignPartyRole(actorHero, entry.TargetHint);

         switch (verdict.Result)
         {
            case AssignPartyRoleResult.ActorUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AssignPartyRoleResult.NotInPlayerParty:
            {
               const string reason = "they no longer ride in the player's own party.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AssignPartyRoleResult.NoValidRole:
            {
               const string reason = "no valid party role was named.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case AssignPartyRoleResult.Assigned:
               ledger.MarkApplied(entry.Id);
               report?.Invoke($"{actorName} now serves as your {verdict.Role}.", true);
               ModLog.Info($"[COUNCIL] lift: {actorName} assigned party role {verdict.Role}.");

               return;
         }
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryRejoinParty), the SAME
         // one the 1:1 rejoin_party action runs (2026-08-05, the party-command batch). Every guard this executor
         // used to check inline (alive, not a prisoner, still a companion, genuinely outside the main party, not
         // leading their own company) is re-checked there in the same order, and the governor-release side effect
         // (the inverse of SettleAppointGovernor's grant) happens there too; this method only maps the verdict back
         // onto the council's own ledger + reporting, and keeps the council's own release log line.
         RejoinPartyVerdict verdict = CourtActionResolver.TryRejoinParty(actorHero);

         switch (verdict.Result)
         {
            case RejoinPartyResult.ActorUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RejoinPartyResult.ActorImprisoned:
            {
               const string reason = "they are held prisoner and cannot travel.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RejoinPartyResult.NotACompanion:
            {
               const string reason = "they no longer belong to the player's clan as a companion.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RejoinPartyResult.AlreadyInParty:
            {
               const string reason = "they already ride in the player's own party.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RejoinPartyResult.LeadsOwnCompany:
            {
               const string reason = "they lead their own company; disband it first.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RejoinPartyResult.Rejoined:
               if (verdict.WasGovernor)
                  ModLog.Info($"[COUNCIL] lift: released {actorName} from governing {verdict.GovernedFief} so they could rejoin the party.");

               ledger.MarkApplied(entry.Id);
               report?.Invoke(verdict.WasGovernor
                  ? $"{actorName} steps down from governing {verdict.GovernedFief} and rejoins your party."
                  : $"{actorName} rejoins your party.", true);
               ModLog.Info($"[COUNCIL] lift: {actorName} rejoined the player's party.");

               return;
         }
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryDispatchMission), the
         // SAME one the 1:1 dispatch_mission action runs (2026-08-05, the party-command batch). Every guard this
         // executor used to check inline (alive, still a companion, still in the player's own party, a mission
         // system exists, not already away, the errand resolves) is re-checked there in the same order, and the
         // reused CompanionMissionBehavior.Dispatch call happens there too; this method only maps the verdict back
         // onto the council's own ledger + reporting. TargetHint carries the MISSION name for this kind.
         DispatchMissionVerdict verdict = CourtActionResolver.TryDispatchMission(actorHero, entry.TargetHint);

         switch (verdict.Result)
         {
            case DispatchMissionResult.ActorUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case DispatchMissionResult.NotACompanion:
            {
               const string reason = "they no longer belong to the player's clan as a companion.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case DispatchMissionResult.NotInPlayerParty:
            {
               const string reason = "they no longer ride in the player's own party.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case DispatchMissionResult.NoMissionSystem:
            {
               const string reason = "no mission system is available right now.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case DispatchMissionResult.AlreadyAway:
            {
               const string reason = "they are already away on an errand.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case DispatchMissionResult.NoValidMission:
            {
               const string reason = "no valid errand was named.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case DispatchMissionResult.DispatchFailed:
               ledger.MarkFailed(entry.Id, verdict.Message ?? "This could not be carried out.");
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {verdict.Message}", false);

               return;

            case DispatchMissionResult.Dispatched:
               ledger.MarkApplied(entry.Id);
               report?.Invoke(verdict.Message, true);
               ModLog.Info($"[COUNCIL] lift: {actorName} dispatched on a {verdict.Type} errand.");

               return;
         }
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryGrantStipend), the SAME
         // one the 1:1 grant_stipend action runs (2026-08-05), so a stipend behaves identically whichever path
         // granted it and neither can double-escrow a second concurrent tranche. Every guard this executor used to
         // check inline (alive, still a player companion, the escrow ledger exists, the amount clamps and funds
         // within the player's purse) is re-checked there in the same order, PLUS the new already-running guard; this
         // method only parses the proposed amount (TargetHint, this kind's own grounding slot) and maps the verdict
         // back onto the council's own ledger + reporting.
         if (!int.TryParse(entry.TargetHint, out int amount))
         {
            const string reason = "no valid stipend amount was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         GrantStipendVerdict verdict = CourtActionResolver.TryGrantStipend(actorHero, amount);

         switch (verdict.Result)
         {
            case GrantStipendResult.RecipientUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantStipendResult.NotPlayerCompanion:
            {
               const string reason = "they no longer belong to the player's clan as a companion.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantStipendResult.NoLedger:
            {
               const string reason = "no stipend ledger is available right now.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantStipendResult.AlreadyActive:
            {
               const string reason = "they already draw a stipend from your coffers.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantStipendResult.CouldNotFund:
               ledger.MarkFailed(entry.Id, verdict.FailReason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {verdict.FailReason}", false);

               return;

            case GrantStipendResult.Started:
               ledger.MarkApplied(entry.Id);
               report?.Invoke($"{actorName} will draw {verdict.ClampedAmountPerDay} denars a day from your coffers for the next {StipendPolicy.TrancheDays} days.", true);
               ModLog.Info($"[COUNCIL] lift: {actorName} granted a stipend of {verdict.ClampedAmountPerDay}/day for {StipendPolicy.TrancheDays} days.");

               return;
         }
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
         // R7-light (swear_oath): this give_gold resolution ALSO counts toward any pay_gold oath this same
         // member swore, whichever channel the gold actually moved through (this council pledge, or the 1:1
         // give_gold action).
         CommitmentBehavior.Instance?.OnGoldGivenToPlayer(actorHero.StringId, payable);

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
      ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "support_claimant"): the LAST catalogue kind, and the one
      ///   Kimi flagged as a genuine API spike (a live vanilla KingdomDecision/Election hook). SAFE v1 (Gabriel's
      ///   own instruction, autonomous build): rather than inject a vote into a live decision (a whole AI-driven
      ///   voting subsystem, KingdomDecision/Supporter/DecisionOutcome, confirmed by decompiling
      ///   TaleWorlds.CampaignSystem.Election.KingdomDecision - CampaignEvents.KingdomDecisionAdded DOES exist as
      ///   a clean hook, but there is no simple "spend influence for candidate X" call), this executor REUSES
      ///   SettleGiveInfluence's own machinery end to end: a seated WarCouncil vassal (a genuine OTHER clan of
      ///   the player's OWN kingdom) throws their house's political weight behind the PLAYER, moving REAL clan
      ///   influence from the backer's own house to the player's own clan, exactly like a give_influence pledge
      ///   but framed as a public declaration of support rather than a private favour. Every gate
      ///   AnySeatedMemberCanSupportClaimant already proves loosely at offer time is re-verified here against
      ///   CURRENT state (bridge is law): the backer still leads a clan distinct from the player's, that clan
      ///   still belongs to the SAME kingdom as the player's own (COUNCIL_ACTIONS.md's own design call: a
      ///   support pledge only makes sense within the player's own kingdom's politics, stricter than
      ///   SettleGiveInfluence's own bare "both hold some kingdom" check), and the backer's clan still holds
      ///   comfortably more influence than the computed backing costs
      ///   (CourtActionPolicy.CanAffordInfluenceGrant). The amount is NEVER an LLM choice (mirrors
      ///   SettleGiveInfluence's own doc): CourtActionPolicy.InfluenceGrant(relation) computes it fresh here,
      ///   scaled by the backer's own personal relation toward the player (zero or negative relation yields zero,
      ///   so a hostile vassal can never appear to back the player). DOCUMENTED FOLLOW-UP (v2, not built this
      ///   slice): spending this same influence directly inside a LIVE pending KingdomDecision when one exists
      ///   for the player's own claim, once the KingdomDecision/Supporter API is properly spiked - this v1 always
      ///   delivers the honest core (a real influence transfer) rather than a conditional promise tied to an
      ///   event that might never fire.
      /// </summary>
      private static void SettleSupportClaimant(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         // The gates and the amount are now the pure ClaimantSupportPolicy's job (Tier 4, Logic + self-test); this
         // executor keeps only the real influence move and the player-facing reason strings, in the SAME gate order.
         ClaimantSupportResult result = EvaluateSupportClaimant(actorHero);

         string reason = result.Outcome switch {
            ClaimantSupportOutcome.NotFound => "they could not be found.",
            ClaimantSupportOutcome.BackerIsPlayerHouse => "their own house is the player's own; there is no backing to lend.",
            ClaimantSupportOutcome.NotInPlayerKingdom => "their house holds no seat in your own kingdom's court.",
            ClaimantSupportOutcome.CannotAfford => "their house cannot spare the influence just now.",
            _ => null
         };

         if (reason != null)
         {
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ChangeClanInfluenceAction.Apply(actorHero.Clan, -result.Amount);
         ChangeClanInfluenceAction.Apply(Clan.PlayerClan, result.Amount);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} throws their house's weight behind you (+{result.Amount} influence).", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} publicly backed the player's claim with {result.Amount} influence.");
      }

      /// <summary>
      ///   Council Tier 4: gathers the live facts SettleSupportClaimant's gates need and runs the pure
      ///   <see cref="ClaimantSupportPolicy.Evaluate" /> against them, WITHOUT moving any influence. Internal seam
      ///   so a cr.testall self-test can drive the real fact-gathering + decision on a live backer non-destructively.
      /// </summary>
      internal static ClaimantSupportResult EvaluateSupportClaimant(Hero actorHero)
      {
         if (actorHero == null) return new ClaimantSupportResult(ClaimantSupportOutcome.NotFound, 0);

         Clan backer = actorHero.Clan;
         Clan playerClan = Clan.PlayerClan;
         bool inPlayerKingdom = backer?.Kingdom != null && playerClan?.Kingdom != null && backer.Kingdom == playerClan.Kingdom;

         return ClaimantSupportPolicy.Evaluate(
            actorHero.IsAlive, actorHero.IsLord, actorHero.IsPrisoner,
            backer != null, backer == playerClan, inPlayerKingdom,
            actorHero.GetRelation(Hero.MainHero), backer?.Influence ?? 0f);
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's give_item, "cadeau d'objet... comme un pot de vin, ou encore un signe
      ///   de generosite ou de recompense"): a seated LORD gives the PLAYER a named item from THEIR OWN PARTY's
      ///   stores, the REVERSE of the 1:1 give_item action (BannerlordGameStateBridge.ExecuteGiveItem, player's
      ///   party -> NPC). Resolves the "a hero has no item roster" murkiness the same way SettleLendTroops
      ///   already does: only a hero who LEADS a party has a real <see cref="MobileParty.ItemRoster" /> (actual
      ///   party stores) to give from, re-checked here rather than trusted from offer time (bridge is law). The
      ///   item NAME the LLM proposed (AssemblyResolutionEntry.TargetHint, this kind's own dedicated SDK field,
      ///   ResolutionProposal.TargetItemName -> "target_item") is resolved against the giver's LIVE roster,
      ///   case-insensitive, EXCLUDING quest items (<see cref="GiveItemPolicy.IsGiveableItem" />, mirroring
      ///   <see cref="CalradiaRemembers.Items.ItemPickerService" />'s own Value&gt;0 filter): a name that matches
      ///   nothing the giver genuinely holds right now is REFUSED outright, never invented or substituted.
      ///   Unlike SettleGiveGold, there is no amount to clamp: exactly ONE unit moves. No extra relation/
      ///   appreciation bump beyond the transfer itself (mirrors SettleGiveGold's/SettleGiveInfluence's own
      ///   discipline: the pledge's own value IS the effect) - the 1:1 give_item action's own
      ///   StanceService.OnGiftReceived/TryPlantValueAppreciation calls credit the OPPOSITE direction (the
      ///   PLAYER's own kindness toward the NPC), so reusing them here would wrongly reward the player for
      ///   merely receiving a gift; a documented follow-up if Gabriel later wants a giver-side "generous lord"
      ///   signal of their own.
      /// </summary>
      private static void SettleGiveItem(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         MobileParty party = actorHero.PartyBelongedTo;

         if (party == null || party.LeaderHero != actorHero || party.ItemRoster == null)
         {
            const string reason = "they lead no party of their own to draw a gift from.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (string.IsNullOrWhiteSpace(entry.TargetHint))
         {
            const string reason = "no item was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ItemRosterElement found = FindGiveableItem(party.ItemRoster, entry.TargetHint);

         if (found.EquipmentElement.Item == null)
         {
            string reason = $"{actorName} has no such item to give ('{entry.TargetHint}').";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (MobileParty.MainParty?.ItemRoster == null)
         {
            const string reason = "the player has nowhere to stow the gift.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         party.ItemRoster.AddToCounts(found.EquipmentElement, -1);
         MobileParty.MainParty.ItemRoster.AddToCounts(found.EquipmentElement, 1);

         var itemName = found.EquipmentElement.Item.Name.ToString();

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} gives you their {itemName}.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} gave the player '{itemName}' from their own party's stores.");
      }

      /// <summary>
      ///   Finds a real (non-quest, <see cref="GiveItemPolicy.IsGiveableItem" />) item in <paramref name="roster" />
      ///   by name, case-insensitive, mirroring BannerlordGameStateBridge.FindItemInRoster's own tolerant match
      ///   for the 1:1 give_item action, but over a SEATED LORD's own party roster rather than the player's.
      /// </summary>
      private static ItemRosterElement FindGiveableItem(ItemRoster roster, string itemName)
      {
         foreach (ItemRosterElement el in roster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;

            if (item == null || !GiveItemPolicy.IsGiveableItem(item.Value)) continue;

            if (string.Equals(item.Name.ToString(), itemName, StringComparison.OrdinalIgnoreCase)) return el;
         }

         return default;
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryGrantFief), the SAME one
         // the 1:1 grant_fief action runs (2026-08-05, the suzerain-command batch), so a fief grant behaves
         // identically whichever path granted it. Every guard this executor used to check inline (recipient alive
         // and free, a genuine OTHER house, the player still rules a kingdom, the fief is still a crown fief of ours),
         // the governor-release side effect, and the influence cost are all handled there in the same order; this
         // method only maps the verdict back onto the council's own ledger + reporting. TargetHint (m2) carries the
         // FIEF NAME for this kind, the same grounding slot appoint_governor's own pledge uses.
         GrantFiefVerdict verdict = CourtActionResolver.TryGrantFief(actorHero, entry.TargetHint);

         switch (verdict.Result)
         {
            case GrantFiefResult.RecipientUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantFiefResult.RecipientNotSeparateHouse:
            {
               const string reason = "they belong to the player's own house; there is no fief to grant across houses.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantFiefResult.RecipientNotVassal:
            {
               const string reason = "they are not a vassal of the player's kingdom; a crown fief passes only to a lord sworn to the realm.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantFiefResult.PlayerNotKingdomLeader:
            {
               const string reason = "the player no longer rules a kingdom to grant a crown fief from.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantFiefResult.NoSuchFief:
            {
               const string reason = "there is no crown fief of ours by that name left to grant.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case GrantFiefResult.Granted:
               ledger.MarkApplied(entry.Id);
               report?.Invoke($"{verdict.Fief.Name} passes to {actorName}'s house by the crown's own decision (-{FiefTransferPolicy.GrantInfluenceCost} influence).", true);
               ModLog.Info($"[COUNCIL] lift: {verdict.Fief.Name} granted to {actorName}'s clan by royal decision.");

               return;
         }
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryRevokeFief), the SAME one
         // the 1:1 revoke_fief action runs (2026-08-05, the suzerain-command batch). Every guard this executor used to
         // check inline (vassal alive, a genuine separate house, the player still rules a kingdom, the fief is still
         // one the spoliated clan holds) is re-checked there in the same order, and the grave side effects (the
         // MAXIMUM FiefRevoked grudge through the real gate, the relation hit) fire there too; this method only maps
         // the verdict back onto the council's own ledger + reporting. TargetHint (m2) carries the FIEF NAME, exactly
         // like grant_fief.
         //
         // TODO (deferred, unchanged by this extraction): a real probabilistic, delayed defection
         // (ChangeKingdomAction.ApplyByLeaveKingdom, weighted by the spoliated lord's standing, some days after the
         // confiscation) is a follow-up build, not hand-rolled here to avoid a fragile, unconditional exit that does
         // not match Gabriel's own "fort probable... dans les jours qui suivent" description.
         RevokeFiefVerdict verdict = CourtActionResolver.TryRevokeFief(actorHero, entry.TargetHint, gameDay);

         switch (verdict.Result)
         {
            case RevokeFiefResult.VassalUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RevokeFiefResult.VassalNotSeparateHouse:
            {
               const string reason = "they hold no separate house whose fief could be revoked.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RevokeFiefResult.VassalNotInKingdom:
            {
               const string reason = "they are not a vassal of the player's kingdom; a fief outside the realm cannot be revoked in its name.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RevokeFiefResult.PlayerNotKingdomLeader:
            {
               const string reason = "the player no longer rules a kingdom to revoke a fief in its name.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RevokeFiefResult.NoSuchFief:
            {
               string reason = $"{actorName} holds no fief of that name to revoke.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case RevokeFiefResult.Revoked:
               ledger.MarkApplied(entry.Id);
               report?.Invoke($"{verdict.Fief.Name} is stripped from {actorName} and returns to the crown.", true);
               ModLog.Info($"[COUNCIL] lift: {actorName} was stripped of {verdict.Fief.Name} by royal decision; a grudge is planted.");

               return;
         }
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
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TryExpelFromClan), the SAME
         // one the 1:1 expel_from_clan action runs (2026-08-05, the suzerain-command batch). Every guard this executor
         // used to check inline (companion alive, the ExpulsionPolicy gate, not away on an errand) is re-checked there
         // in the same order, and the side effects (ApplyByFire making them a Fugitive, the Expelled grudge through
         // the real gate, the relation hit) fire there too; this method only maps the verdict back onto the council's
         // own ledger + reporting.
         ExpelFromClanVerdict verdict = CourtActionResolver.TryExpelFromClan(actorHero, gameDay);

         switch (verdict.Result)
         {
            case ExpelFromClanResult.ActorUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case ExpelFromClanResult.PlayerNotClanLeader:
            {
               const string reason = "the player no longer leads the clan.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case ExpelFromClanResult.NotACompanion:
            {
               const string reason = "they no longer belong to the player's clan as a companion.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case ExpelFromClanResult.IsSpouse:
            {
               const string reason = "they are the player's own spouse; that bond can only be ended another way.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case ExpelFromClanResult.AwayOnErrand:
            {
               const string reason = "they are away on an errand; recall them before casting them out.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

               return;
            }

            case ExpelFromClanResult.Expelled:
               ledger.MarkApplied(entry.Id);
               report?.Invoke(verdict.WasGovernor
                  ? $"{actorName} is stripped of the governorship of {verdict.GovernedFief} and cast out of your clan."
                  : $"{actorName} is cast out of your clan.", true);
               ModLog.Info($"[COUNCIL] lift: {actorName} was expelled from the player's clan; a grudge is planted.");

               return;
         }
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
            case ArrangeMarriageResult.BlockedByGuard: return "the two named cannot be wed now: not across a war, nor from captivity.";
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
      ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "swap_fiefs"): the player and a seated WarCouncil vassal
      ///   ATOMICALLY EXCHANGE one fief each, REUSING grant_fief's and revoke_fief's own machinery end to end
      ///   (FiefTransferPolicy.CanSwap, the literal AND of CanGrant's and CanRevoke's own two conditions;
      ///   <see cref="ChangeOwnerOfSettlementAction.ApplyByKingDecision" />, the same detail grant_fief/
      ///   revoke_fief already apply). <paramref name="actorHero" /> IS the seated vassal on the OTHER side of
      ///   the trade (mirroring SettleGrantFief's/SettleRevokeFief's own beneficiary/spoliated-lord role).
      ///   <see cref="AssemblyResolutionEntry.TargetHint" /> carries BOTH fief names composed together
      ///   (<see cref="FiefSwapGroundingCodec" />, mirroring <see cref="ArrangeMarriageGroundingCodec" />'s own
      ///   two-name grounding, since a single TargetHint slot cannot otherwise hold two names).
      ///   ATOMIC BY CONSTRUCTION (the whole stake of this executor): every guard for BOTH fiefs (ownership,
      ///   siege) is checked to completion BEFORE either <see cref="ChangeOwnerOfSettlementAction.ApplyByKingDecision" />
      ///   call is made; if any single guard fails, the method returns having applied NEITHER transfer, never
      ///   one side of the trade left half-done. A besieged fief on EITHER side refuses the whole swap (Design
      ///   call, this build: an active siege makes ownership itself provisional, an unsafe moment for the crown
      ///   to move either parcel of land). A garrison FOLLOWS its fief on a transfer (vanilla's own
      ///   ChangeOwnerOfSettlementAction default), exactly like grant_fief/revoke_fief already inherit; a
      ///   governor in post on either side is released first, mirroring SettleGrantFief's own governor-release
      ///   (a released PLAYER companion is sent back to the main party rather than left stranded in what is now
      ///   foreign land).
      /// </summary>
      private static void SettleSwapFiefs(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
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
            const string reason = "they belong to the player's own house; there is no cross-clan swap to make.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!FiefSwapGroundingCodec.TryDecompose(entry.TargetHint, out string playerFiefName, out string targetFiefName))
         {
            const string reason = "no two fiefs were named plainly enough to swap.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;

         // TargetHint (composed above) names the PLAYER's own fief on the left, the VASSAL's own fief on the
         // right (RecordCouncilResolution's own composing order, see that method's doc). Each is resolved
         // against the SPECIFIC clan the swap needs it to belong to right now, exactly like grant_fief resolves
         // against Clan.PlayerClan and revoke_fief resolves against the spoliated clan: naming a fief neither
         // side currently holds must refuse rather than conjure a transfer from nothing.
         Settlement playerFief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == Clan.PlayerClan
            && string.Equals(s.Name?.ToString(), playerFiefName, StringComparison.OrdinalIgnoreCase));

         Settlement vassalFief = Settlement.All?.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle)
            && s.OwnerClan == actorHero.Clan
            && string.Equals(s.Name?.ToString(), targetFiefName, StringComparison.OrdinalIgnoreCase));

         if (!FiefTransferPolicy.CanSwap(playerIsKingdomLeader, playerFief != null, vassalFief != null))
         {
            string reason = !playerIsKingdomLeader
               ? "the player no longer rules a kingdom to broker a fief swap in its name."
               : playerFief == null
                  ? "there is no fief of ours by that name left to offer."
                  : $"{actorName} holds no fief of that name to offer in exchange.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // Design call, this build (COUNCIL_ACTIONS.md Partie 8 does not pin a siege rule for swap_fiefs
         // specifically): a fief under active siege has provisional ownership, an unsafe moment for the crown
         // to move EITHER parcel of land; refuse the whole swap rather than complete half of it around a fight
         // still in progress. Checked for BOTH sides before either transfer is even considered, the same
         // validate-everything-first discipline the whole method follows.
         if (playerFief.SiegeEvent != null || vassalFief.SiegeEvent != null)
         {
            const string reason = "one of the two fiefs is under siege and cannot change hands right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         // Every guard above has now passed for BOTH fiefs: only past this point does either transfer actually
         // apply, so a failure anywhere above this line has left the world completely untouched (the atomicity
         // this whole executor exists to guarantee).
         Hero formerPlayerFiefGovernor = playerFief.Town?.Governor;
         if (formerPlayerFiefGovernor != null)
         {
            ChangeGovernorAction.RemoveGovernorOf(formerPlayerFiefGovernor);

            if (formerPlayerFiefGovernor.IsPlayerCompanion && formerPlayerFiefGovernor.PartyBelongedTo != MobileParty.MainParty)
               AddHeroToPartyAction.Apply(formerPlayerFiefGovernor, MobileParty.MainParty, false);
         }

         Hero formerVassalFiefGovernor = vassalFief.Town?.Governor;
         if (formerVassalFiefGovernor != null)
            ChangeGovernorAction.RemoveGovernorOf(formerVassalFiefGovernor);

         ChangeOwnerOfSettlementAction.ApplyByKingDecision(actorHero, playerFief);
         ChangeOwnerOfSettlementAction.ApplyByKingDecision(Hero.MainHero, vassalFief);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{playerFief.Name} passes to {actorName}'s house, and {vassalFief.Name} passes to yours: an even exchange sealed by the crown.", true);
         ModLog.Info($"[COUNCIL] lift: swap sealed, {playerFief.Name} to {actorName}'s clan, {vassalFief.Name} to the player's clan.");
      }

      /// <summary>
      ///   Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): the Parley's own submission/buy-off motion. The
      ///   SEATED ENEMY leader (actorHero, the parley's own counterpart) pledges a recurring DAILY payment out of
      ///   their own coffers, PAY-AS-YOU-GO rather than escrowed (TributePolicy, CouncilTributeBehavior): unlike
      ///   SettleGrantStipend's whole-tranche fund-up-front, nothing is charged here, only started. Guards
      ///   mirror SettleGiveInfluence's/SettleGrantFief's own cross-clan check (the payer must lead a house
      ///   distinct from the player's own, or a tribute paid to oneself is meaningless), NOT the ordinary
      ///   IsPlayerCompanion check every Companions-scoped executor above uses (a Parley counterpart is an enemy
      ///   lord, never a companion). Deliberately does NOT require the two factions still being at war: peace
      ///   may have just been struck in this SAME parley turn (make_peace and tribute may both be pledged at
      ///   once), and a tribute is exactly the kind of submission that accompanies a peace, not only a war.
      /// </summary>
      private static void SettleTribute(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
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
            const string reason = "they lead no house distinct from the player's own to pay tribute from.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!int.TryParse(entry.TargetHint, out int amount))
         {
            const string reason = "no valid tribute amount was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CouncilTributeBehavior.Instance == null)
         {
            const string reason = "no tribute ledger is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!CouncilTributeBehavior.Instance.TryStart(actorHero, amount, out string failReason))
         {
            ledger.MarkFailed(entry.Id, failReason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failReason}", false);

            return;
         }

         int clamped = TributePolicy.ClampAmountPerDay(amount);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} will pay you {clamped} denars a day for the next {TributePolicy.TributeDays} days.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} pledged a tribute of {clamped}/day for {TributePolicy.TributeDays} days.");
      }

      /// <summary>
      ///   Kimi's "v1 invite d'honneur" (COUNCIL_ACTIONS.md's Partie 8, "give_hostage"), rounding out the Parley
      ///   toolkit's FOURTH concession alongside MakePeace, Tribute, and ReleasePrisoner: the SEATED ENEMY leader
      ///   (actorHero, the parley's own counterpart) gives an eligible relative of their own
      ///   (<see cref="HostageEligibilityPolicy" />, re-resolved by name here via
      ///   <see cref="HostageCandidateResolver.ResolveByName" /> against actorHero's OWN eligible pool, bridge is
      ///   law) to reside at the player's own court as a living guarantee, for a fixed term
      ///   (<see cref="HostagePolicy.TermDays" />). Mirrors SettleTribute's own cross-clan guard (the pledger
      ///   must lead a house distinct from the player's own): a Parley counterpart is always an enemy lord,
      ///   never a companion, so this never uses the ordinary IsPlayerCompanion check other executors read.
      /// </summary>
      private static void SettleGiveHostage(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
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
            const string reason = "they lead no house distinct from the player's own to give a kinsman from.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         Hero hostage = HostageCandidateResolver.ResolveByName(entry.TargetHint, actorHero);

         if (hostage == null)
         {
            const string reason = "no eligible kinsman by that name is known to give as a hostage.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CouncilHostageBehavior.Instance == null)
         {
            const string reason = "no hostage ledger is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!CouncilHostageBehavior.Instance.TryStart(actorHero, hostage, out string failReason))
         {
            ledger.MarkFailed(entry.Id, failReason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failReason}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} gives you their kin {hostage.Name} as a hostage of good faith, for {HostagePolicy.TermDays} days.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} gave {hostage.Name} as a hostage of good faith for {HostagePolicy.TermDays} days.");
      }

      /// <summary>
      ///   Rounds out the Parley toolkit (make_peace + tribute + release_prisoner, 2026-08-04): as a concession
      ///   in THIS SAME negotiation, the PLAYER releases an enemy lord they currently hold prisoner back to
      ///   freedom, REUSING the existing 1:1 free_prisoner mechanic end to end
      ///   (<see cref="EndCaptivityAction.ApplyByReleasedByChoice" />, the SAME call
      ///   BannerlordGameStateBridge.ExecuteFreePrisoner and the 1:1 release_prisoner verb both already use)
      ///   plus the SAME +relation bump the 1:1 release_prisoner verb already gives a freed captive toward the
      ///   player (<see cref="ChangeRelationAction.ApplyRelationChangeBetweenHeroes" />,
      ///   <see cref="CourtActionPolicy.ReleasePrisonerRelationBump" />). <see cref="AssemblyResolutionEntry.TargetHint" />
      ///   carries the CAPTIVE'S OWN NAME (this kind's own grounding, see RecordCouncilResolution's doc),
      ///   resolved among the player's own held hero prisoners by
      ///   <see cref="BuyPrisonerVerb.ResolvePlayerHeldPrisonerByName" /> (the SAME player-custody pool the
      ///   prisoner-trade verbs already read, so "still held" is proven by the lookup itself finding them at
      ///   all). Guards re-checked here rather than trusted from proposal/offer time (bridge is law): the named
      ///   prisoner must still belong to the SAME faction the parley was convened against
      ///   (<paramref name="parleyTargetFactionId" />, exactly like <see cref="SettleMakePeace" />'s own
      ///   preferred source), so this only ever frees a genuine concession to THIS counterpart, never an
      ///   unrelated captive of some third house. A named prisoner who is not held, or belongs to a different
      ///   faction, is surfaced honestly rather than silently doing nothing, the same discipline every executor
      ///   above follows.
      /// </summary>
      private static void SettleReleasePrisoner(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report, string parleyTargetFactionId)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         Hero prisoner = BuyPrisonerVerb.ResolvePlayerHeldPrisonerByName(entry.TargetHint);

         if (prisoner == null)
         {
            const string reason = "you hold no such captive to free.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         IFaction enemyFaction = ParleyFactionLookup.ByStringId(parleyTargetFactionId);

         if (enemyFaction == null || prisoner.MapFaction == null || prisoner.MapFaction != enemyFaction)
         {
            string reason = $"{prisoner.Name} does not belong to {actorName}'s own faction; freeing them would be no concession to this table.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         string prisonerName = prisoner.Name?.ToString() ?? "the captive";

         EndCaptivityAction.ApplyByReleasedByChoice(prisoner, Hero.MainHero);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
            prisoner, Hero.MainHero, CourtActionPolicy.ReleasePrisonerRelationBump, false);

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"You free {prisonerName}, returning them to {actorName}.", true);
         ModLog.Info($"[COUNCIL] lift: released {prisonerName} from captivity as a parley concession to {actorName}.");
      }

      /// <summary>
      ///   Rounds out the Parley toolkit with a FIFTH concession (COUNCIL_ACTIONS.md's Partie 8,
      ///   "non_aggression_pact"): the player's own FACTION and the parley's own enemy FACTION agree not to raise
      ///   arms against one another for a FIXED term (<see cref="NonAggressionPactPolicy.TermDays" />).
      ///   HONEST-LIMITED BY DESIGN (see <see cref="NonAggressionPactPolicy" />'s own header, Kimi's warning):
      ///   this binds ONLY the mod's own hostile-quest emission and a detected/sanctioned breach, never vanilla
      ///   AI behaviour or a kingdom's own war council. Not FACTION-CENTRIC (see <see cref="ResolutionCatalog.IsFactionCentric" />),
      ///   so <paramref name="actorHero" /> has already passed the ordinary seated-member re-validation (the
      ///   seated enemy leader must still be present); THIS method resolves the actual FACTION pair, mirroring
      ///   <see cref="SettleReleasePrisoner" />'s own preference for <paramref name="parleyTargetFactionId" /> (the
      ///   parley's own stored target) over re-deriving one from prose. Guards: the player must still lead their
      ///   own faction, the parley's target must still resolve to a live faction, and the two must not be the
      ///   same (nothing else - deliberately NOT a war-status check, mirroring SettleTribute's own documented
      ///   choice: a pact may be struck in the SAME turn as make_peace, or stand on its own while a standing war
      ///   continues, see NonAggressionPactPolicy's own header).
      /// </summary>
      private static void SettleNonAggressionPact(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report, string parleyTargetFactionId)
      {
         if (Hero.MainHero is not {IsFactionLeader: true})
         {
            const string reason = "the player no longer leads a faction that can strike this pact.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         IFaction playerFaction = Hero.MainHero.MapFaction;
         IFaction enemyFaction = ParleyFactionLookup.ByStringId(parleyTargetFactionId);

         if (playerFaction == null || enemyFaction == null)
         {
            const string reason = "no faction by that name could be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CouncilPactBehavior.Instance == null)
         {
            const string reason = "no pact ledger is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!CouncilPactBehavior.Instance.TryStart(playerFaction, enemyFaction, out string failReason))
         {
            ledger.MarkFailed(entry.Id, failReason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failReason}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"Your companies and {enemyFaction.Name}'s will not attack each other for {NonAggressionPactPolicy.TermDays} days.", true);
         ModLog.Info($"[COUNCIL] lift: non-aggression pact struck between {playerFaction.Name} and {enemyFaction.Name} for {NonAggressionPactPolicy.TermDays} days.");
      }

      /// <summary>
      ///   Kimi's design, MINIMAL v1 (COUNCIL_ACTIONS.md's Partie 8, "sponsor_ward"): the SEATED MENTOR (actorHero,
      ///   a ClanLords or Family member) takes a named young companion of their own clan under their wing as a
      ///   protege, re-resolved by name here via <see cref="WardCandidateResolver.ResolveByName" /> against
      ///   actorHero's OWN eligible pool (bridge is law). Mirrors SettlePledgeAgainst's own actor gate (a genuine
      ///   Family/ClanLords seat is always a Hero.IsLord member of the player's own clan), unlike
      ///   SettleTribute's/SettleGiveHostage's own cross-clan guard: sponsor_ward's mentor is ALWAYS of the
      ///   player's own house, never a foreign leader, so this checks Clan.PlayerClan directly instead.
      /// </summary>
      private static void SettleSponsorWard(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.Clan != Clan.PlayerClan)
         {
            const string reason = "they no longer belong to the player's own clan to sponsor a ward from.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         Hero protege = WardCandidateResolver.ResolveByName(entry.TargetHint, actorHero);

         if (protege == null)
         {
            const string reason = "no eligible young companion by that name is known to take on as a ward.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (CouncilWardBehavior.Instance == null)
         {
            const string reason = "no ward ledger is available right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!CouncilWardBehavior.Instance.TryStart(actorHero, protege, out string failReason))
         {
            ledger.MarkFailed(entry.Id, failReason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failReason}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"{actorName} takes {protege.Name} under their wing as a protege.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName} took {protege.Name} under their wing as a protege.");
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's lend_troops, Gabriel's own CONDITIONS, the highest-detail item in the
      ///   spec): a seated lord commits real soldiers from their own party to the player. No LLM-proposed amount
      ///   to clamp (entry.TargetHint carries no grounding for this kind, mirrors GiveInfluence's own doc): the
      ///   tier split is computed HERE, fresh, from the donor's LIVE roster (<see cref="TroopTierBucketer.BucketHealthyTroops" />
      ///   -> <see cref="CouncilTroopLoanPolicy.ComputeLoan" />), never trusted from offer time. Guards re-checked here
      ///   (bridge is law): alive, leads their own party, not at war with the player (Gabriel's condition D). The
      ///   actual deed is NOT instantaneous like every other Partie 1 kind above: Gabriel's condition A ("il
      ///   envoie une delegation") makes this an ETALED executor - <see cref="CouncilTroopDeliveryBehavior.TryStart" />
      ///   either sets a real delegation party in motion (reported "on the way", Applied only once it actually
      ///   arrives - the entry itself is marked Applied here because the PROMISE was kept the moment the
      ///   delegation set out with the troops already aboard, mirroring how SettleDispatchMission marks a
      ///   companion's errand Applied at departure, not at return) or falls back to an instant transfer (this
      ///   build's own documented deviation, see CouncilTroopDeliveryBehavior's own header) when no minor hero of
      ///   the donor's own house was free to lead one.
      /// </summary>
      private static void SettleLendTroops(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         MobileParty donorParty = actorHero.PartyBelongedTo;

         if (donorParty == null || donorParty.LeaderHero != actorHero)
         {
            const string reason = "they no longer lead a party of their own.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.MapFaction != null && Hero.MainHero?.MapFaction != null &&
             FactionManager.IsAtWarAgainstFaction(actorHero.MapFaction, Hero.MainHero.MapFaction))
         {
            const string reason = "your two houses are at war; they will not send you soldiers now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         TierTroopCounts partyCounts = TroopTierBucketer.BucketHealthyTroops(donorParty.MemberRoster);
         TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(partyCounts);

         if (loan.Total <= 0 || CouncilTroopDeliveryBehavior.Instance == null)
         {
            const string reason = "they have no troops left to spare right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         TroopDeliveryOutcome outcome = CouncilTroopDeliveryBehavior.Instance.TryStart(actorHero, loan, out TierTroopCounts moved, out string failReason);

         if (outcome == TroopDeliveryOutcome.Failed)
         {
            ledger.MarkFailed(entry.Id, failReason ?? "the pledge could not be carried out.");
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failReason}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);

         if (outcome == TroopDeliveryOutcome.Delegation)
            report?.Invoke($"{actorName} sends {moved.Total} soldiers to you; a delegation is already on the road and should reach you before long.", true);
         else
            // Instant fallback (this build's own documented deviation): honest about there being no delegation to
            // watch for, never a silent substitution of the promise it actually kept.
            report?.Invoke($"{actorName} sends {moved.Total} soldiers to you; word reaches you that riders slipped through in the night and they are already among your ranks.", true);

         ModLog.Info($"[COUNCIL] lift: {actorName} lent the player {moved.Total} troops ({moved.Low}/{moved.Medium}/{moved.High}), outcome {outcome}.");
      }

      /// <summary>
      ///   Partie 1 (COUNCIL_ACTIONS.md's give_troops, the reciprocal of lend_troops): the PLAYER reinforces a
      ///   seated lord with real soldiers from the PLAYER's OWN party. Gabriel's own written condition
      ///   (COUNCIL_ACTIONS.md, Partie 1 give_troops) asks that the player pick the troops through the native
      ///   party-transfer SCREEN; this v1 build documents that as a FOLLOW-UP rather than building it (reopening
      ///   the screen at the lift, after the council window has already closed, is a fiddly UX sequencing
      ///   problem, not a one-line change) and instead reuses <see cref="CouncilTroopLoanPolicy.ComputeLoan" />
      ///   on the PLAYER's own LIVE roster, exactly the same pure 25%-cap tier-mixed math
      ///   <see cref="SettleLendTroops" /> already uses for the opposite direction, never a re-tuning of it.
      ///   Unlike SettleLendTroops, this is INSTANT: the player's own troops already stand at the council table,
      ///   so no travelling delegation is needed for this direction, and the SAME <see cref="TroopTierBucketer.MoveLoan" />
      ///   transfer helper LendTroops uses is reused here too, only with the donor/recipient parties swapped
      ///   (MainParty -> the recipient's party). Guards re-checked here (bridge is law): the recipient is alive,
      ///   leads a real party of their own with room to receive more, is not the player's own party (a no-op),
      ///   and the two houses are not at war. No separate absolute floor is added on the player's own side beyond
      ///   what ComputeLoan's own 25% cap already guarantees: capping the gift at a quarter of the PLAYER's own
      ///   healthy troops means at least 75% always remains, comfortably above the 1:1 give_troops verb's own
      ///   70% retain floor (CourtActionPolicy.GiveTroopsPlayerMinRetainFraction) - a documented v1 design call,
      ///   not an oversight, mirroring how LendTroops' own CouncilTroopLoanPolicy already supersedes the 1:1
      ///   verb's separate tuning rather than reusing it.
      /// </summary>
      private static void SettleGiveTroops(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, Action<string, bool> report)
      {
         if (actorHero is not {IsAlive: true} || actorHero.IsPrisoner)
         {
            const string reason = "they could not be found.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         MobileParty recipientParty = actorHero.PartyBelongedTo;

         if (recipientParty == null || recipientParty.LeaderHero != actorHero)
         {
            const string reason = "they no longer lead a party of their own.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         MobileParty playerParty = MobileParty.MainParty;

         if (playerParty?.MemberRoster == null)
         {
            const string reason = "you have no party of your own right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"Your pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (recipientParty == playerParty)
         {
            const string reason = "that would just be your own troops changing hands with themselves.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"Your pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (actorHero.MapFaction != null && Hero.MainHero?.MapFaction != null &&
             FactionManager.IsAtWarAgainstFaction(actorHero.MapFaction, Hero.MainHero.MapFaction))
         {
            const string reason = "your two houses are at war; you will not reinforce them now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (recipientParty.MemberRoster.TotalManCount >= recipientParty.Party.PartySizeLimit)
         {
            const string reason = "their party has no room left for more troops.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         TierTroopCounts playerCounts = TroopTierBucketer.BucketHealthyTroops(playerParty.MemberRoster);
         TierTroopCounts gift = CouncilTroopLoanPolicy.ComputeLoan(playerCounts);

         if (gift.Total <= 0)
         {
            const string reason = "you have no troops to spare right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"Your pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         TierTroopCounts moved = TroopTierBucketer.MoveLoan(playerParty, recipientParty, gift);

         if (moved.Total <= 0)
         {
            const string reason = "you had no troops left to spare right now.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"Your pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         ledger.MarkApplied(entry.Id);
         report?.Invoke($"You reinforce {actorName} with {moved.Total} troops.", true);
         ModLog.Info($"[COUNCIL] lift: player gave {actorName} {moved.Total} troops ({moved.Low}/{moved.Medium}/{moved.High}).");
      }

      /// <summary>
      ///   R7-light (COUNCIL_ACTIONS.md's Partie 8, "swear_oath"): a seated member swears one of the THREE
      ///   whitelisted, auto-verifiable oath kinds (pay_gold/keep_peace/protect, <see cref="OathKindParser" />),
      ///   tracked from here on by the SAME generalized <see cref="TrackedCommitment" />/<see cref="CommitmentBehavior" />
      ///   engine pledge_against already uses (one active commitment per member, <see cref="CommitmentBehavior.HasActive" />,
      ///   shared across EVERY kind, so a member cannot swear a second oath, or a pledge_against, while one is
      ///   already outstanding). Grounding is decomposed from <see cref="AssemblyResolutionEntry.TargetHint" />
      ///   by <see cref="OathGroundingCodec" /> (WHICH kind, plus that kind's own target value: a denars figure
      ///   for pay_gold, a faction name for keep_peace, nothing for protect). Every guard is re-checked here
      ///   against LIVE state (bridge is law), never trusted from the moment the oath was proposed: an
      ///   unverifiable or invalid oath (an unrecognised kind, no valid sum, a swearer who leads no faction of
      ///   their own for keep_peace) is REFUSED outright rather than tracked and silently doomed to breach.
      ///   Success carries <see cref="AssemblyResolutionEntry.WitnessIds" /> onto the new commitment (the OTHER
      ///   seats present when this was sworn), so a later broken oath costs more than one sworn in private would
      ///   (CommitmentBehavior's own witness-cost penalty and BrokenOath grudge).
      /// </summary>
      private static void SettleSwearOath(AssemblyLedger ledger, AssemblyResolutionEntry entry, Hero actorHero, string actorName, int gameDay, Action<string, bool> report)
      {
         // The whole decision+mutation now lives in the shared core (CourtActionResolver.TrySwearOath), the SAME one
         // the 1:1 swear_oath action runs (2026-08-05), so an oath behaves identically whichever path recorded it.
         // Every guard this executor used to check inline (alive/free/not-the-player, the commitment ledger exists,
         // no oath already outstanding, the kind resolves through the whitelist, and that kind's own payload
         // validates) is re-checked there in the same order; this method only decomposes the grounding
         // (OathGroundingCodec: WHICH kind plus that kind's own target value) and maps the verdict back onto the
         // council's own ledger + reporting, carrying the WitnessIds (the other seats present) onto the oath so a
         // later breach costs more than one sworn in private.
         if (!OathGroundingCodec.TryDecompose(entry.TargetHint, out string oathKindKey, out string targetValue))
         {
            const string reason = "no oath kind was named.";
            ledger.MarkFailed(entry.Id, reason);
            report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

            return;
         }

         SwearOathVerdict verdict = CourtActionResolver.TrySwearOath(actorHero, oathKindKey, targetValue, gameDay, entry.WitnessIds);

         switch (verdict.Result)
         {
            case SwearOathResult.SwearerUnavailable:
            {
               const string reason = "they could not be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.NoLedger:
            {
               const string reason = "no commitment ledger is available right now.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.AlreadyCommitted:
            {
               const string reason = "they already have a pledge outstanding, one vow at a time.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.UnknownKind:
            {
               // R7.2 (COUNCIL_ACTIONS.md): a kind not in the whitelist (e.g. an attempted "move_against", which is
               // pledge_against's own separate resolution) is refused rather than tracked, never rewritten as
               // whatever it superficially resembles.
               const string reason = "that is not an oath the table can hold anyone to.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.InvalidAmount:
            {
               const string reason = "no valid sum was named.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.CannotSwearPeace:
            {
               const string reason = "they do not lead a faction of their own to swear peace for.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.NoSuchFaction:
            {
               const string reason = "no faction by that name could be found.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.OwnFaction:
            {
               const string reason = "that is their own house.";
               ledger.MarkFailed(entry.Id, reason);
               report?.Invoke($"{actorName}'s oath ({entry.Detail}) could not be sworn: {reason}", false);

               return;
            }

            case SwearOathResult.Sworn:
               string swornLine;

               switch (verdict.Kind)
               {
                  case CommitmentKind.PayGold:
                     swornLine = $"{actorName} swears to pay you {verdict.PromisedAmount} denars within {CommitmentPolicy.DeadlineDays} days.";

                     break;

                  case CommitmentKind.KeepPeace:
                     swornLine = $"{actorName} swears to keep the peace with {verdict.TargetFactionName} for {CommitmentPolicy.DeadlineDays} days.";

                     break;

                  default:
                     swornLine = $"{actorName} swears to stand at your side in battle within {CommitmentPolicy.DeadlineDays} days.";

                     break;
               }

               ledger.MarkApplied(entry.Id);
               report?.Invoke(swornLine, true);
               ModLog.Info($"[COUNCIL] lift: {actorName} swore a {verdict.Kind} oath (witnesses={entry.WitnessIds?.Count ?? 0}).");

               return;
         }
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

      /// <summary>
      ///   Every faction named in <paramref name="playerMessage" />, scanned over the SAME pool
      ///   <see cref="ResolveFactionByName" /> draws from (live Kingdoms and independent Clans): a substring
      ///   containment test suffices here (unlike a hero or settlement's multi-part name) because a faction's
      ///   name is a single distinctive proper noun. Exposed for <c>ExternalVerbAdapter</c>'s pre-response
      ///   entity resolution (Extension Surface actions guidance). Never null; empty when nothing matches.
      /// </summary>
      internal static IReadOnlyList<IFaction> ResolveFactionsMentioned(string playerMessage)
      {
         var result = new List<IFaction>();

         if (string.IsNullOrWhiteSpace(playerMessage)) return result;

         foreach (Kingdom kingdom in Kingdom.All ?? Enumerable.Empty<Kingdom>())
         {
            string name = kingdom?.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(name) && playerMessage.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
               result.Add(kingdom);
         }

         foreach (Clan clan in Clan.All ?? Enumerable.Empty<Clan>())
         {
            if (clan?.Kingdom != null) continue;

            string name = clan?.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(name) && playerMessage.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
               result.Add(clan);
         }

         return result;
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
