// Code written by Gabriel Mailhot, 22/09/2026.
// Where the illuminator picks the chronicle back up, when the list of deeds behind it can shrink.
//
// The bookmark was a COUNT of deeds already written up. That is exact for as long as the list only ever grows,
// which it did until 22/09/2026, when tashmetu's bridge became the first caller anywhere to use Feed.Retract:
//
//   "this latest round of work is the first time we've actually called Feed.Retract... for sieges, raids, town
//    revolts, and pending motions, instead of narrating the end we just retract the original fact once the
//    underlying condition clears."
//
// Retracting a deed the chronicle had already written up removes it from the list the count indexes into. The
// count then points one entry too far in, and the next illumination skips a deed that was never written: the
// chronicle silently loses a chapter, and nothing anywhere says so. Only a full rewrite would put it back, and
// only if somebody noticed.
//
// So the bookmark becomes an IDENTITY: the last deed actually written up, found again by a fingerprint of its
// day and its line. The count stays as the fallback, for a save written before the mark existed and for the
// case where the marked deed is itself the one that was retracted.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Chronicle
{
   /// <summary>Decides which deed the illuminator should resume from, given what it wrote last time.</summary>
   public static class ChronicleResumePolicy
   {
      /// <summary>
      ///   One deed's fingerprint: its day and its line. Not an id, because a deed only has one when a third
      ///   party fed it; day plus line is stable for everything the chronicle holds, and two deeds that share
      ///   both are the same sentence on the same day, where resuming from either gives the same chronicle.
      /// </summary>
      public static string Mark(int day, string line) => day + "|" + (line ?? "").Trim();

      /// <summary>
      ///   The index to start writing from: just after the last deed that was written up, found by its mark.
      ///   <para>
      ///     Falls back to the old count, clamped, when there is no mark (a save from before this existed) or
      ///     when the marked deed is gone (it was the one retracted). The fallback can still skip in that
      ///     second case, and that is the honest limit: once the evidence of where we stopped has been removed,
      ///     a count is the only thing left to go on.
      ///   </para>
      /// </summary>
      public static int ResumeFrom(IReadOnlyList<string> deedMarks, string lastMark, int lastCount)
      {
         if (deedMarks == null || deedMarks.Count == 0) return 0;

         if (!string.IsNullOrEmpty(lastMark))
            for (int i = deedMarks.Count - 1; i >= 0; i--)
               if (string.Equals(deedMarks[i], lastMark, StringComparison.Ordinal))
                  return i + 1;

         if (lastCount <= 0) return 0;

         return lastCount > deedMarks.Count ? deedMarks.Count : lastCount;
      }
   }
}
