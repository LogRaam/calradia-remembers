// Code written by Gabriel Mailhot, 06/10/2026.
// A tale may ask for its giver or its village to be of a culture, one or several (tashmetu, Nexus, 06/10/2026: a tale
// about Battanian woods told by a Battanian lord). The author writes the culture's id ("battania") or its name; either
// matches, whatever the case. Asking for none matches everyone, as before.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>
   ///   Whether a giver or a village is of the culture a tale asks for (id or name, any case; none asked is any). The
   ///   giver's realm is matched the same way (Gabriel, 18/10/2026): one id-or-name rule for both, so they cannot drift.
   /// </summary>
   public static class AuthoredCulturePolicy
   {
      public static bool Matches(IReadOnlyList<string> wanted, string cultureId, string cultureName)
      {
         List<string> asked = wanted?.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList();
         if (asked == null || asked.Count == 0) return true;

         return asked.Any(w => string.Equals(w, cultureId, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(w, cultureName, StringComparison.OrdinalIgnoreCase));
      }

      /// <summary>"battania or sturgia", for the author's why-not line.</summary>
      public static string Describe(IReadOnlyList<string> wanted)
         => string.Join(" or ", wanted?.Where(w => !string.IsNullOrWhiteSpace(w)) ?? Enumerable.Empty<string>());
   }
}
