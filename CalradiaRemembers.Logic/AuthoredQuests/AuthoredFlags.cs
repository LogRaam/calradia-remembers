// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md): flags are Twine's variables, cut down to what a tale needs. A
// flag is a name, set by a choice taken, a finding revealed or a person met, and tested by "if": "name" holds when
// set, "!name" when not. No arithmetic and no text: set and test only, so a file can always be checked by reading it.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Tests a list of flag conditions against the flags a run has set. Pure.</summary>
   public static class AuthoredFlags
   {
      /// <summary>True when every condition holds; an empty list always holds.</summary>
      public static bool Hold(IEnumerable<string> conditions, ICollection<string> flags)
      {
         foreach (string raw in conditions ?? Enumerable.Empty<string>())
         {
            string c = (raw ?? "").Trim();
            if (c.Length == 0) continue;
            bool negated = c.StartsWith("!");
            string name = c.TrimStart('!').Trim();
            bool set = flags != null && flags.Contains(name);
            if (set == negated) return false;
         }

         return true;
      }

      /// <summary>The lines whose flags hold, in their order.</summary>
      public static IEnumerable<string> Said(IEnumerable<AuthoredLine> lines, ICollection<string> flags)
         => (lines ?? Enumerable.Empty<AuthoredLine>()).Where(l => l != null && !string.IsNullOrWhiteSpace(l.Text) && Hold(l.If, flags)).Select(l => l.Text);
   }
}
