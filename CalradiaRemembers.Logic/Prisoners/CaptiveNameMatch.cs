// Code written by Gabriel Mailhot, 04/11/2026.
// froggyluv (Nexus, 04/11/2026): a lady agreed to buy one of the player's Vlandian captives, and the sale failed with
// "Eilidh holds no captive called Elthild of dey Tihr. Nothing is paid." The captive was matched only when the name the
// model wrote was CONTAINED in the hero's name; the model wrote the name with the house ("Elthild of dey Tihr"), the game
// knows her as "Elthild", so the captive the player plainly held was not found. This also accepts the hero's whole name
// standing as words inside what was written.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Prisoners
{
   /// <summary>Whether a name written in conversation points at a captive, tolerant of a house or title around it.</summary>
   public static class CaptiveNameMatch
   {
      /// <summary>
      ///   True when <paramref name="written" /> is part of <paramref name="heroName" /> (the old rule), or the hero's
      ///   whole name stands as words inside it ("Elthild of dey Tihr", "Lady Elthild"); never a name inside a longer word.
      /// </summary>
      public static bool Matches(string heroName, string written)
      {
         if (string.IsNullOrWhiteSpace(heroName) || string.IsNullOrWhiteSpace(written)) return false;

         string name = heroName.Trim();
         string query = written.Trim();
         if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;

         for (int at = query.IndexOf(name, StringComparison.OrdinalIgnoreCase); at >= 0;
              at = query.IndexOf(name, at + 1, StringComparison.OrdinalIgnoreCase))
         {
            int end = at + name.Length;
            bool startsWord = at == 0 || !char.IsLetter(query[at - 1]);
            bool endsWord = end == query.Length || !char.IsLetter(query[end]);
            if (startsWord && endsWord) return true;
         }

         return false;
      }

      /// <summary>
      ///   Which of <paramref name="heroNames" /> a name written in conversation points at: the one it spells exactly, else the
      ///   only one it <see cref="Matches" />; -1 when none, or when it could be two of them (RaveN017Mod, Nexus, 05/11/2026:
      ///   several captives sold in a row; the first loose match sold whoever came first in the roster). Refusing a name that
      ///   could be two captives costs a line; selling the wrong one costs a captive.
      /// </summary>
      public static int Best(IReadOnlyList<string> heroNames, string written)
      {
         if (heroNames == null || string.IsNullOrWhiteSpace(written)) return -1;

         string query = written.Trim();
         int exact = -1, loose = -1, looseCount = 0;
         for (int i = 0; i < heroNames.Count; i++)
         {
            string name = heroNames[i]?.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
            {
               if (exact >= 0) return -1; // two captives of one name: no way to tell them apart
               exact = i;
            }
            else if (Matches(name, query))
            {
               loose = i;
               looseCount++;
            }
         }

         if (exact >= 0) return exact;

         return looseCount == 1 ? loose : -1;
      }
   }
}
