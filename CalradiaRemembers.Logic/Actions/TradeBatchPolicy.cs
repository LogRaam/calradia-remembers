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

      /// <summary>The roster item it resolves to (its id), or null when unknown.</summary>
      public string ResolvedId { get; init; }
   }

   /// <summary>A turn's actions, and whether the player picked the item from the bag this turn.</summary>
   public sealed class TradeBatchFacts
   {
      /// <summary>The turn's actions, in order.</summary>
      public IReadOnlyList<TradeBatchAction> Actions { get; init; } = Array.Empty<TradeBatchAction>();

      /// <summary>
      ///   The player picked an item from the bag this turn: then a give_item and a sell_item beside it both resolve to
      ///   that one item, whatever words the model used for it.
      /// </summary>
      public bool ItemPickedFromBag { get; init; }
   }

   /// <summary>Which actions of a turn a sale makes redundant. Pure.</summary>
   public static class TradeBatchPolicy
   {
      private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase) {"the", "of", "and", "a", "an", "my", "your", "this", "that"};

      /// <summary>
      ///   The indexes to drop: beside each sell_item, any give_item of the same item. The same item is: any item at all
      ///   when the player picked it from the bag this turn (both resolve to the pick, "blade" and "sword" alike); else
      ///   the same resolved roster item when both are known; else a shared word. Nothing when the turn holds no sale.
      ///   (A gold move beside the sale is TurnPaymentPolicy's.)
      /// </summary>
      public static IReadOnlyCollection<int> Superseded(TradeBatchFacts f)
      {
         var dropped = new SortedSet<int>();
         IReadOnlyList<TradeBatchAction> batch = f?.Actions;
         if (batch == null) return dropped;

         foreach (TradeBatchAction sale in batch.Where(a => Is(a, "sell_item")))
            for (var i = 0; i < batch.Count; i++)
            {
               TradeBatchAction a = batch[i];
               if (a == null || ReferenceEquals(a, sale)) continue;
               if (Is(a, "give_item") && SameItem(a, sale, f.ItemPickedFromBag)) dropped.Add(i);
            }

         return dropped;
      }

      /// <summary>
      ///   The indexes to drop beside a counter_offer in the same turn: free_prisoner (counter_offer's own executor
      ///   frees her, with every term already validated against what is open now) and join_clan (fealty rides as
      ///   counter_offer's own "fealty" term, never a separate join_clan). Nothing when the turn holds no
      ///   counter_offer. Mirrors <see cref="Superseded" />'s shape (a set of indexes into the caller's own list),
      ///   but on bare action TYPES: a counter_offer's redundancy is never about which ITEM two actions share.
      /// </summary>
      public static IReadOnlyCollection<int> SupersededByCounterOffer(IReadOnlyList<string> actionTypesThisTurn)
      {
         var dropped = new SortedSet<int>();
         if (actionTypesThisTurn == null) return dropped;
         if (!actionTypesThisTurn.Any(t => string.Equals(t, "counter_offer", StringComparison.OrdinalIgnoreCase))) return dropped;

         for (var i = 0; i < actionTypesThisTurn.Count; i++)
         {
            string t = actionTypesThisTurn[i];
            // recruit_prisoner too: it runs unconfirmed and would free and recruit her before the player answers the offer,
            // whose own service term then finds her no longer a prisoner (found in review, 14/10/2026).
            if (string.Equals(t, "free_prisoner", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "join_clan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "recruit_prisoner", StringComparison.OrdinalIgnoreCase))
               dropped.Add(i);
         }

         return dropped;
      }

      #region private

      private static bool Is(TradeBatchAction a, string type) => a != null && string.Equals(a.Type, type, StringComparison.OrdinalIgnoreCase);

      private static bool SameItem(TradeBatchAction gift, TradeBatchAction sale, bool pickedFromBag)
      {
         if (pickedFromBag) return true;
         if (!string.IsNullOrEmpty(gift.ResolvedId) && !string.IsNullOrEmpty(sale.ResolvedId))
            return string.Equals(gift.ResolvedId, sale.ResolvedId, StringComparison.Ordinal);

         return SameThing(gift.Item, sale.Item);
      }

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
