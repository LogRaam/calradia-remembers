// Code written by Gabriel Mailhot, 08/07/2026.
// Personal supply quests (referential increment 2): maps a canonical ProvideGoods category key (from the pure
// CalradiaRemembers.Logic.ProvideGoodsPolicy) to actual party items at RUNTIME. Item categories are only
// populated once the game has loaded, so this cannot be previewed standalone; matching is by the reliable
// ItemType for horses and by the standard vanilla ItemCategory StringIds for livestock and grain.

#region

using CalradiaRemembers.Logic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Items
{
    /// <summary>Whether a party item counts toward a <c>ProvideGoods</c> supply category.</summary>
    internal static class SupplyItemMatcher
    {
        public static bool Matches(ItemObject item, string categoryKey)
        {
            if (item == null || string.IsNullOrEmpty(categoryKey)) return false;

            string cat = item.ItemCategory?.StringId?.ToLowerInvariant() ?? "";

            switch (categoryKey)
            {
                case ProvideGoodsPolicy.Horses:
                    // Riding and war horses count; pack animals (mules) do not fill a cavalry demand.
                    return item.ItemType == ItemObject.ItemTypeEnum.Horse
                           && !cat.Contains("pack") && cat != "mule";
                case ProvideGoodsPolicy.Livestock:
                    return cat is "cow" or "sheep" or "hog" or "chicken" or "goose" or "pig" or "goat" or "cattle";
                case ProvideGoodsPolicy.Grain:
                    return cat == "grain";
                case ProvideGoodsPolicy.Food:
                    // Whatever the game itself counts as food (fearente, 29/10/2026: fish and meat did not count).
                    return item.IsFood;
                default:
                    // A weapon the player forged, of the kind asked (Lordfadooboo, 01/10/2026).
                    if (CraftWeaponPolicy.KindOfKey(categoryKey) != null)
                        return CraftWeaponPolicy.Answers(item.IsCraftedByPlayer, item.WeaponComponent?.PrimaryWeapon?.WeaponClass.ToString(), categoryKey);

                    // One exact item a tale asks for (tashmetu, 20/10/2026, items in tales).
                    string itemId = ProvideGoodsPolicy.ItemIdOf(categoryKey);
                    if (itemId != null) return item.StringId == itemId;

                    // A good a town lacks (fkasad, 01/10/2026): its own item category, exactly.
                    string marketId = ProvideGoodsPolicy.MarketCategoryId(categoryKey);

                    return marketId != null && string.Equals(cat, marketId, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>A short player-facing word for the category, for picker titles and NPC lines.</summary>
        public static string Label(string categoryKey)
            => categoryKey switch {
                ProvideGoodsPolicy.Horses => "horses",
                ProvideGoodsPolicy.Livestock => "livestock",
                ProvideGoodsPolicy.Grain => "grain",
                ProvideGoodsPolicy.Food => "food",
                _ => ForgeLabel(categoryKey) ?? MarketLabel(categoryKey) ?? ItemLabel(categoryKey) ?? "goods"
            };

        private static string ItemLabel(string categoryKey)
        {
            string id = ProvideGoodsPolicy.ItemIdOf(categoryKey);

            return id == null ? null : MBObjectManager.Instance?.GetObject<ItemObject>(id)?.Name?.ToString() ?? id;
        }

        private static string ForgeLabel(string categoryKey)
        {
            string kind = CraftWeaponPolicy.KindOfKey(categoryKey);

            return kind == null ? null : "forged " + CraftWeaponPolicy.Label(kind);
        }

        private static string MarketLabel(string categoryKey)
        {
            string id = ProvideGoodsPolicy.MarketCategoryId(categoryKey);

            return id == null ? null : MBObjectManager.Instance?.GetObject<ItemCategory>(id)?.GetName()?.ToString() ?? id;
        }
    }
}
