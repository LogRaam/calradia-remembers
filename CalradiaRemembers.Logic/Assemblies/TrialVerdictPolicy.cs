// Code written by Gabriel Mailhot, 26/09/2026.
// A trial's verdict (arrests; Gabriel: a trial acted by a council, "le joueur doit sentir qu'il confronte une
// assemblee"). The council pronounces it as a [RESOLUTION] of type verdict whose detail says convict or acquit; this
// reads that detail. And a trial that rises WITHOUT a verdict (the player closed the window, or a save interrupted the
// sitting) is not an escape: the council deliberates without the accused, and its members' own regard for them decides.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
   /// <summary>Reads a trial's verdict, and decides one when the council rose without it. Pure.</summary>
   public static class TrialVerdictPolicy
   {
      /// <summary>
      ///   True when the verdict's words convict. Acquittal is read FIRST, since "not guilty" contains "guilty"; words
      ///   that say neither convict nobody (a garbled verdict never jails the player).
      /// </summary>
      public static bool IsConviction(string detail)
      {
         string d = detail?.ToLowerInvariant() ?? "";
         if (d.Contains("acquit") || d.Contains("not guilty") || d.Contains("innocent") || d.Contains("released") || d.Contains("goes free")) return false;

         return d.Contains("convict") || d.Contains("guilty") || d.Contains("condemn") || d.Contains("sentence");
      }

      /// <summary>
      ///   The council's judgement when it rose without pronouncing one: convicted when its members, on the mod's
      ///   own regard ledger, stand against the accused on average; acquitted otherwise. Closing the window never
      ///   buys an acquittal from a hostile bench.
      /// </summary>
      public static bool DefaultConviction(IEnumerable<int> seatRegards)
      {
         List<int> regards = seatRegards?.ToList() ?? new List<int>();

         return regards.Count > 0 && regards.Average() < 0;
      }
   }
}
