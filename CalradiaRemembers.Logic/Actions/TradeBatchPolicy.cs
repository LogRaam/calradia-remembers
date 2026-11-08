// Code written by Gabriel Mailhot, 13/10/2026.
// One sale read once.
//
// 3chmidt (Nexus, 13/10/2026): selling a sword, the interpreter emitted a give_item and a gold move for one sale, and
// guessed the gold's direction wrong ("The coin is yours." became "Give Poison Menja 9000 coins? [Y/n]"); later "the
// game just takes another sword out of my inventory". With sell_item in the vocabulary a model can still emit the sale
// AND its parts. The gold half is the existing rule (TurnPaymentPolicy: a deal moving its own coin drops any give_gold
// or take_gold of the same turn; sell_item is one of those deals). This is the item half: beside a sell_item, a
// give_item of the same thing is the sale read twice, and is dropped before anything executes.
//
// 3chmidt again (Nexus, 06/11/2026), with screenshots: Yfinja agreed 9,600 for an axe and the game paid 100, because the
// price came as a give_gold, which is judged as a gift and capped by regard (GiftCeilingPolicy). No sell_item was in
// the turn, so nothing above applied. Gabriel's go (08/11/2026) on the proposal: when the character hands coin over
// and the player hands an item over in the same turn, that is one sale (SaleFromParts), paid as a sale and confirmed
// as one. It needs a real item changing hands in that same turn, so it cannot draw coin for nothing.

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

      /// <summary>The "amount" parameter as written (a give_gold's sum), or null.</summary>
      public string Amount { get; init; }

      /// <summary>The "count" parameter as written (how many of the item), or null.</summary>
      public string Count { get; init; }

      /// <summary>The "actor" parameter (a council member the action belongs to), or null for the speaker.</summary>
      public string Actor { get; init; }
   }

   /// <summary>A give_gold and a give_item of one turn read as the one sale they are.</summary>
   public sealed class SaleFromParts
   {
      /// <summary>Index of the give_gold in the turn: its coin is the price.</summary>
      public int GoldIndex { get; init; }

      /// <summary>Index of the give_item in the turn: its item is what is sold.</summary>
      public int ItemIndex { get; init; }

      /// <summary>The item named, as the give_item wrote it.</summary>
      public string Item { get; init; }

      /// <summary>The price: the coin the give_gold handed over.</summary>
      public int Price { get; init; }
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
      ///   The sale a turn's parts make: exactly one give_gold (the character's coin to the player, a positive sum) and
      ///   exactly one give_item (the player's item to the character) and no sell_item already. Null otherwise, and
      ///   null when either belongs to someone else at a council table, or when the give_item moves several of a thing
      ///   named in words (a sale from words moves one, so the price for many would buy one); from the bag the amount
      ///   set there decides, as for any sale.
      /// </summary>
      public static SaleFromParts PartsOfASale(TradeBatchFacts f)
      {
         IReadOnlyList<TradeBatchAction> batch = f?.Actions;
         if (batch == null || batch.Any(a => Is(a, "sell_item"))) return null;

         List<int> gold = IndexesOf(batch, "give_gold");
         List<int> item = IndexesOf(batch, "give_item");
         if (gold.Count != 1 || item.Count != 1) return null;

         TradeBatchAction coin = batch[gold[0]];
         TradeBatchAction thing = batch[item[0]];
         if (!string.IsNullOrWhiteSpace(coin.Actor) || !string.IsNullOrWhiteSpace(thing.Actor)) return null;
         if (!int.TryParse(coin.Amount?.Trim(), out int price) || price <= 0) return null;
         if (!f.ItemPickedFromBag && int.TryParse(thing.Count?.Trim(), out int count) && count > 1) return null;

         return new SaleFromParts {GoldIndex = gold[0], ItemIndex = item[0], Item = thing.Item, Price = price};
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

      private static List<int> IndexesOf(IReadOnlyList<TradeBatchAction> batch, string type)
         => Enumerable.Range(0, batch.Count).Where(i => Is(batch[i], type)).ToList();

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
