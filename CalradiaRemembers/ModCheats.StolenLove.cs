// Code written by Gabriel Mailhot, 26/08/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Marriage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.stolen_love [status|seed|declare|divorce] [name]
      ///   Drives the Stolen Love estrangement arc on a MARRIED NPC without waiting the weeks it normally takes,
      ///   for testing. status: report the tracked phase; seed: begin an estrangement (bypasses the affair gate);
      ///   declare: pull it to ReadyToDivorce and stamp the declaration; divorce: fire the divorce NOW (unmarry
      ///   the couple + plant the jilted spouse's grudge). The NPC must be married to a third party, not to you.
      ///   Needs "Enable Stolen Love" on for the arc to run naturally, but these forced steps apply regardless.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("stolen_love", "cr")]
      public static string StolenLove(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         NpcMarriageEstrangementBehavior behavior = NpcMarriageEstrangementBehavior.Instance;
         if (behavior == null) return "Stolen Love system is not active.";

         var all = args ?? new List<string>();
         string sub = all.Count > 0 ? all[0].ToLowerInvariant() : "status";
         List<string> nameTokens = all.Skip(1).ToList();

         Hero npc = ResolveHero(nameTokens);
         if (npc == null) return "No NPC found. Pass a name fragment, or start a conversation first.";

         var today = (int) CampaignTime.Now.ToDays;

         switch (sub)
         {
            case "seed":    return $"{npc.Name}: {behavior.DebugBegin(npc, today)}";
            case "declare": return $"{npc.Name}: {behavior.DebugDeclare(npc.StringId, today)}";
            case "divorce": return $"{npc.Name}: {behavior.DebugForceDivorce(npc, today)}";
            case "status":  return $"{npc.Name}: {behavior.DebugDescribe(npc.StringId)}";
            default:        return "Usage: cr.stolen_love [status|seed|declare|divorce] [name]";
         }
      }
   }
}
