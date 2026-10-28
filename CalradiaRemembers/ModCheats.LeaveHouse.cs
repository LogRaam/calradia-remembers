// Code written by Gabriel Mailhot, 26/10/2026.
// Leaving the house for a ruler (Gabriel, 26/10/2026): what bars it, and the real hand-over for a throwaway save. The
// hand-over cannot be undone, so cr.test all never runs it; cr.leave_house is how it is walked.

#region

using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Marriage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.leave_house_check [name]: what stands between the player and leaving their house for this ruler. Read-only.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("leave_house_check", "cr")]
      public static string LeaveHouseCheck(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         Hero ruler = ResolveHero(args);
         if (ruler == null) return "No hero found. Pass a name fragment or start a conversation first.";

         LeaveHouseFacts f = LeaveHouseForRulerBehavior.FactsFor(ruler, LeaveHouseRoad.ThroughTheHeir);
         Hero heir = LeaveHouseForRulerBehavior.BestHeir();
         var sb = new StringBuilder();
         sb.AppendLine($"{ruler.Name}: through the heir {LeaveHouseForRulerPolicy.WhyNot(f)}; giving her all {LeaveHouseForRulerBehavior.WhyNot(ruler, LeaveHouseRoad.GiveHerAll)}");
         UnionFacts union = GiveHerAllService.UnionFor(ruler);
         sb.AppendLine($"  giving all: fiefs={union.FiefsBrought} realm={union.BringsARealm} defects={union.DefectsFromARival} war chance={LeaveHouseForRulerPolicy.WarChance(union)}%");
         sb.AppendLine($"  heir: {heir?.Name?.ToString() ?? "none"}{(f?.HeirRidesWithThePlayer == true ? " (in your party)" : "")}");
         sb.AppendLine($"  wed={f?.PlayerIsWed} twoHeads={f?.TwoHeadsOfHouses} warOrCaptivity={f?.WarOrCaptivity} heartWon={f?.HerHeartIsWon}");
         sb.AppendLine($"  armyOrBattle={f?.ArmyOrBattle} gameWouldWedAfter={f?.TheGameWouldWedThemAfter}");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.give_her_all [name]: gives the ruler your fiefs, gold and realm and weds her now, no popup: the second road.
      ///   IRREVERSIBLE: for a throwaway save.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("give_her_all", "cr")]
      public static string GiveHerAll(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         Hero ruler = ResolveHero(args);
         if (ruler == null) return "No hero found. Pass a name fragment.";

         string refusal = GiveHerAllService.Give(ruler);

         return refusal ?? $"Done: {ruler.Name} holds your fiefs and gold, and is your {(ruler.IsFemale ? "wife" : "husband")}.";
      }

      /// <summary>
      ///   cr.leave_house [name]: runs the REAL hand-over through the heir now, no popup. IRREVERSIBLE.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("leave_house", "cr")]
      public static string LeaveHouse(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         Hero ruler = ResolveHero(args);
         if (ruler == null) return "No hero found. Pass a name fragment.";

         string refusal = LeaveHouseForRulerBehavior.HandOver(ruler);

         return refusal ?? $"Done: you now play {Hero.MainHero?.Name}; {Hero.MainHero?.Name}'s predecessor is wed to {ruler.Name}.";
      }
   }
}
