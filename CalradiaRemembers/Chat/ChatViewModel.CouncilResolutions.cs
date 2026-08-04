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
         var behavior = NpcMemoryCampaignBehavior.Instance;
         int gameDay = behavior?.GameStateBridge?.GetCurrentGameDay() ?? 0;

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
               WithdrawCouncilResolution(heroId, memberName, proposal.Detail, behavior, gameDay);

               continue;
            }

            if (string.IsNullOrWhiteSpace(proposal.Detail))
            {
               ModLog.Info($"[COUNCIL] resolution '{proposal.Type}' from {memberName} carried no detail, dropped.");

               continue;
            }

            // The other seated members witness this promise (audit M6): recorded so a broken pledge can be
            // made socially costly, and so the promiser's own memory can name who heard it.
            List<string> witnessIds = heroIds.Where(id => !string.IsNullOrEmpty(id) && id != heroId).ToList();
            RecordCouncilResolution(proposal, heroId, memberName, witnessIds);
         }
      }

      /// <summary>
      ///   Resolves a "type: withdraw" against <paramref name="heroId" />'s own live entries via
      ///   <see cref="CouncilResolutionPolicy.SelectWithdrawTarget" />. No resolution id is ever taught to the
      ///   model (the block stays type/actor/detail), so this is the only place a withdrawal is matched to what
      ///   it targets; an unmatched or ambiguous case is silently ignored rather than guessed (logged only).
      /// </summary>
      private void WithdrawCouncilResolution(string heroId, string memberName, string detail, NpcMemoryCampaignBehavior behavior, int gameDay)
      {
         List<AssemblyResolutionEntry> entries = _councilLedger.EntriesFor(heroId).ToList();
         AssemblyResolutionEntry target = CouncilResolutionPolicy.SelectWithdrawTarget(entries, detail);

         if (target == null)
         {
            ModLog.Info($"[COUNCIL] {memberName} tried to withdraw, but no single live resolution of theirs could be matched, ignored.");

            return;
         }

         _councilLedger.Reverse(target.Id);
         CouncilLift.RememberPledge(behavior, heroId, target.Detail, CouncilPledgeOutcome.Withdrawn, gameDay);
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
      private void RecordCouncilResolution(ResolutionProposal proposal, string heroId, string memberName, List<string> witnessIds)
      {
         List<AssemblyResolutionEntry> existing = _councilLedger.EntriesFor(heroId).ToList();

         if (!CouncilResolutionPolicy.CanRecord(proposal.Type, existing))
         {
            ModLog.Info($"[COUNCIL] {memberName} already carries a live '{proposal.Type}' resolution this session, a second one is ignored (one at a time).");

            return;
         }

         string id = Guid.NewGuid().ToString("N");
         // TargetHint is the entry's ONE grounding string, reused per kind rather than growing a second field:
         // a "quest"/"appoint_governor" pledge grounds on the settlement named, "assign_party_role" grounds on the
         // ROLE named instead (see PartyRoleParser, read back by CouncilLift.SettleAssignPartyRole), "dispatch_mission"
         // grounds on the ERRAND named instead (see MissionTypeParser, read back by CouncilLift.SettleDispatchMission),
         // and "grant_stipend" grounds on the proposed AMOUNT instead (read back by int.TryParse in
         // CouncilLift.SettleGrantStipend, the FIRST executor that touches persisted save state).
         string targetHint;
         if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.AssignPartyRole), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetRole;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.DispatchOnMission), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetMission;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.GrantStipend), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetAmount?.ToString();
         else
            targetHint = proposal.TargetSettlement;
         AssemblyResolutionEntry entry = _councilLedger.Propose(id, proposal.Type, heroId, "Council pledge", proposal.Detail, _playerTurnsThisSession, targetHint);

         if (entry == null)
         {
            ModLog.Info($"[COUNCIL] resolution for {memberName} was refused by the ledger (blank or duplicate id); nothing recorded.");

            return;
         }

         entry.WitnessIds = witnessIds; // M6: who heard this promise made
         _councilLedger.Agree(id);

         AppendSystemNotice($"{memberName} pledges: {proposal.Detail}");
         ModLog.Info($"[COUNCIL] {memberName} resolution recorded ({proposal.Type}): {proposal.Detail}");
      }

      #endregion

      /// <summary>
      ///   The council's LIFT: called once from <see cref="ChatManager.Close" />, exactly like the other
      ///   Finalize* memory hooks (idempotent via <see cref="_councilResolutionsFinalized" />). Delegates to the
      ///   shared <see cref="CouncilLift" /> (the same settlement a reloaded council runs headlessly), reporting
      ///   through the chat window, then clears the persisted session (C4): a council closing normally has
      ///   nothing left to recover, and any pledge left unlifted by a crash is caught on the next load instead.
      /// </summary>
      public void FinalizeCouncilResolutions()
      {
         if (_councilResolutionsFinalized) return;
         _councilResolutionsFinalized = true;

         if (_councilScope == null) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null || behavior.GameStateBridge == null) return;

         int gameDay = behavior.GameStateBridge.GetCurrentGameDay();

         CouncilLift.Finalize(_councilLedger, _councilScope.Value, behavior, gameDay, AppendSystemMessage);
         SealCouncilEngagements(behavior, gameDay);
         CouncilSessionBehavior.Instance?.EndSession();
      }
   }
}
