// Code written by Gabriel Mailhot, 05/11/2026.
// The party's goods read with their quality. avalanche24 (Nexus, 05/11/2026): a Masterwork Noble Bow showed as a plain
// "Noble Bow" in the gift bag and was worn as a plain one by the companion who received it. Every place that shows,
// finds, values or hands over a piece of the party's goods asks here, so the item modifier travels with it
// (RosterItemKey decides which stack a key means).

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Character;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Items
{
   /// <summary>Keys, names, values and lookups of the party's goods with their quality.</summary>
   public static class RosterGoods
   {
      /// <summary>The key of this very piece: its item and its quality.</summary>
      public static string KeyOf(EquipmentElement goods) => RosterItemKey.Of(goods.Item?.StringId, goods.ItemModifier?.StringId);

      /// <summary>Its name as the game shows it, quality first ("Masterwork Noble Bow").</summary>
      public static string NameOf(EquipmentElement goods)
         => goods.Item == null ? "" : goods.GetModifiedItemName()?.ToString() ?? goods.Item.Name?.ToString() ?? "";

      /// <summary>What one of it is worth, its quality's price multiplier included.</summary>
      public static int ValueOf(EquipmentElement goods) => goods.Item == null ? 0 : goods.GetBaseValue();

      /// <summary>The item a key names, whatever its quality; null when the game has no such item.</summary>
      public static ItemObject ItemOf(string key)
      {
         string id = RosterItemKey.ItemIdOf(key);

         return string.IsNullOrEmpty(id) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(id);
      }

      /// <summary>The stack a key means among the stacks still held (amount above 0); default when none.</summary>
      public static ItemRosterElement Find(ItemRoster roster, string key)
      {
         if (roster == null || string.IsNullOrWhiteSpace(key)) return default;
         List<ItemRosterElement> held = roster.Where(e => e.Amount > 0 && e.EquipmentElement.Item != null).ToList();
         int at = RosterItemKey.Pick(held.Select(e => new HeldGoods {ItemId = e.EquipmentElement.Item.StringId, ModifierId = e.EquipmentElement.ItemModifier?.StringId ?? ""}).ToList(), key);

         return at < 0 ? default : held[at];
      }

      /// <summary>True when <paramref name="key" /> means this very stack (a key without quality means its plain piece).</summary>
      public static bool Means(string key, EquipmentElement goods)
      {
         if (string.IsNullOrWhiteSpace(key) || goods.Item == null) return false;
         var one = new List<HeldGoods> {new HeldGoods {ItemId = goods.Item.StringId, ModifierId = goods.ItemModifier?.StringId ?? ""}};

         return RosterItemKey.Pick(one, key) == 0;
      }
   }
}
