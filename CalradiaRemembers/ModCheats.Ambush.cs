// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.ambush [name]
      ///   Arms a town ambush ("stance with teeth", increment 3) against the player by the named lord (or the
      ///   conversation NPC). If you are already in a town it springs at once; otherwise walk into one and it may.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ambush", "cr")]
      public static string ForceAmbush(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero assassin = ResolveHero(args);
         if (assassin == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Stance.StanceConsequenceBehavior.DebugArm(assassin);

         if (Settlement.CurrentSettlement is {IsTown: true})
            return Stance.TownAmbushBehavior.DebugSpring(assassin)
               ? $"{assassin.Name} springs an ambush on you now."
               : $"{assassin.Name} armed an ambush, but it could not spring here (check the log).";

         return $"{assassin.Name} has armed an ambush — walk into a town and it may spring.";
      }

      /// <summary>
      ///   cr.ambush_lose [name]
      ///   Simulates LOSING a town ambush ("stance with teeth", increment 3.2a): the named lord's (or the
      ///   conversation NPC's) cutthroats take the player prisoner and convoy them toward the lord's seat.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ambush_lose", "cr")]
      public static string ForceAmbushLose(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero paymaster = ResolveHero(args);
         if (paymaster == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         return Stance.AmbushCaptivityBehavior.Instance?.Capture(paymaster) == true
            ? $"Capture armed by {paymaster.Name} — you will be pulled out of the town onto the road and taken."
            : $"Could not arm the capture (check the log).";
      }

      /// <summary>
      ///   cr.ambush_gate [town]
      ///   TEST: opens the alley fight for a named town (or the first town) while the player is ON THE MAP, to
      ///   see whether OpenAlleyFightMission can launch from a map context (the "fight at the gates" hypothesis).
      ///   Stand outside a town, not inside it, and run this.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ambush_gate", "cr")]
      public static string ForceAmbushGate(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (Hero.MainHero?.CurrentSettlement != null) return "You are INSIDE a settlement — leave to the map first to test the gate launch.";

         string townName = args is {Count: > 0} ? string.Join(" ", args).Trim() : null;
         Settlement town = string.IsNullOrEmpty(townName)
            ? Settlement.All.FirstOrDefault(s => s.IsTown)
            : Settlement.All.FirstOrDefault(s => s.IsTown && s.Name.ToString().IndexOf(townName, StringComparison.OrdinalIgnoreCase) >= 0);
         if (town == null) return "No matching town found.";

         Hero assassin = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
         if (assassin == null) return "No lord to play the paymaster.";

         return Stance.TownAmbushBehavior.DebugSpringAt(assassin, town)
            ? $"Opening the alley fight at {town.Name} from the map (paymaster {assassin.Name}) — watch if the scene loads."
            : $"Could not open the alley fight at {town.Name} (check the log).";
      }

      /// <summary>
      ///   cr.ambush_scene &lt;locationId&gt; [name]
      ///   TEST: spring the in-town ambush forcing a specific location id as the fight scene — to hunt which one
      ///   renders a coherent enclosed brawl instead of OpenAlleyFightMission's siege fallback. Stand INSIDE a
      ///   town and try: tavern, house_1, house_2, port, arena, center. The paymaster is the named NPC, else any
      ///   armed assassin, else any lord.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ambush_scene", "cr")]
      public static string ForceAmbushScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (args is not {Count: > 0}) return "Usage: cr.ambush_scene <locationId> [name]  (try: tavern, house_1, port, arena, center)";
         if (Settlement.CurrentSettlement is not {IsTown: true}) return "Stand INSIDE a town first.";

         string locationId = args[0];
         Hero assassin = ResolveHero(args.Skip(1).ToList())
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
         if (assassin == null) return "No NPC found to play the paymaster.";

         return Stance.TownAmbushBehavior.DebugSpringScene(assassin, locationId)
            ? $"Springing the ambush in '{locationId}' (paymaster {assassin.Name}) — see how it renders + watch the loaded-scene log."
            : $"Could not spring in '{locationId}' (check the log).";
      }

      /// <summary>
      ///   cr.ambush_audience [name]
      ///   TEST (3.2b): open the gloating audience with the named lord (or the conversation NPC) now — the captor
      ///   lord receives the captured player and savours it. Skips the convoy journey.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ambush_audience", "cr")]
      public static string ForceAmbushAudience(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero lord = ResolveHero(args);
         if (lord == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         return Stance.AmbushCaptivityBehavior.DebugLordAudience(lord)
            ? $"Opening {lord.Name}'s gloating audience."
            : $"Could not open the audience (no stored profile for {lord.Name}? check the log).";
      }
   }
}
