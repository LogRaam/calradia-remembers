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
            // declare_war and its symmetric counterpart make_peace are the carve-outs (ResolveDeclareWarActor /
            // ResolveMakePeaceActor): both are FACTION-CENTRIC decisions, not a member's own promise, so an
            // OMITTED actor there defaults to the presiding member instead of being dropped; a NAMED-but-unmatched
            // actor still refuses exactly like every other kind.
            bool isDeclareWar = string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.DeclareWar), StringComparison.OrdinalIgnoreCase);
            bool isMakePeace = string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.MakePeace), StringComparison.OrdinalIgnoreCase);
            int idx = isDeclareWar
               ? CouncilActorPolicy.ResolveDeclareWarActor(proposal.Actor, names)
               : isMakePeace
                  ? CouncilActorPolicy.ResolveMakePeaceActor(proposal.Actor, names)
                  : CouncilActorPolicy.ResolveActor(proposal.Actor, names);

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
         // "grant_stipend" grounds on the proposed AMOUNT instead (read back by int.TryParse in
         // CouncilLift.SettleGrantStipend, the FIRST executor that touches persisted save state), and "declare_war"
         // grounds on the FACTION named instead (read back by CouncilLift.SettleDeclareWar, the FIRST FACTION-CENTRIC
         // kind); its symmetric counterpart "make_peace" REUSES the same TargetFaction field (read back as the
         // FALLBACK in CouncilLift.SettleMakePeace, which prefers the parley's own STORED target faction). Partie
         // 1's "give_gold" REUSES grant_stipend's own TargetAmount field/parse (no new SDK field): both kinds
         // ground on a denars figure, read back by int.TryParse in CouncilLift.SettleGiveGold. Partie 1's other
         // kind, "give_influence", grounds on NOTHING beyond the actor: the amount is never an LLM choice (see
         // ResolutionCatalog.GiveInfluence's own doc), it is computed fresh at the lift by CourtActionPolicy.InfluenceGrant,
         // so it falls through to the default branch below and simply carries a null TargetHint (proposal.TargetSettlement,
         // which the LLM is never taught to emit for this kind), CouncilLift.SettleGiveInfluence never reads it.
         // Partie 2's "grant_fief" and "revoke_fief" (COUNCIL_ACTIONS.md) ground on the FIEF NAME, which needs no
         // new field either: they fall through to the SAME default branch "quest"/"appoint_governor" already use,
         // reading proposal.TargetSettlement, read back by CouncilLift.SettleGrantFief/SettleRevokeFief. Partie
         // 8's "expel_from_clan" grounds on NOTHING beyond the actor, exactly like "give_influence": it also
         // falls through to this default branch and simply carries a null TargetHint (the LLM is never taught to
         // emit target_settlement for this kind), CouncilLift.SettleExpelFromClan never reads it.
         // Partie 1's "arrange_marriage" (COUNCIL_ACTIONS.md, "juste arrange_mariage qui est valide") is the ONE
         // kind that grounds on TWO names rather than one (the player-clan kin AND the seated ally's own kin):
         // both are composed into this single TargetHint slot by ArrangeMarriageGroundingCodec.Compose (a
         // structured composite this codebase controls end to end, never prose-parsing), split apart again at
         // the lift by the SAME codec (CouncilLift.SettleArrangeMarriage). Partie 8's "swap_fiefs" (COUNCIL_ACTIONS.md's
         // Kimi review) is the SECOND such two-name kind (the player's own fief AND the seated vassal's own
         // fief), composed the same way by the dedicated FiefSwapGroundingCodec.Compose, split apart again at
         // the lift by that same codec (CouncilLift.SettleSwapFiefs). "release_prisoner" (rounding out the
         // Parley toolkit alongside make_peace and tribute, 2026-08-04) grounds on the CAPTIVE'S OWN NAME, a
         // dedicated SDK field (ResolutionProposal.TargetPrisonerName, not a reuse of TargetRival, which is
         // pledge_against's own ADVERSARY grounding, a different relationship entirely), read back by
         // CouncilLift.SettleReleasePrisoner via BuyPrisonerVerb's own tolerant name match over the player's
         // held prisoners. "sponsor_ward" (Kimi's design, MINIMAL v1, COUNCIL_ACTIONS.md's Partie 8) grounds on
         // the NAMED YOUNG COMPANION'S OWN NAME, its own dedicated field (ResolutionProposal.TargetWardName, not
         // a reuse of TargetHostageName or TargetRival - a ward is neither a Parley hostage nor a rival), read
         // back by CalradiaRemembers.Assemblies.WardCandidateResolver.ResolveByName at the lift.
         string targetHint;
         if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.ArrangeMarriage), StringComparison.OrdinalIgnoreCase))
            targetHint = ArrangeMarriageGroundingCodec.Compose(proposal.PlayerKinName, proposal.TargetKinName);
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.SwapFiefs), StringComparison.OrdinalIgnoreCase))
            targetHint = FiefSwapGroundingCodec.Compose(proposal.PlayerFiefName, proposal.TargetFiefName);
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.AssignPartyRole), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetRole;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.DispatchOnMission), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetMission;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.GrantStipend), StringComparison.OrdinalIgnoreCase)
               || string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.GiveGold), StringComparison.OrdinalIgnoreCase)
               // Partie 8 ("tribute", COUNCIL_ACTIONS.md's Kimi review): a THIRD reuse of grant_stipend's own
               // TargetAmount field/parse (no new SDK field), read back by int.TryParse in
               // CouncilLift.SettleTribute exactly like grant_stipend's own amount.
               || string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.Tribute), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetAmount?.ToString();
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.DeclareWar), StringComparison.OrdinalIgnoreCase)
               || string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.MakePeace), StringComparison.OrdinalIgnoreCase))
            targetHint = proposal.TargetFaction;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.PledgeAgainst), StringComparison.OrdinalIgnoreCase))
            // Partie 1's schemes kind (COUNCIL_ACTIONS.md's schemes block, reusing the existing 1:1
            // pledge_against system end to end): grounds on the RIVAL'S NAME, a dedicated SDK field
            // (ResolutionProposal.TargetRival, not a reuse of TargetSettlement, which would be a confusing lie
            // for a hero's name to live in), read back by CouncilLift.SettlePledgeAgainst via
            // CourtActionResolver's own tolerant name match.
            targetHint = proposal.TargetRival;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.ReleasePrisoner), StringComparison.OrdinalIgnoreCase))
            // "release_prisoner" (rounding out the Parley toolkit alongside make_peace and tribute): grounds on
            // the CAPTIVE'S OWN NAME, this kind's own dedicated field (see this method's own doc above).
            targetHint = proposal.TargetPrisonerName;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.GiveHostage), StringComparison.OrdinalIgnoreCase))
            // "give_hostage" (Kimi's "v1 invite d'honneur", COUNCIL_ACTIONS.md's Partie 8, the Parley toolkit's
            // fourth concession): grounds on the ELIGIBLE RELATIVE'S OWN NAME, this kind's own dedicated field
            // (ResolutionProposal.TargetHostageName, not a reuse of TargetPrisonerName or TargetRival - a
            // hostage is neither a captive the player holds nor a rival), read back by
            // CalradiaRemembers.Assemblies.HostageCandidateResolver.ResolveByName at the lift.
            targetHint = proposal.TargetHostageName;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.SponsorWard), StringComparison.OrdinalIgnoreCase))
            // "sponsor_ward" (Kimi's design, MINIMAL v1, COUNCIL_ACTIONS.md's Partie 8): grounds on the NAMED
            // YOUNG COMPANION'S OWN NAME, this kind's own dedicated field (see this method's own doc above),
            // read back by CalradiaRemembers.Assemblies.WardCandidateResolver.ResolveByName at the lift.
            targetHint = proposal.TargetWardName;
         else if (string.Equals(proposal.Type, ResolutionCatalog.KeyOf(ResolutionKind.SwearOath), StringComparison.OrdinalIgnoreCase))
            // R7-light ("swear_oath", COUNCIL_ACTIONS.md's Partie 8): the ONE kind whose own grounding needs TWO
            // pieces in this single slot, WHICH oath kind (proposal.OathKind, a dedicated field: "pay_gold" /
            // "keep_peace" / "protect", read back by OathKindParser at the lift) and that kind's own target
            // value, REUSING existing fields rather than growing new ones (proposal.TargetAmount for pay_gold's
            // denars figure, proposal.TargetFaction for keep_peace's named faction; protect names neither, so
            // the value half is simply blank). Composed by OathGroundingCodec.Compose, split apart again at the
            // lift by the SAME codec (CouncilLift.SettleSwearOath).
            targetHint = OathGroundingCodec.Compose(proposal.OathKind, proposal.TargetAmount?.ToString() ?? proposal.TargetFaction ?? "");
         // "non_aggression_pact" (rounding out the Parley toolkit with a FIFTH concession) grounds on NOTHING
         // beyond the actor, exactly like "give_influence"/"expel_from_clan" below: the faction pair is never an
         // LLM choice (the player's own live faction and the parley's own stored target, see
         // CouncilLift.SettleNonAggressionPact), so it falls through to this same default branch and carries a
         // null TargetHint, never read by the lift.
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

         CouncilLift.Finalize(_councilLedger, _councilScope.Value, behavior, gameDay, AppendSystemMessage, _councilParleyTargetFactionId);
         SealCouncilEngagements(behavior, gameDay);
         CouncilSessionBehavior.Instance?.EndSession();
      }
   }
}
