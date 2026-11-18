// Code written by Gabriel Mailhot, 18/11/2026.
// How cr.cost_bench prices a call on any provider. Gabriel (18/11/2026): "pour le benchmark, il faudrait pouvoir
// l'exécuter sur d'autres providers que OpenRouter.. des joueurs utilisent des setup locaux, d'autres Player2, NanoGPT".
// The bench refused every provider but OpenRouter because its prices came only from OpenRouter's own bill (usage.cost).
// Every provider the mod speaks to reports the tokens it read, cached and wrote, so accuracy, speed and cache work
// everywhere; only the price needs a source. In order: the provider's own bill when it sends one; else the row's own
// prices per million tokens; else the row says the model is free (a local server). A paid provider with no known price
// is not run at all: the spend cap could not hold, and a player would spend without a limit.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>The price fields a row closes with.</summary>
   public sealed class RowPrice
   {
      /// <summary>The row wrote <c>free</c>.</summary>
      public bool Free { get; init; }

      /// <summary>Dollars per million tokens sent; null when the row names none.</summary>
      public double? In { get; init; }

      /// <summary>Dollars per million tokens read from cache.</summary>
      public double? Cached { get; init; }

      /// <summary>Dollars per million tokens written.</summary>
      public double? Out { get; init; }
   }

   /// <summary>The tokens a provider reported for one call.</summary>
   public sealed class CallTokens
   {
      /// <summary>Tokens sent.</summary>
      public int Prompt { get; init; }

      /// <summary>Of those, read from cache.</summary>
      public int Cached { get; init; }

      /// <summary>Tokens written, thinking included.</summary>
      public int Completion { get; init; }
   }

   /// <summary>Reads a row's prices and prices a call from them. Pure.</summary>
   public static class CostPricing
   {
      /// <summary>The word that marks a free model in a row.</summary>
      public const string FreeWord = "free";

      /// <summary>Whether a row's field is a price field (<c>free</c>, or <c>$...</c>) rather than a positional one.</summary>
      public static bool IsPriceField(string field)
      {
         string f = (field ?? "").Trim();

         return string.Equals(f, FreeWord, StringComparison.OrdinalIgnoreCase) || f.StartsWith("$", StringComparison.Ordinal);
      }

      /// <summary>
      ///   Reads the price fields among <paramref name="fields" />: <c>free</c>, <c>$sent/cached/written</c> or <c>$sent/written</c>
      ///   (cached then costs what sent does), dollars per million tokens. False, with <paramref name="problem" />, when a price
      ///   field cannot be read or a row is both free and priced.
      /// </summary>
      public static bool TryRead(IEnumerable<string> fields, out RowPrice price, out string problem)
      {
         bool free = false;
         double? inPrice = null, cachedPrice = null, outPrice = null;
         price = new RowPrice();
         problem = "";
         foreach (string raw in (fields ?? Enumerable.Empty<string>()).Where(IsPriceField))
         {
            string f = raw.Trim();
            if (string.Equals(f, FreeWord, StringComparison.OrdinalIgnoreCase))
            {
               free = true;
               continue;
            }

            string[] parts = f.Substring(1).Split('/').Select(p => p.Trim().TrimStart('$')).ToArray();
            var values = new List<double>();
            foreach (string p in parts)
               if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 0) values.Add(v);
            if (values.Count != parts.Length || (values.Count != 2 && values.Count != 3))
            {
               problem = $"prices must read $sent/cached/written or $sent/written, dollars per million tokens (got {f})";

               return false;
            }

            inPrice = values[0];
            cachedPrice = values.Count == 3 ? values[1] : values[0];
            outPrice = values[values.Count - 1];
         }

         if (free && inPrice != null)
         {
            problem = "a row is either free or priced, not both";

            return false;
         }

         price = new RowPrice {Free = free, In = inPrice, Cached = cachedPrice, Out = outPrice};

         return true;
      }

      /// <summary>
      ///   The price of one call: the provider's bill when it sent one; else the row's prices times the tokens (cached tokens
      ///   at the cached price); else nothing for a free row; else unknown.
      /// </summary>
      public static (double? Cost, PriceSource Source) Price(CostScenario row, CallTokens tokens, double? billed)
      {
         if (billed != null) return (billed, PriceSource.Bill);
         tokens ??= new CallTokens();
         if (row?.InPerMillion != null && row.OutPerMillion != null)
         {
            int cached = Math.Max(0, Math.Min(tokens.Cached, tokens.Prompt));
            double cost = ((tokens.Prompt - cached) * row.InPerMillion.Value + cached * (row.CachedPerMillion ?? row.InPerMillion.Value)
                           + tokens.Completion * row.OutPerMillion.Value) / 1_000_000.0;

            return (cost, PriceSource.RowPrices);
         }

         return row?.Free == true ? (0.0, PriceSource.Free) : ((double?) null, PriceSource.None);
      }

      /// <summary>
      ///   Whether a row can run under the spend cap: its provider bills each call itself, or the row names its prices, or the row
      ///   is free. Otherwise its spending could not be counted, so it is not run.
      /// </summary>
      public static bool CanCap(CostScenario row, bool providerBills)
         => providerBills || row != null && (row.Free || row.InPerMillion != null && row.OutPerMillion != null);
   }
}
