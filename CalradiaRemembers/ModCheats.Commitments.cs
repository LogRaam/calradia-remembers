// Code written by Gabriel Mailhot, 03/07/2026.
// RP with teeth, increment 2, Phase 2: console surface for TrackedCommitment.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.commitments [name]
      ///   Lists any commitments still tracked from an older save (before 2026-08-22, when an NPC's vow
      ///   stopped being a tracked quest and became a plain memory). On a current campaign this is always
      ///   empty; a vow now shows up under cr.memory on the NPC who made it. Diagnostic only, mutates nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("commitments", "cr")]
      public static string ShowCommitments(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         CommitmentBehavior commitments = CommitmentBehavior.Instance;
         if (commitments == null) return "Commitment tracker not active.";

         Hero filter = args is {Count: > 0} ? ResolveHero(args) : null;
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         List<TrackedCommitment> list = commitments.Active
            .Where(c => filter == null || c.PromisorId == filter.StringId || c.TargetId == filter.StringId)
            .ToList();

         if (list.Count == 0)
            return filter == null
               ? "No outstanding pledges."
               : $"No outstanding pledges involving {filter.Name}.";

         var sb = new StringBuilder();
         sb.AppendLine($"{list.Count} outstanding pledge(s):");
         foreach (TrackedCommitment c in list)
         {
            Hero promisor = Hero.FindFirst(h => h.StringId == c.PromisorId);
            Hero rival = Hero.FindFirst(h => h.StringId == c.TargetId);
            int daysLeft = c.DeadlineDay - today;
            sb.AppendLine($"  {promisor?.Name?.ToString() ?? c.PromisorId} -> {rival?.Name?.ToString() ?? c.TargetId} : " +
                          $"deadline day {c.DeadlineDay} ({daysLeft} day(s) left), state={c.State}");
         }

         return sb.ToString();
      }

      /// <summary>
      ///   cr.pledge [name]
      ///   Seeds a real pledge_against for testing: resolves the target NPC (or the current conversation
      ///   NPC), picks a kin-safe rival, forces the two into genuine enmity (below
      ///   <see cref="CourtActionPolicy.PledgeMinAnimosity" />), then fires the SAME pledge_against
      ///   action <see cref="BannerlordGameStateBridge" /> executes from a real conversation — an
      ///   end-to-end seed, not a reimplementation. Launches the underlying scheme (ForcePlot) and leaves
      ///   a memory of the vow on the promisor (2026-08-22: a vow is remembered, not tracked as a quest,
      ///   so there is no journal mirror and no countdown). Check it with cr.memory. **(save)**
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("pledge", "cr")]
      public static string SeedPledge(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero npc = ResolveHero(args);
         if (npc is not {IsLord: true, IsAlive: true} || npc.IsPrisoner)
            return "No eligible lord found. Pass a name fragment or start a conversation with a living lord.";

         NpcProfile profile = GetOrCreateProfile(behavior, npc);

         CommitmentBehavior commitments = CommitmentBehavior.Instance;
         if (commitments != null && commitments.HasActive(npc.StringId))
            return $"{npc.Name} already has a pledge outstanding — one vow at a time.";

         Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != npc && h.IsLord && h.IsAlive && !CourtActionResolver.IsCloseKin(npc, h));
         if (target == null) return $"No kin-safe rival found for {npc.Name}.";

         int relBefore = npc.GetRelation(target);
         int wantedRelation = CourtActionPolicy.PledgeMinAnimosity - 10;
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, wantedRelation - relBefore, false);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "pledge_against",
               Parameters = new Dictionary<string, string> {{"target", target.Name.ToString()}}
            }, npc, new ActionContext(profile, null));

         if (outcome is not {IsPositive: true})
            return $"pledge_against failed for {npc.Name} -> {target.Name}: {outcome?.Message ?? "(no outcome)"}";

         return $"{outcome.Message} (relation {npc.Name}->{target.Name}: {relBefore} -> {npc.GetRelation(target)})";
      }

   }
}
