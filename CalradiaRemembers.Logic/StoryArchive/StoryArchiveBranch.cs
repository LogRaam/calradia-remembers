// Code written by Gabriel Mailhot, 17/11/2026.
// The archive lives outside the save, so a save can be reloaded at an earlier point, or saves can be loaded out of order.
// A cut by date cannot tell those apart: campaign time stands still during a chat (a talk had after the last save is stamped
// with the very time the save holds), and two saves' futures can share dates. Each record names its parent; the save holds
// the id of the last record it knew (its head). Walking back from that head reads exactly that save's past; a branch the
// player abandoned stays in the file, unreachable, so loading the wrong save by mistake costs nothing.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.StoryArchive
{
   /// <summary>Reads the branch of the archive a save belongs to.</summary>
   public static class StoryArchiveBranch
   {
      /// <summary>
      ///   The records from the first to <paramref name="headId" />, oldest first, following each record's parent. Empty for no
      ///   head or an unknown head. Stops at a missing parent (a pruned start) or a cycle (a hand-edited file).
      /// </summary>
      public static IReadOnlyList<StoryArchiveRecord> Walk(IReadOnlyDictionary<string, StoryArchiveRecord> byId, string headId)
      {
         var chain = new List<StoryArchiveRecord>();
         if (byId == null || string.IsNullOrEmpty(headId)) return chain;

         var seen = new HashSet<string>();
         string at = headId;
         while (!string.IsNullOrEmpty(at) && seen.Add(at) && byId.TryGetValue(at, out StoryArchiveRecord record))
         {
            chain.Add(record);
            at = record.ParentId;
         }

         chain.Reverse();

         return chain;
      }

      /// <summary>The records of one file indexed by id; a later line with the same id wins, lines without an id are skipped.</summary>
      public static Dictionary<string, StoryArchiveRecord> Index(IEnumerable<StoryArchiveRecord> records)
      {
         var byId = new Dictionary<string, StoryArchiveRecord>();
         if (records == null) return byId;

         foreach (StoryArchiveRecord r in records)
            if (!string.IsNullOrEmpty(r?.Id))
               byId[r.Id] = r;

         return byId;
      }
   }
}
