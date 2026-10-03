// Code written by Gabriel Mailhot, 03/10/2026.
// Shinelegacy (Nexus, 03/10/2026): memories typed at the console before the console fix were stored as mojibake.
// Gabriel asked for one command that mends the whole memory. This mends one profile; ConsoleTextRepair decides, and
// leaves alone anything that was not mangled.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Mends a profile's memories mangled by the console. Pure.</summary>
   public static class MemoryAccentRepair
   {
      /// <summary>Repairs each mangled memory in place, keeping all it carries besides its words; returns how many.</summary>
      public static int Repair(NpcProfile profile)
      {
         if (profile?.Events == null) return 0;

         var mended = 0;
         for (var i = 0; i < profile.Events.Count; i++)
         {
            NotableEvent e = profile.Events[i];
            if (e?.summary == null) continue;

            string repaired = ConsoleTextRepair.Repair(e.summary);
            if (repaired == e.summary) continue;

            profile.Events[i] = e with {summary = repaired};
            mended++;
         }

         return mended;
      }
   }
}
