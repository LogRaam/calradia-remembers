// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.memory_add &lt;days_ago&gt; &lt;text&gt; [name]
      ///   Creates a new memory event dated &lt;days_ago&gt; days in the past, so a player
      ///   can construct a prior relationship with a character (tester request: "not
      ///   everyone should be a stranger"). The last token is tried as an NPC name
      ///   fragment, same as cr.memory_set.
      ///   Example: cr.memory_add 30 We fought side by side at Pendraic. Derthert
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory_add", "cr")]
      public static string AddMemoryEvent(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count < 2 || !int.TryParse(args[0], out int daysAgo) || daysAgo < 0)
            return "Usage: cr.memory_add <days_ago> <text> [name]\n" + "       Example: cr.memory_add 30 We fought side by side at Pendraic. Derthert\n" + "       <days_ago> dates the memory in the past (0 = today).";

         // Try the last token as an NPC name fragment. If it resolves, strip it.
         Hero hero = null;
         int summaryEnd = args.Count; // exclusive
         if (args.Count >= 3)
         {
            string lastToken = args[args.Count - 1];
            Hero candidate = Hero.AllAliveHeroes.FirstOrDefault(h =>
               h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(lastToken, StringComparison.OrdinalIgnoreCase) >= 0);
            if (candidate != null)
            {
               hero = candidate;
               summaryEnd = args.Count - 1;
            }
         }

         if (hero == null)
            hero = Hero.OneToOneConversationHero;

         if (hero == null)
            return "No NPC found. Pass a name fragment as the last argument or start a conversation first.";

         string summary = string.Join(" ", args.Skip(1).Take(summaryEnd - 1)).Trim();

         if (string.IsNullOrWhiteSpace(summary))
            return "The memory text cannot be empty.";

         int gameDay = Math.Max(1, (int) CampaignTime.Now.ToDays - daysAgo);
         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         profile.Events.Add(new NotableEvent(gameDay, NotableEventType.Other, summary));
         // Keep chronology readable for both cr.memory and the prompt.
         profile.Events.Sort((a, b) => a.gameDay.CompareTo(b.gameDay));
         behavior.Store.Set(profile);

         return $"{hero.Name}: memory added ({DescribeDay(gameDay)}) — \"{summary}\".\n" + $"{profile.Events.Count} event(s) total. Remember: profiles persist when you SAVE the game.";
      }

      /// <summary>
      ///   cr.forget_all [name]
      ///   Clears the entire memory of an NPC. Use when a session has badly corrupted
      ///   the profile and a fresh start is the cleanest fix.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("forget_all", "cr")]
      public static string ForgetAll(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int count = profile.Events.Count;
         profile.Events.Clear();
         behavior.Store.Set(profile);

         return $"{hero.Name}: all {count} memory event(s) cleared. They will start fresh from the next conversation.";
      }

      /// <summary>
      ///   cr.forget &lt;index&gt; [name]
      ///   Removes the memory event at the given 0-based index (see cr.memory).
      ///   Use this to delete a corrupted or erroneous entry so the NPC stops building on it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("forget", "cr")]
      public static string ForgetEvent(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count == 0 || !int.TryParse(args[0], out int index))
            return "Usage: cr.forget <index> [name]  (use cr.memory to see indices)";

         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : new List<string>();
         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (index < 0 || index >= profile.Events.Count)
            return $"Index {index} out of range — {hero.Name} has {profile.Events.Count} event(s). Use cr.memory to see them.";

         NotableEvent removed = profile.Events[index];
         profile.Events.RemoveAt(index);
         behavior.Store.Set(profile);

         return $"{hero.Name}: removed event [{index}] (Day {removed.gameDay} | {removed.type}) — \"{removed.summary}\".\n" + $"{profile.Events.Count} event(s) remain.";
      }

      /// <summary>
      ///   cr.memory [name]
      ///   Lists all recorded memory events for an NPC, each prefixed by its 0-based
      ///   index so the player can identify which ones to remove with cr.forget.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory", "cr")]
      public static string ListMemory(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.Events.Count == 0)
            return $"{hero.Name} has no recorded memory events.";

         IEnumerable<string> lines = profile.Events.Select((e, i) =>
            $"  [{i}] {DescribeDay(e.gameDay)} | {e.type} | {e.summary}");

         return $"{hero.Name} memory ({profile.Events.Count} event(s)):\n" + string.Join("\n", lines);
      }

      /// <summary>
      ///   cr.memory_set &lt;index&gt; &lt;new text&gt; [name]
      ///   Replaces the summary of the event at the given index with new text.
      ///   The last token is tried as an NPC name fragment; if it matches a hero it is
      ///   used as such, otherwise the whole remainder is the summary and the current
      ///   conversation NPC is used.
      ///   Example: cr.memory_set 2 She remembered me fondly. Alympia
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory_set", "cr")]
      public static string SetMemoryEvent(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count < 2 || !int.TryParse(args[0], out int index))
            return "Usage: cr.memory_set <index> <new text> [name]\n" + "       Example: cr.memory_set 2 She was kind and warm. Alympia\n" + "       Use cr.memory to see indices.";

         // Try the last token as an NPC name fragment. If it resolves, strip it.
         Hero hero = null;
         int summaryEnd = args.Count; // exclusive
         if (args.Count >= 3)
         {
            string lastToken = args[args.Count - 1];
            Hero candidate = Hero.AllAliveHeroes.FirstOrDefault(h =>
               h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(lastToken, StringComparison.OrdinalIgnoreCase) >= 0);
            if (candidate != null)
            {
               hero = candidate;
               summaryEnd = args.Count - 1;
            }
         }

         if (hero == null)
            hero = Hero.OneToOneConversationHero;

         if (hero == null)
            return "No NPC found. Pass a name fragment as the last argument or start a conversation first.";

         string newSummary = string.Join(" ", args.Skip(1).Take(summaryEnd - 1)).Trim();

         if (string.IsNullOrWhiteSpace(newSummary))
            return "The new text cannot be empty.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (index < 0 || index >= profile.Events.Count)
            return $"Index {index} out of range — {hero.Name} has {profile.Events.Count} event(s). Use cr.memory to see them.";

         NotableEvent old = profile.Events[index];
         profile.Events[index] = old with {summary = newSummary};
         behavior.Store.Set(profile);

         return $"{hero.Name}: event [{index}] updated.\n" + $"  Before: \"{old.summary}\"\n" + $"  After:  \"{newSummary}\"";
      }

      /// <summary>
      ///   cr.summary_queue [drain]
      ///   Shows the durable conversation-summary queue: rich-summary jobs waiting to enrich a base
      ///   "Spoke with X." memory. They are persisted with the save, drained in the background, and replayed
      ///   on load, so a promised plan's summary is not lost on save/quit/crash. 'drain' forces a pass now.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("summary_queue", "cr")]
      public static string SummaryQueue(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         var queue = behavior.ConvSummaryQueue;

         if (args != null && args.Any(a => string.Equals(a, "drain", StringComparison.OrdinalIgnoreCase)))
         {
            queue.DrainNow();

            return $"Drain kicked. {queue.PendingCount} job(s) pending (they finish in the background).";
         }

         IReadOnlyList<PendingSummaryJob> jobs = queue.Snapshot();

         if (jobs.Count == 0) return "Conversation-summary queue is empty.";

         var sb = new StringBuilder();
         sb.AppendLine($"Conversation-summary queue: {jobs.Count} pending job(s).");
         foreach (PendingSummaryJob j in jobs)
            sb.AppendLine($"  {j.ProfileId}: {DescribeDay(j.AnchorDay)}: \"{j.AnchorSummary}\" (attempts {j.Attempts})");
         sb.Append("Use cr.summary_queue drain to force a pass.");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.api_probe [name]
      ///   Smoke-tests the public Extension Surface memory API: prints the API version and the loaded save's
      ///   stable id, and, when a name resolves, injects a test memory via CrNpcMemory.AddMemory then reads it
      ///   back via GetMemories, so a modder can confirm the read/write door works end to end.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("api_probe", "cr")]
      public static string ApiProbe(List<string> args)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"CR Extension API v{CrNpcMemory.ApiVersion}.");
         sb.AppendLine($"SaveId: {Campaign.Current?.UniqueGameId ?? "(no campaign loaded)"}");

         Hero hero = ResolveHero(args);

         if (hero == null)
         {
            sb.Append("Give an NPC name to inject a test memory and read it back: cr.api_probe <name>");

            return sb.ToString();
         }

         MemoryResult result = CrNpcMemory.AddMemory(new CrMemoryEntry {
            NpcId = hero.StringId,
            Summary = "(api_probe) We spoke of a plan to meet again.",
            Kind = "Agreement",
            SourceMod = "cr.api_probe"
         });
         IReadOnlyList<CrMemoryRecord> memories = CrNpcMemory.GetMemories(hero.StringId);

         sb.AppendLine($"AddMemory({hero.Name}) -> {result}.");
         sb.AppendLine($"GetMemories({hero.Name}) -> {memories.Count} record(s), newest first:");
         foreach (CrMemoryRecord m in Enumerable.Reverse(memories).Take(3))
            sb.AppendLine($"  {DescribeDay(m.Day)}: [{m.Kind}] {m.Summary}");
         sb.Append("(the injected line is marked '(api_probe)'.)");

         return sb.ToString();
      }

      /// <summary>
      ///   "Day 91078 (6 days ago)": day numbers are absolute calendar days (the
      ///   Calradian year is 84 days), which players read as gibberish without the
      ///   relative part (tester report: guessed '9' was the day and the rest hours).
      /// </summary>
      private static string DescribeDay(int gameDay)
         => MemoryDayFormatter.Describe(gameDay, (int) CampaignTime.Now.ToDays);
   }
}
