// Code written by Gabriel Mailhot, 01/11/2026.
// RaveN017Mod (Nexus, 01/11/2026): Rhagaea offered 20,000 denars for a prisoner with nothing telling her what that was worth.
// Gabriel, same day: "Il faudrait que le modele connaisse la valeur des choses avant de proposer un montant. Si le modele a
// une table de reference avec des exemples, il pourra offrir un montant juste." The figures are read from the running game,
// so a modded economy reads true, and this only words them.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Real prices read from the game, in denars. Zero means unread, and that line is left out.</summary>
   public sealed class PriceFacts
   {
      /// <summary>A sack of grain.</summary>
      public int Grain { get; init; }

      /// <summary>A trained soldier's pay for a day.</summary>
      public int SoldierPay { get; init; }

      /// <summary>A veteran's pay for a day.</summary>
      public int VeteranPay { get; init; }

      /// <summary>An ordinary riding horse.</summary>
      public int RidingHorse { get; init; }

      /// <summary>A war horse.</summary>
      public int WarHorse { get; init; }

      /// <summary>A good sword.</summary>
      public int GoodSword { get; init; }

      /// <summary>The ransom of an ordinary lord.</summary>
      public int LordRansom { get; init; }

      /// <summary>The ransom of a ruler.</summary>
      public int RulerRansom { get; init; }
   }

   /// <summary>Words the price table the model reasons from before it names a sum. Pure.</summary>
   public static class PriceReference
   {
      /// <summary>The table for a full prompt, or null when nothing was read.</summary>
      public static string Full(PriceFacts f)
      {
         var lines = new List<string>();
         Add(lines, "a sack of grain", f?.Grain ?? 0, "");
         Add(lines, "a soldier's pay", f?.SoldierPay ?? 0, " a day");
         Add(lines, "a veteran's pay", f?.VeteranPay ?? 0, " a day");
         Add(lines, "a riding horse", f?.RidingHorse ?? 0, "");
         Add(lines, "a war horse", f?.WarHorse ?? 0, "");
         Add(lines, "a good sword", f?.GoodSword ?? 0, "");
         Add(lines, "a lord's ransom", f?.LordRansom ?? 0, "");
         Add(lines, "a ruler's ransom", f?.RulerRansom ?? 0, "");
         if (lines.Count == 0) return null;

         var sb = new StringBuilder();
         sb.AppendLine("WHAT THINGS ARE WORTH IN CALRADIA, in denars:");
         foreach (string line in lines) sb.AppendLine("- " + line);
         sb.Append("Reason from these before you name a sum, and name none out of proportion to what it pays for.");

         return sb.ToString();
      }

      /// <summary>The table in one line for a compact prompt, or null when nothing was read.</summary>
      public static string Compact(PriceFacts f)
      {
         var parts = new List<string>();
         Add(parts, "soldier's pay", f?.SoldierPay ?? 0, " a day");
         Add(parts, "war horse", f?.WarHorse ?? 0, "");
         Add(parts, "lord's ransom", f?.LordRansom ?? 0, "");
         if (parts.Count == 0) return null;

         return "PRICES in denars: " + string.Join(", ", parts) + ". Name sums in proportion to these.";
      }

      /// <summary>A price as someone would say it: two significant figures, so 2347 reads 2300 and the table does not flicker.</summary>
      public static int Rounded(int value)
      {
         if (value < 100) return value;

         var step = (int) Math.Pow(10, Math.Floor(Math.Log10(value)) - 1);

         return value / step * step;
      }

      private static void Add(List<string> lines, string name, int price, string unit)
      {
         if (price > 0) lines.Add($"{name}: {Rounded(price).ToString("N0", CultureInfo.InvariantCulture)}{unit}");
      }
   }
}
