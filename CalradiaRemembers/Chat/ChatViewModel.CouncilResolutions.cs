// Code written by Gabriel Mailhot, 21/07/2026.
// The council's mechanical afterlife. PromptBuilder's council section no longer only forbids ("NOTHING IS
// SEALED AT THIS TABLE"): it now teaches a positive [RESOLUTION] channel for a real commitment. This file is
// the other half of that contract: it records or withdraws what the table decides during the session (every
// resolution is provisional and reversible, nothing touches the world yet, see ResolutionLifecycle), and, at
// the LIFT (ChatManager.Close -> FinalizeCouncilResolutions), re-validates each surviving resolution against
// the world AS IT STANDS THEN and only now executes it. A resolution that fails re-validation is reported to
// the player in plain language, never silently dropped: the exact lesson of the marriage-claim bug this
// codebase already guards against elsewhere: a promise recorded but never honoured, and never explained.

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

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      #region private

      /// <summary>
      ///   Records or withdraws every [RESOLUTION] block this turn against the seated roster. A no-op outside
      ///   a council (<see cref="_councilScope" /> null), so an ordinary conversation (which the prompt never
      ///   teaches this block on anyway) is never touched. Called from the main turn path right after
      ///   <see cref="QuestService.ApplyResponse" />, the same place an ordinary [QUEST] is recorded.
      /// </summary>
      private void ProcessCouncilResolutions(ParsedResponse response)
      {
         if (_councilScope == null) return;
         if (response?.Resolutions == null || response.Resolutions.Count == 0) return;

         (List<string> names, List<string> heroIds) = SeatedMembers();

         foreach (ResolutionProposal proposal in response.Resolutions)
         {
            if (string.IsNullOrWhiteSpace(proposal.Type)) continue; // the parser already filters this; defensive only

            // Unlike an [ACTION]'s optional "actor" (absent = the current speaker), a [RESOLUTION] always
            // requires one: there is no natural "whoever is speaking" fallback for a table decision, and
            // guessing it onto the anchor is exactly the misattribution this whole feature exists to prevent.
            int idx = CouncilActorPolicy.ResolveActor(proposal.Actor, names);

            if (idx < 0)
            {
               ModLog.Error($"[COUNCIL] resolution '{proposal.Type}' named actor '{proposal.Actor}' who could not "
                          + $"be resolved among the seated members ({string.Join(", ", names)}); dropped rather than misattributed.");

               continue;
            }

            string heroId = heroIds[idx];
            string memberName = names[idx];

            if (string.IsNullOrEmpty(heroId))
            {
               ModLog.Error($"[COUNCIL] resolution '{proposal.Type}' named actor '{memberName}', who has no persisted hero, dropped.");

               continue;
            }

            if (string.Equals(proposal.Type, CouncilResolutionPolicy.WithdrawKind, StringComparison.OrdinalIgnoreCase))
            {
               WithdrawCouncilResolution(heroId, memberName, proposal.Detail);

               continue;
            }

            if (string.IsNullOrWhiteSpace(proposal.Detail))
            {
               ModLog.Info($"[COUNCIL] resolution '{proposal.Type}' from {memberName} carried no detail, dropped.");

               continue;
            }

            RecordCouncilResolution(proposal, heroId, memberName);
         }
      }

      /// <summary>
      ///   Resolves a "type: withdraw" against <paramref name="heroId" />'s own live entries via
      ///   <see cref="CouncilResolutionPolicy.SelectWithdrawTarget" />. No resolution id is ever taught to the
      ///   model (the block stays type/actor/detail), so this is the only place a withdrawal is matched to what
      ///   it targets; an unmatched or ambiguous case is silently ignored rather than guessed (logged only).
      /// </summary>
      private void WithdrawCouncilResolution(string heroId, string memberName, string detail)
      {
         List<AssemblyResolutionEntry> entries = _councilLedger.EntriesFor(heroId).ToList();
         AssemblyResolutionEntry target = CouncilResolutionPolicy.SelectWithdrawTarget(entries, detail);

         if (target == null)
         {
            ModLog.Info($"[COUNCIL] {memberName} tried to withdraw, but no single live resolution of theirs could be matched, ignored.");

            return;
         }

         _councilLedger.Reverse(target.Id);
         AppendSystemNotice($"{memberName} withdraws: {target.Detail}");
         ModLog.Info($"[COUNCIL] {memberName} withdrew resolution {target.Id} ({target.Kind}).");
      }

      /// <summary>
      ///   Records a new resolution as the table's decision. There is no separate consent/vote step this slice
      ///   (see AssemblyConsent for that vocabulary, unwired here): a spoken pledge at the table IS the
      ///   decision, so the entry goes straight to Agreed via the legal Proposed -> Agreed transition, staying
      ///   provisional (reversible, nothing executed) until the lift. A second live pledge of the same kind
      ///   from the same member is ignored (CouncilResolutionPolicy.CanRecord), mirroring QuestService's own
      ///   one-task-at-a-time rule.
      /// </summary>
      private void RecordCouncilResolution(ResolutionProposal proposal, string heroId, string memberName)
      {
         List<AssemblyResolutionEntry> existing = _councilLedger.EntriesFor(heroId).ToList();

         if (!CouncilResolutionPolicy.CanRecord(proposal.Type, existing))
         {
            ModLog.Info($"[COUNCIL] {memberName} already carries a live '{proposal.Type}' resolution this session, a second one is ignored (one at a time).");

            return;
         }

         string id = Guid.NewGuid().ToString("N");
         AssemblyResolutionEntry entry = _councilLedger.Propose(id, proposal.Type, heroId, "Council pledge", proposal.Detail, _playerTurnsThisSession);
         entry.TargetHint = proposal.TargetSettlement;
         _councilLedger.Agree(id);

         AppendSystemNotice($"{memberName} pledges: {proposal.Detail}");
         ModLog.Info($"[COUNCIL] {memberName} resolution recorded ({proposal.Type}): {proposal.Detail}");
      }

      /// <summary>
      ///   At the lift: re-validates a single agreed resolution against the world as it stands NOW and executes
      ///   it, or marks it Failed with a plain reason and reports that to the player. The re-validation order
      ///   matters: a member who is no longer available at all (dead, captured, gone from the roster) is
      ///   reported before the kind is even considered, since nothing about WHAT they pledged matters if THEY
      ///   cannot carry it out anymore.
      /// </summary>
      private void SettleCouncilResolution(AssemblyResolutionEntry entry, CouncilScope scope, NpcMemoryCampaignBehavior behavior, int gameDay)
      {
         Hero actorHero = Hero.FindFirst(h => h.StringId == entry.TargetHeroId);
         string actorName = actorHero?.Name?.ToString() ?? "One of the council";

         CouncilEligibility verdict = CouncilRosterResolver.EvaluateNow(scope, actorHero);

         if (actorHero == null || !verdict.Eligible)
         {
            string reason = DescribeCouncilUnavailability(verdict.Reason, actorHero == null);
            _councilLedger.MarkFailed(entry.Id, reason);
            AppendSystemMessage($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {reason}", false);

            return;
         }

         if (!string.Equals(entry.Kind, CouncilResolutionPolicy.QuestKind, StringComparison.OrdinalIgnoreCase))
         {
            // Seam left open for future catalogue kinds (assign_party_role, appoint_governor, dispatch_on_mission,
            // grant_stipend, grant_fief, see ResolutionCatalog): only "quest" has a lift executor this slice, so
            // anything else is reported honestly instead of silently applied or silently dropped.
            _councilLedger.MarkFailed(entry.Id, "This kind of decision is not yet settled by the council.");
            AppendSystemMessage($"{actorName}'s pledge ({entry.Detail}) was not carried out: the table cannot yet act on this kind of decision.", false);

            return;
         }

         NpcProfile giver = behavior.Store.Get(entry.TargetHeroId);

         if (giver == null)
         {
            _councilLedger.MarkFailed(entry.Id, "No record of them could be found.");
            AppendSystemMessage($"{actorName}'s pledge ({entry.Detail}) could not be carried out: no record of them was found.", false);

            return;
         }

         // The exact path an ordinary [QUEST] block already takes (QuestFactory grounding, the one-task-at-a-
         // time guard, the journal mirror, the persisted profile): the seated member stands in as the giver,
         // and the free-text pledge becomes the quest's own description. QuestFactory can only ground a
         // BanditClear deed against a real, named settlement, which is why "quest" resolutions this slice are
         // scoped to that shape (the ratified design's own worked example: "ride to X and clear the bandits").
         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = entry.Detail,
            TargetSettlement = entry.TargetHint
         };

         var failed = false;
         string failureMessage = null;

         QuestService.IssueQuest(giver, actorHero, proposal, gameDay, behavior.Store, (text, positive) => {
            failed = true;
            failureMessage = text;
         });

         if (failed)
         {
            _councilLedger.MarkFailed(entry.Id, failureMessage ?? "This could not be carried out.");
            AppendSystemMessage($"{actorName}'s pledge ({entry.Detail}) could not be carried out: {failureMessage}", false);

            return;
         }

         _councilLedger.MarkApplied(entry.Id);
         AppendSystemMessage($"{actorName} makes good on the council's word: {entry.Detail}.", true);
         ModLog.Info($"[COUNCIL] lift: {actorName}'s resolution settled as a real task ({entry.Detail}).");
      }

      private static string DescribeCouncilUnavailability(CouncilExclusionReason reason, bool heroGone)
      {
         if (heroGone) return "they could not be found.";

         switch (reason)
         {
            case CouncilExclusionReason.Dead: return "they have since died.";
            case CouncilExclusionReason.Imprisoned: return "they are now held prisoner.";
            case CouncilExclusionReason.InBattle: return "they are caught in a battle right now.";
            case CouncilExclusionReason.NotOfThisCouncil: return "they no longer belong to this house.";
            default: return "they are no longer available.";
         }
      }

      #endregion

      /// <summary>
      ///   The council's LIFT: called once from <see cref="ChatManager.Close" />, exactly like the other
      ///   Finalize* memory hooks (idempotent via <see cref="_councilResolutionsFinalized" />). Walks every
      ///   AGREED resolution (<see cref="AssemblyLedger.PendingCommit" />; a merely Proposed or already
      ///   Reversed one never reaches here) and settles each via <see cref="SettleCouncilResolution" />. A
      ///   council with nothing agreed returns immediately: behaves exactly like today, with no extra chat
      ///   line at all.
      /// </summary>
      public void FinalizeCouncilResolutions()
      {
         if (_councilResolutionsFinalized) return;
         _councilResolutionsFinalized = true;

         if (_councilScope == null) return;

         List<AssemblyResolutionEntry> pending = _councilLedger.PendingCommit().ToList();

         if (pending.Count == 0) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null || behavior.GameStateBridge == null) return;

         int gameDay = behavior.GameStateBridge.GetCurrentGameDay();
         CouncilScope scope = _councilScope.Value;

         foreach (AssemblyResolutionEntry entry in pending)
         {
            try
            {
               SettleCouncilResolution(entry, scope, behavior, gameDay);
            }
            catch (Exception ex)
            {
               ModLog.Error($"[COUNCIL] lift: resolution {entry.Id} threw and was skipped: {ex.GetType().Name}: {ex.Message}");
               _councilLedger.MarkFailed(entry.Id, "Something went wrong settling this.");
               AppendSystemMessage("A pledge from the council could not be settled: something went wrong.", false);
            }
         }
      }
   }
}
