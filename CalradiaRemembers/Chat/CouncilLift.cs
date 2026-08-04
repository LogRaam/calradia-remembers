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
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
      ///   caller may pass a banner reporter, or null to stay silent.
      /// </summary>
      public static void Finalize(AssemblyLedger ledger, CouncilScope scope, NpcMemoryCampaignBehavior behavior, int gameDay, Action<string, bool> report)
      {
         if (ledger == null || behavior?.Store == null) return;

         List<AssemblyResolutionEntry> pending = ledger.PendingCommit().ToList();

         if (pending.Count == 0) return;

         foreach (AssemblyResolutionEntry entry in pending)
         {
            try
            {
               Settle(ledger, entry, scope, behavior, gameDay, report);
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

      private static void Settle(AssemblyLedger ledger, AssemblyResolutionEntry entry, CouncilScope scope, NpcMemoryCampaignBehavior behavior, int gameDay, Action<string, bool> report)
      {
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
         // yet, Q5 "extend then migrate") is reported honestly instead of silently applied or dropped. Quest and
         // AppointGovernor are executable today, so a passing entry here is one of those two; the switch below
         // this gate is the per-kind dispatch, extended the day a third executor is wired.
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

         // Council audit M4 -> B2: the executable gate above proves kind is Quest or AppointGovernor today;
         // this switch is the per-kind dispatch that plug point replaces. Each case owns its own guards and
         // reporting so a new executor never has to thread through another kind's assumptions.
         switch (kind)
         {
            case ResolutionKind.AppointGovernor:
               SettleAppointGovernor(ledger, entry, actorHero, actorName, giver, report);

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
