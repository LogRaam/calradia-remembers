// Code written by Gabriel Mailhot, 03/10/2026.
// Console doors to the authored quests (docs/design/AUTHORED_QUESTS.md): what loaded and what is wrong with the files
// (for authors), where the tale stands, and levers to start, step and abandon one while the remaining increments are
// built and for anyone writing a quest of their own.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.AuthoredQuests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.story_check: the quests that loaded, and every problem in the quest files.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_check", "cr")]
      public static string StoryCheck(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b == null) return "Authored quests are not active.";

         b.LoadFiles();
         var sb = new StringBuilder();
         sb.AppendLine($"{b.Quests.Count} quest(s) loaded: {string.Join(", ", b.Quests.Select(q => q.Id))}");
         if (b.Problems.Count == 0) sb.Append("No problem in the quest files.");
         else foreach (string p in b.Problems) sb.AppendLine("- " + p);

         return sb.ToString().TrimEnd();
      }

      /// <summary>cr.story_status: the tale under way, its stage, and who it is bound to.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_status", "cr")]
      public static string StoryStatus(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestRun run = AuthoredQuestBehavior.Instance?.ActiveRun;
         if (run == null) return "No tale has been taken on in this campaign session.";

         return $"'{run.QuestId}' ({run.Status}), stage '{run.StageId}' since day {run.StageStartDay}, started day {run.StartDay}. "
                + $"Bound: {string.Join(", ", run.BoundNames.Select(kv => kv.Key + "=" + kv.Value))}. "
                + $"Locals heard {run.TalkedThisStage.Count}, nights watched {run.NightsWatched}, findings [{string.Join(", ", run.Findings)}].";
      }

      /// <summary>cr.story_start &lt;quest id&gt; [name]: binds the quest to that character (or the one spoken to) and takes it on.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_start", "cr")]
      public static string StoryStart(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b == null || args == null || args.Count == 0) return "Usage: cr.story_start <quest id> [name]   (ids: cr.story_check)";

         AuthoredQuest q = b.Quests.FirstOrDefault(x => x.Id == args[0]);
         if (q == null) return $"No quest '{args[0]}'. See cr.story_check.";
         Hero giver = ResolveHero(args.Skip(1));
         if (giver == null) return "No character found. Pass a name, or open a conversation first.";

         b.ForgetPlayed(q.Id);
         if (!b.TryOffer(giver, (int) CampaignTime.Now.ToDays, 0, q.Id))
            return $"{giver.Name} cannot give '{q.Id}' (the file's requires: role, regard, a village of theirs by a forest...), or a tale is already under way.";

         b.Accept(q, b.Offer);

         return $"'{q.Id}' taken on from {giver.Name}. {StoryStatus(null)}";
      }

      /// <summary>cr.story_step: completes the current stage as though its objective were met.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_step", "cr")]
      public static string StoryStep(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return AuthoredQuestBehavior.Instance?.DebugStep() ?? "Authored quests are not active.";
      }

      /// <summary>cr.story_abandon: ends the tale under way, without reward or penalty.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_abandon", "cr")]
      public static string StoryAbandon(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b?.ActiveRun == null) return "No tale is under way.";
         string id = b.ActiveRun.QuestId;
         b.Abandon("Set aside.");

         return $"'{id}' set aside.";
      }
   }
}
