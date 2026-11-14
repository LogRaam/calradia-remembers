// Code written by Gabriel Mailhot, 10/11/2026.
// When a task offered is one the player already carries, or has just done, and what a war council is told of them.
//
// bdionysis80 (Nexus, 10/11/2026): a first war council set "Scout an army of Sturgia (Athina)", done; a second council,
// days later, set "Scout an army of Sturgia (Vamina)" on the same army. "This was effectively a repetition of the
// scouting assignment I had already completed." The only guard was one task at a time PER GIVER, blind to the deed,
// its target and every other giver, and the council was never told which tasks the player held. Gabriel (10/11/2026):
// "oui vas-y".
//
// So the same deed against the same target (a realm, a person or a place) is not set again while one is open, by
// anyone, nor within RepeatWindowDays of one done; and the table reads the tasks open or done lately, so it builds on
// them. A task with no target at all is never called a repeat (two loads of goods are two tasks).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Quests
{
   /// <summary>A task the game already holds for the player, as the repeat check and the council read it.</summary>
   public sealed class HeldQuest
   {
      /// <summary>The kind of deed.</summary>
      public QuestType Type { get; init; }

      /// <summary>What the deed is against (see <see cref="QuestRepeatPolicy.KeyOf" />); empty when it has no target.</summary>
      public string TargetKey { get; init; }

      /// <summary>Who set it, as the player knows them.</summary>
      public string GiverName { get; init; }

      /// <summary>The game's own title for the deed ("Scout an army of Sturgia"), or null when it has none.</summary>
      public string Deed { get; init; }

      /// <summary>Not yet carried out (still open).</summary>
      public bool Open { get; init; }

      /// <summary>The day it was carried out, or null when it was not (open, failed, given up).</summary>
      public int? DoneOnDay { get; init; }

      /// <summary>What the game recorded as proof, or null.</summary>
      public string Evidence { get; init; }
   }

   /// <summary>Decides whether a task offered repeats one the player holds, and lists those a council should know. Pure.</summary>
   public static class QuestRepeatPolicy
   {
      /// <summary>TUNE: days after a task is done during which the same deed against the same target is not set again.</summary>
      public const int RepeatWindowDays = 7;

      /// <summary>TUNE: the most tasks a council is shown.</summary>
      public const int CouncilListCap = 8;

      /// <summary>
      ///   The target a deed is against, as one key: realm, person and place, lower-cased, and the hideout itself when the deed
      ///   names one; empty when none is set. Aporo (Discord, 14/11/2026, "Can't accept task that is named the same as previous
      ///   one?"): two bandit camps near Iyakis shared the town as their key, so clearing the second was refused as a repeat of
      ///   the first. A deed with no hideout keeps the key it always had.
      /// </summary>
      public static string KeyOf(string factionId, string heroId, string settlement, string hideoutId = null)
      {
         string[] parts = {factionId, heroId, settlement};
         if (parts.All(string.IsNullOrWhiteSpace) && string.IsNullOrWhiteSpace(hideoutId)) return "";

         string key = string.Join("|", parts.Select(p => (p ?? "").Trim().ToLowerInvariant()));

         return string.IsNullOrWhiteSpace(hideoutId) ? key : key + "|" + hideoutId.Trim().ToLowerInvariant();
      }

      /// <summary>
      ///   The held task an offer of <paramref name="type" /> against <paramref name="targetKey" /> repeats: one open, or
      ///   one done within <see cref="RepeatWindowDays" /> of <paramref name="today" />. Null when it repeats none.
      /// </summary>
      public static HeldQuest RepeatOf(QuestType type, string targetKey, int today, IEnumerable<HeldQuest> held)
      {
         if (string.IsNullOrEmpty(targetKey) || held == null) return null;

         return held.FirstOrDefault(q => q != null && q.Type == type && q.TargetKey == targetKey
                                         && (q.Open || q.DoneOnDay is int done && today - done <= RepeatWindowDays));
      }

      /// <summary>
      ///   The lines a council reads of the tasks the player holds: the open ones, then those done within
      ///   <see cref="RepeatWindowDays" />, newest first, at most <see cref="CouncilListCap" />; a task with no title is left out.
      /// </summary>
      public static List<string> CouncilLines(IEnumerable<HeldQuest> held, int today)
      {
         if (held == null) return new List<string>();

         return held.Where(q => q != null && !string.IsNullOrWhiteSpace(q.Deed)
                                && (q.Open || q.DoneOnDay is int done && today - done <= RepeatWindowDays))
                    .OrderByDescending(q => q.Open)
                    .ThenByDescending(q => q.DoneOnDay ?? int.MinValue)
                    .Take(CouncilListCap)
                    .Select(Line)
                    .ToList();
      }

      private static string Line(HeldQuest q)
      {
         string who = string.IsNullOrWhiteSpace(q.GiverName) ? "" : q.GiverName.Trim() + ": ";
         string deed = q.Deed.Trim();
         deed = deed.Length > 0 ? char.ToLowerInvariant(deed[0]) + deed.Substring(1) : deed;
         string state = q.Open ? "still open" : "done";
         string proof = q.Open || string.IsNullOrWhiteSpace(q.Evidence) ? "" : ": " + q.Evidence.Trim().TrimEnd('.');

         return who + deed + ", " + state + proof;
      }
   }
}
