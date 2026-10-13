// Code written by Gabriel Mailhot, 13/10/2026.
// One sale read once.
//
// 3chmidt (Nexus, 13/10/2026): selling a sword, the interpreter emitted a give_item and a gold move for one sale, and
// guessed the gold's direction wrong ("The coin is yours." became "Give Poison Menja 9000 coins? [Y/n]"); later "the
// game just takes another sword out of my inventory". With sell_item in the vocabulary a model can still emit the sale
// AND its parts. The gold half is the existing rule (TurnPaymentPolicy: a deal moving its own coin drops any give_gold
// or take_gold of the same turn; sell_item is one of those deals). This is the item half: beside a sell_item, a
// give_item of the same thing is the sale read twice, and is dropped before anything executes.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>One action of a turn, as far as a sale is concerned.</summary>
   public sealed class TradeBatchAction
   {
      /// <summary>The action type ("sell_item", "give_item", "give_gold", "take_gold", ...).</summary>
      public string Type { get; init; }

      /// <summary>The item it names, or null.</summary>
      public string Item { get; init; }
   }

   /// <summary>Which actions of a turn a sale makes redundant. Pure.</summary>
   public static class TradeBatchPolicy
   {
      private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase) {"the", "of", "and", "a", "an", "my", "your", "this", "that"};

      /// <summary>
      ///   The indexes to drop: beside each sell_item, any give_item naming the same thing (a shared word). Nothing when
      ///   the turn holds no sale. (A gold move beside the sale is TurnPaymentPolicy's.)
      /// </summary>
      public static IReadOnlyCollection<int> Superseded(IReadOnlyList<TradeBatchAction> batch)
      {
         var dropped = new SortedSet<int>();
         if (batch == null) return dropped;

         foreach (TradeBatchAction sale in batch.Where(a => Is(a, "sell_item")))
            for (var i = 0; i < batch.Count; i++)
            {
               TradeBatchAction a = batch[i];
               if (a == null || ReferenceEquals(a, sale)) continue;
               if (Is(a, "give_item") && SameThing(a.Item, sale.Item)) dropped.Add(i);
            }

         return dropped;
      }

      #region private

      private static bool Is(TradeBatchAction a, string type) => a != null && string.Equals(a.Type, type, StringComparison.OrdinalIgnoreCase);

      private static bool SameThing(string a, string b)
      {
         HashSet<string> left = Words(a);

         return left.Count > 0 && Words(b).Overlaps(left);
      }

      private static HashSet<string> Words(string s)
         => new((s ?? "").Split(new[] {' ', '-', ',', '.', '\'', '"'}, StringSplitOptions.RemoveEmptyEntries)
                         .Where(w => w.Length >= 3 && !Filler.Contains(w)),
                StringComparer.OrdinalIgnoreCase);

      #endregion
   }
}
