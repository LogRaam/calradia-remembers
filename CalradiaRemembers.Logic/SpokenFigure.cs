// Code written by Gabriel Mailhot, 27/09/2026.
// The last sum a speaker named, in digits ("11,500") or in English words ("eleven thousand five hundred").
//
// tashmetu (Nexus, 27/09/2026, on 2.5.25): an envoy's "final offer of eleven thousand five hundred" was accepted with "i
// will return him", and the captive went home for nothing, because a reply naming no sum was read as a gift. A plain
// acceptance closes at the last offer on the table, and that offer is often written out in words.

#region

using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Reads the last sum named in a line of speech. Pure.</summary>
   public static class SpokenFigure
   {
      private static readonly IReadOnlyDictionary<string, int> Units = new Dictionary<string, int> {
         ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7,
         ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14,
         ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19,
         ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70,
         ["eighty"] = 80, ["ninety"] = 90
      };

      private static readonly Regex Tokens = new(@"\d[\d,]*|[a-z]+", RegexOptions.Compiled);

      /// <summary>
      ///   The last sum of at least <paramref name="minimum" /> named in <paramref name="text" />, or null. The minimum
      ///   keeps ordinary small numbers ("two or three days", "no one") from reading as an offer.
      /// </summary>
      public static int? LastIn(string text, int minimum)
      {
         if (string.IsNullOrWhiteSpace(text)) return null;

         int? last = null;
         long total = 0, current = 0;
         var inWords = false;

         void Close()
         {
            if (inWords)
            {
               long value = total + current;
               if (value >= minimum && value <= int.MaxValue) last = (int) value;
            }

            inWords = false;
            total = 0;
            current = 0;
         }

         foreach (Match m in Tokens.Matches(text.ToLowerInvariant()))
         {
            string token = m.Value;

            if (char.IsDigit(token[0]))
            {
               Close();
               if (long.TryParse(token.Replace(",", ""), out long n) && n >= minimum && n <= int.MaxValue) last = (int) n;

               continue;
            }

            if (Units.TryGetValue(token, out int unit))
            {
               current += unit;
               inWords = true;
            }
            else if (token == "hundred" && inWords) current = (current == 0 ? 1 : current) * 100;
            else if (token == "thousand" && inWords)
            {
               total += (current == 0 ? 1 : current) * 1000;
               current = 0;
            }
            else if (token == "and" && inWords) { }
            else Close();
         }

         Close();

         return last;
      }
   }
}
