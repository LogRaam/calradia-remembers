// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (26/10/2026), on Rhagaea's night: "si c'est la deuxieme fois ou plus que l'histoire se joue, il faudrait que
// Rhagaea se souvienne des autres fois et que ca soit une continuite." The game keeps, for each tale and each giver, the
// endings it reached with them; when the tale begins again with the same giver, its run carries flags its lines may
// test: "played_before" (any ending before), and "ended_before:<ending id>" for each ending reached ("reward" for the
// reward, "failure" for the failure). Another giver starts fresh: what was lived with one is not the other's memory.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What a tale already lived with this giver, as flags of the run that begins. Pure.</summary>
   public static class TaleHistory
   {
      /// <summary>Set when the tale reached an ending with this giver before, whichever.</summary>
      public const string PlayedBefore = "played_before";

      /// <summary>The prefix of the flag naming an ending reached with this giver before ("ended_before:dawn").</summary>
      public const string EndedBeforePrefix = "ended_before:";

      /// <summary>The key a tale's history with one giver is kept under.</summary>
      public static string Key(string questId, string giverId) => (questId ?? "") + "|" + (giverId ?? "");

      /// <summary>The ending's name as the history keeps it: the ending's id, "reward" or "failure".</summary>
      public static string EndingName(string endingId, bool failed) => failed ? "failure" : string.IsNullOrWhiteSpace(endingId) ? "reward" : endingId.Trim();

      /// <summary>The history with one more ending reached; an ending already there is kept once.</summary>
      public static List<string> After(IReadOnlyCollection<string> endingsBefore, string ending)
      {
         var after = (endingsBefore ?? new List<string>()).ToList();
         if (!string.IsNullOrWhiteSpace(ending) && !after.Contains(ending)) after.Add(ending);

         return after;
      }

      /// <summary>Gives the run beginning now the flags of what the tale already lived with this giver.</summary>
      public static void Mark(AuthoredQuestRun run, IReadOnlyCollection<string> endingsBefore)
      {
         if (run == null || endingsBefore == null || endingsBefore.Count == 0) return;

         run.Flags ??= new List<string>();
         foreach (string flag in new[] {PlayedBefore}.Concat(endingsBefore.Select(e => EndedBeforePrefix + e)))
            if (!run.Flags.Contains(flag)) run.Flags.Add(flag);
      }

      /// <summary>The history flags a tale's lines may test: "played_before", and one per ending the tale can reach.</summary>
      public static IEnumerable<string> FlagsOf(AuthoredQuest q)
      {
         yield return PlayedBefore;
         yield return EndedBeforePrefix + "reward";
         yield return EndedBeforePrefix + "failure";
         foreach (string id in q?.Endings?.Keys ?? Enumerable.Empty<string>()) yield return EndedBeforePrefix + id;
      }
   }
}
