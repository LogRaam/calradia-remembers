// Code written by Gabriel Mailhot, 11/08/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.recruit_notable [name]
      ///   Forces the recruit_notable verb through the REAL bridge path on a settlement notable (a gang leader,
      ///   headman, merchant, artisan, rural notable or preacher): it bequeaths the notable's gang/role to an
      ///   eligible successor in the same town, then makes the notable a companion in the player's clan and
      ///   party with a starting relation bonus. The mod's regard ledger is force-raised past the floor so the
      ///   gate passes (test convenience). With a name fragment it targets that notable; without, the current
      ///   conversation NPC. Reports the outcome, the successor, and the notable's new companion/party state.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("recruit_notable", "cr")]
      public static string RecruitNotable(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No target. Pass a notable's name fragment, or run this in a conversation with one.";
         if (!hero.IsNotable)
            return $"{hero.Name} is not a settlement notable (occupation {hero.Occupation}). recruit_notable only applies to gang leaders, headmen, merchants, artisans, rural notables or preachers.";

         Hero successor = BannerlordGameStateBridge.FindNotableSuccessor(hero);

         if (successor == null)
            return $"{hero.Name} has no eligible successor in {hero.CurrentSettlement?.Name?.ToString() ?? "their settlement"} to inherit the post, so recruit_notable would refuse (it never leaves a town's role vacant).";

         // Force the mod's own regard ledger past the floor so the shared gate passes (bypasses earning it).
         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         profile.ReputationWithPlayer = Math.Max(profile.ReputationWithPlayer, NotableRecruitmentPolicy.RegardFloor);
         behavior.Store.Set(profile);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "recruit_notable"}, hero, new ActionContext(profile, null));

         if (outcome == null) return $"recruit_notable returned no outcome for {hero.Name} (see the log).";

         string verdict = outcome.IsPositive
            ? "RECRUITED"
            : "REFUSED";
         string party = hero.PartyBelongedTo == MobileParty.MainParty
            ? "main"
            : hero.PartyBelongedTo?.Name?.ToString() ?? "none";

         return $"[{verdict}] {outcome.Message}\n" +
                $"successor {successor.Name} now holds the post; {hero.Name} companion={hero.IsPlayerCompanion}, party={party}.";
      }
   }
}
