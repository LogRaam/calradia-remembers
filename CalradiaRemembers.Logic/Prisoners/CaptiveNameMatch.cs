// Code written by Gabriel Mailhot, 04/11/2026.
// froggyluv (Nexus, 04/11/2026): a lady agreed to buy one of the player's Vlandian captives, and the sale failed with
// "Eilidh holds no captive called Elthild of dey Tihr. Nothing is paid." The captive was matched only when the name the
// model wrote was CONTAINED in the hero's name; the model wrote the name with the house ("Elthild of dey Tihr"), the game
// knows her as "Elthild", so the captive the player plainly held was not found. This also accepts the hero's whole name
// standing as words inside what was written.

#region

using System;

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
   }
}
