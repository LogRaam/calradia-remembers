// Code written by Gabriel Mailhot, 27/08/2026.
// Keeps the mod's log file from growing without bound (player request 2026-08-27): the file is split into
// SESSION blocks, each starting with a known marker line, and only the most recent few are kept. The rule is
// pulled out of ModLog (which is engine/IO-bound) into a pure string-in / string-out policy so the off-by-one
// that "keep the last two sessions" invites is pinned by tests rather than discovered in a bloated log.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Trims a session-delimited log to its most recent blocks.</summary>
   public static class LogRotationPolicy
   {
      /// <summary>
      ///   Returns <paramref name="content" /> reduced to its last <paramref name="maxBlocks" /> session blocks,
      ///   where a block runs from one <paramref name="marker" /> occurrence to just before the next. Anything
      ///   before the first kept block (including any preamble before the first marker) is dropped. The caller
      ///   appends the new session's block AFTER this, so pass one less than the total it wants retained.
      ///   Returns the SAME reference unchanged when there is nothing to trim (fewer blocks than the budget, no
      ///   marker at all, or a non-positive budget that is not zero); returns "" when <paramref name="maxBlocks" />
      ///   is zero (keep none) or content is null.
      /// </summary>
      public static string KeepLastSessions(string content, string marker, int maxBlocks)
      {
         if (content == null) return string.Empty;
         if (maxBlocks == 0) return string.Empty;
         if (maxBlocks < 0 || string.IsNullOrEmpty(marker)) return content;

         var starts = new List<int>();
         int at = content.IndexOf(marker, StringComparison.Ordinal);
         while (at >= 0)
         {
            starts.Add(at);
            at = content.IndexOf(marker, at + marker.Length, StringComparison.Ordinal);
         }

         if (starts.Count <= maxBlocks) return content; // already within budget (or no marker at all)

         return content.Substring(starts[starts.Count - maxBlocks]);
      }
   }
}
