// Code written by Gabriel Mailhot, 02/09/2026.
// Player recovery for a stuck quest. A player reported a "capture an enemy lord" task that outlived a peace
// treaty and could not be dropped in conversation (the giver's LLM invented a non-existent action). The daily
// sweep now voids that specific case on peace, but a general escape hatch is still wanted for any quest that
// became impossible or bugged: cr.quests_all lists every outstanding quest with a number, cr.quest_void <#>
// cancels one as a clean NEUTRAL void (no penalty), distinct from cr.quest_abandon which applies the broken-word
// penalty (unjust for a quest that failed through no fault of the player). The number-to-quest mapping is the
// tested QuestRecoveryPolicy; this file only enumerates the quests and runs the neutral void.

#region

using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Quests;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.quests_all
      ///   Lists EVERY outstanding quest across all givers, each with a number, so a stuck one can be cancelled by
      ///   number with cr.quest_void. Unlike cr.quests [name], it is not scoped to one NPC.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quests_all", "cr")]
      public static string QuestsAll(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         List<OutstandingQuestRef> outstanding = EnumerateOutstandingQuests(behavior);
         if (outstanding.Count == 0) return "No outstanding quests.";

         var sb = new StringBuilder();
         sb.AppendLine($"{outstanding.Count} outstanding quest(s) - cr.quest_void <#> cancels one with no penalty:");
         for (var i = 0; i < outstanding.Count; i++)
         {
            OutstandingQuestRef r = outstanding[i];
            sb.AppendLine($"  {i + 1}. {DescribeQuestShort(r.Quest)} - from {r.Giver?.Name?.ToString() ?? r.Profile.Name}");
         }

         return sb.ToString().TrimEnd();
      }

      /// <summary>
      ///   cr.quest_void &lt;#&gt;
      ///   Cancels the numbered quest from cr.quests_all as a clean NEUTRAL void: no relation penalty, no grudge,
      ///   no broken-word. For unsticking an impossible or bugged quest (cr.quest_abandon instead applies the
      ///   broken-word penalty, unjust for a quest that became impossible through no fault of the player).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_void", "cr")]
      public static string QuestVoid(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         List<OutstandingQuestRef> outstanding = EnumerateOutstandingQuests(behavior);
         QuestRecoveryPolicy.Selection sel =
            QuestRecoveryPolicy.Resolve(args != null && args.Count > 0 ? args[0] : null, outstanding.Count);
         if (!sel.Ok) return sel.Error;

         OutstandingQuestRef chosen = outstanding[sel.Index];
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         return QuestService.VoidQuestNeutral(chosen.Profile, chosen.Giver, chosen.Quest, day, behavior.Store);
      }

      private static List<OutstandingQuestRef> EnumerateOutstandingQuests(NpcMemoryCampaignBehavior behavior)
      {
         var list = new List<OutstandingQuestRef>();
         if (behavior?.Store == null) return list;

         foreach (string id in behavior.Store.ListIds())
         {
            NpcProfile profile = behavior.Store.Get(id);
            if (profile?.ActiveQuests == null) continue;

            Hero giver = Hero.FindFirst(h => h.StringId == id);
            foreach (InformalQuest quest in profile.ActiveQuests)
               if (quest.IsOutstanding)
                  list.Add(new OutstandingQuestRef {Profile = profile, Giver = giver, Quest = quest});
         }

         return list;
      }

      private static string DescribeQuestShort(InformalQuest quest)
      {
         if (quest == null) return "(none)";

         string target = string.IsNullOrEmpty(quest.TargetHeroName) ? "" : $" ({quest.TargetHeroName})";

         return $"{quest.Type}{target}";
      }

      private struct OutstandingQuestRef
      {
         public NpcProfile Profile;
         public Hero Giver;
         public InformalQuest Quest;
      }
   }
}
