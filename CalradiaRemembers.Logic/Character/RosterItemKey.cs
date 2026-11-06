// Code written by Gabriel Mailhot, 05/11/2026.
// Which piece of the party's goods the player means, quality included. avalanche24 (Nexus, 05/11/2026): "I gave one of my
// companions a Masterwork Noble Bow. Firstly, it didn't show up in the gift selection window that it was 'Masterwork',
// just a normal 'Noble Bow'. Secondly, when I gifted this to my companion, it lost its Masterwork quality." The bag and
// every hand-over named goods by the item alone, so the quality (the game's item modifier) was dropped on the way, and
// with a plain and a fine piece of the same item, either one could leave. A key now names the item AND its quality; a key
// written without one (the model's words, an older offer) still finds the item, the plain piece first. Pure.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>One stack the party holds, as the key reads it.</summary>
   public sealed class HeldGoods
   {
      /// <summary>The item's id.</summary>
      public string ItemId { get; init; } = "";

      /// <summary>The id of its quality (the game's item modifier); blank for a plain piece.</summary>
      public string ModifierId { get; init; } = "";
   }

   /// <summary>Names one stack of goods by item and quality, and finds it again among what the party holds.</summary>
   public static class RosterItemKey
   {
      /// <summary>Stands between the item id and the quality id; neither id ever holds it.</summary>
      public const char Separator = '@';

      /// <summary>The key of an item of this quality: the bare item id for a plain piece.</summary>
      public static string Of(string itemId, string modifierId)
         => string.IsNullOrWhiteSpace(modifierId) ? itemId ?? "" : $"{itemId}{Separator}{modifierId}";

      /// <summary>The item id a key names, with or without a quality.</summary>
      public static string ItemIdOf(string key)
      {
         if (string.IsNullOrEmpty(key)) return "";
         int at = key.IndexOf(Separator);

         return at < 0 ? key : key.Substring(0, at);
      }

      /// <summary>
      ///   The index of the stack a key means among <paramref name="held" />, or -1. The very piece first (item and
      ///   quality); a key without a quality takes the plain piece, else the first of that item; a key with a quality the
      ///   party no longer holds finds nothing, so a fine piece is never swapped for another.
      /// </summary>
      public static int Pick(IReadOnlyList<HeldGoods> held, string key)
      {
         if (held == null || string.IsNullOrWhiteSpace(key)) return -1;
         string itemId = ItemIdOf(key);
         bool bare = key.IndexOf(Separator) < 0;
         int sameItem = -1;
         for (var i = 0; i < held.Count; i++)
         {
            HeldGoods g = held[i];
            if (g == null || g.ItemId != itemId) continue;
            if (Of(g.ItemId, g.ModifierId) == key) return i;
            if (bare && sameItem < 0) sameItem = i;
         }

         return sameItem;
      }
   }
}
