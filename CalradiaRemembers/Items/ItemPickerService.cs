// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Character;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Items
{
   /// <summary>
   ///   Shows the player's party items (excluding quest items) so goods can be chosen by hand rather than named:
   ///   the gift bag (several goods, each in a chosen amount), a bargain paid in goods, and a supply task's goods.
   /// </summary>
   public static class ItemPickerService
   {
      /// <summary>Sentinel identifier for the synthetic "Gold" entry pinned atop the give picker.</summary>
      private const string GoldIdentifier = "__cr_give_gold__";

      /// <summary>
      ///   Opens a MULTI-selection picker for paying a bargain in goods. Shows each item's
      ///   whole-stack value so the player can pick enough to clear <paramref name="requiredValue" />,
      ///   and fires <paramref name="onConfirm" /> with one <see cref="ItemOffer" /> per chosen
      ///   item type (the whole stack). The bridge tallies and enforces the value; a short
      ///   offer is rejected there, so this only gathers intent.
      /// </summary>
      public static void ShowBargainPicker(int requiredValue, Action<List<ItemOffer>> onConfirm)
      {
         if (onConfirm == null) return;
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return;

            List<(EquipmentElement goods, int amount)> eligible = BuildEligibleItems();
            if (eligible.Count == 0)
            {
               MBInformationManager.AddQuickInformation(
                  new TextObject("Your party carries no goods to offer."));

               return;
            }

            eligible.Sort(CompareItems);
            var elements = new List<InquiryElement>(eligible.Count);
            foreach ((EquipmentElement goods, int amount) in eligible)
            {
               int stackValue = RosterGoods.ValueOf(goods) * amount;
               string count = amount > 1
                  ? $" ×{amount}"
                  : "";
               var label = $"{RosterGoods.NameOf(goods)}{count}: {stackValue:N0} denars";
               elements.Add(new InquiryElement(RosterGoods.KeyOf(goods), label, null));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Pay in Goods",
                  $"Offer goods worth at least {requiredValue:N0} denars (whole stacks):",
                  elements,
                  true, // isExitShown — Cancel closes without offering
                  1, elements.Count, // pick one up to all
                  "Offer", "Cancel",
                  selected =>
                  {
                     if (selected == null || selected.Count == 0) return;
                     var offers = new List<ItemOffer>(selected.Count);
                     foreach (InquiryElement element in selected)
                     {
                        var id = element.Identifier as string;

                        if (string.IsNullOrEmpty(id)) continue;
                        int held = CountInRoster(id);
                        if (held > 0) offers.Add(new ItemOffer(id, held));
                     }

                     if (offers.Count > 0) onConfirm(offers);
                  },
                  null),
               false,
               true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ItemPickerService] ShowBargainPicker failed: {ex.Message}");
         }
      }

      /// <summary>
      ///   Opens a MULTI-selection picker of the player's goods that match a ProvideGoods supply category
      ///   (horses / livestock / grain), so they can hand over enough to meet <paramref name="requiredCount" />.
      ///   Fires <paramref name="onConfirm" /> with one <see cref="ItemOffer" /> per chosen stack; the bridge
      ///   tallies the count and enforces it (a short offer is rejected there), so this only gathers intent.
      /// </summary>
      public static void ShowSupplyPicker(string categoryKey, int requiredCount, Action<List<ItemOffer>> onConfirm)
      {
         if (onConfirm == null) return;
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return;

            string label = SupplyItemMatcher.Label(categoryKey);
            var eligible = new List<(EquipmentElement goods, int amount)>();
            foreach ((EquipmentElement goods, int amount) in BuildEligibleItems())
               if (SupplyItemMatcher.Matches(goods.Item, categoryKey))
                  eligible.Add((goods, amount));

            if (eligible.Count == 0)
            {
               MBInformationManager.AddQuickInformation(new TextObject($"Your party carries no {label} to hand over."));

               return;
            }

            eligible.Sort(CompareItems);
            var elements = new List<InquiryElement>(eligible.Count);
            foreach ((EquipmentElement goods, int amount) in eligible)
            {
               string count = amount > 1 ? $" ×{amount}" : "";
               elements.Add(new InquiryElement(RosterGoods.KeyOf(goods), $"{RosterGoods.NameOf(goods)}{count}", null));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Hand Over Supplies",
                  $"Hand over at least {requiredCount} {label}:",
                  elements,
                  true,
                  1, elements.Count,
                  "Hand Over", "Cancel",
                  selected =>
                  {
                     if (selected == null || selected.Count == 0) return;
                     var offers = new List<ItemOffer>(selected.Count);
                     foreach (InquiryElement element in selected)
                     {
                        var id = element.Identifier as string;

                        if (string.IsNullOrEmpty(id)) continue;
                        int held = CountInRoster(id);
                        if (held > 0) offers.Add(new ItemOffer(id, held));
                     }

                     if (offers.Count > 0) onConfirm(offers);
                  },
                  null),
               false,
               true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ItemPickerService] ShowSupplyPicker failed: {ex.Message}");
         }
      }

      /// <summary>
      ///   The gift bag: several goods at once, each in the amount the player chooses (fearente, Nexus, 29/10/2026: he
      ///   gave grain, then fish and meat one by one, one unit each). Gold stays its own entry: picked alone, it opens the
      ///   amount of denars; picked with goods, the goods are given and gold is offered on its own afterwards. For each
      ///   good held more than once, a box asks how many, filled with all of it, so Enter gives the whole stack. Fires
      ///   <paramref name="onGoods" /> with every good and its amount once all are answered; Cancel on any box gives
      ///   nothing.
      /// </summary>
      public static void ShowGiftPicker(Action<List<ItemOffer>> onGoods, Action onGoldSelected)
      {
         if (onGoods == null) return;
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return;

            List<(EquipmentElement goods, int amount)> eligible = BuildEligibleItems();
            if (eligible.Count == 0 && onGoldSelected == null)
            {
               MBInformationManager.AddQuickInformation(new TextObject("Your party carries no items available to offer."));

               return;
            }

            eligible.Sort(CompareItems);
            List<InquiryElement> elements = BuildInquiryElements(eligible);
            if (onGoldSelected != null)
            {
               int gold = Hero.MainHero?.Gold ?? 0;
               elements.Insert(0, new InquiryElement(GoldIdentifier, $"Gold (you carry {gold:N0} denars)", null));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Give",
                  "Choose gold, or one or more goods from your party to give (you choose how many of each next):",
                  elements,
                  true,
                  1, elements.Count,
                  "Give", "Cancel",
                  selected =>
                  {
                     if (selected == null || selected.Count == 0) return;
                     List<string> ids = selected.Select(e => e.Identifier as string).Where(id => !string.IsNullOrEmpty(id)).ToList();
                     bool gold = ids.Remove(GoldIdentifier);
                     if (ids.Count == 0)
                     {
                        if (gold) onGoldSelected?.Invoke();

                        return;
                     }

                     if (gold)
                        MBInformationManager.AddQuickInformation(new TextObject("Gold is given on its own: offer it once these goods are handed over."));

                     AskAmounts(ids, 0, new List<ItemOffer>(), onGoods);
                  },
                  null),
               false,
               false); // queued, not prioritized: opened from a parent inquiry's closing callback it would be swallowed
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ItemPickerService] ShowGiftPicker failed: {ex.Message}");
         }
      }

      #region private

      // ── private ──────────────────────────────────────────────────────────

      /// <summary>Asks, one good after another, how many of it to give; fires <paramref name="done" /> after the last.</summary>
      private static void AskAmounts(List<string> ids, int index, List<ItemOffer> chosen, Action<List<ItemOffer>> done)
      {
         if (index >= ids.Count)
         {
            if (chosen.Count > 0) done(chosen);

            return;
         }

         string id = ids[index];
         int held = CountInRoster(id);
         if (held <= 0)
         {
            AskAmounts(ids, index + 1, chosen, done);

            return;
         }

         if (held == 1)
         {
            chosen.Add(new ItemOffer(id, 1));
            AskAmounts(ids, index + 1, chosen, done);

            return;
         }

         string name = NameOf(id);
         InformationManager.ShowTextInquiry(new TextInquiryData(
            "How Many?",
            $"How many {name} do you give? (You carry {held}.)",
            true, true,
            "Give", "Cancel",
            typed =>
            {
               int? amount = GiftBundlePolicy.Amount(typed, held);
               if (amount == null) return;
               chosen.Add(new ItemOffer(id, amount.Value));
               AskAmounts(ids, index + 1, chosen, done);
            },
            null,
            false,
            typed => GiftBundlePolicy.Amount(typed, held) != null
               ? new Tuple<bool, string>(true, "")
               : new Tuple<bool, string>(false, $"A number from 1 to {held}."),
            "",
            held.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            false,
            false);
      }

      /// <summary>A stack's display name from the party roster, quality included, or its key when it cannot be read.</summary>
      private static string NameOf(string key)
      {
         ItemRosterElement el = RosterGoods.Find(MobileParty.MainParty.ItemRoster, key);

         return el.EquipmentElement.Item == null ? key : RosterGoods.NameOf(el.EquipmentElement);
      }

      // One entry per stack, so a Masterwork piece is listed apart from a plain one (avalanche24, 05/11/2026).
      private static List<(EquipmentElement goods, int amount)> BuildEligibleItems()
      {
         var list = new List<(EquipmentElement, int)>();
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;

            if (item is not {Value: > 0}) continue;
            list.Add((el.EquipmentElement, el.Amount));
         }

         return list;
      }

      private static List<InquiryElement> BuildInquiryElements(
         List<(EquipmentElement goods, int amount)> eligible)
      {
         var elements = new List<InquiryElement>(eligible.Count);
         foreach ((EquipmentElement goods, int amount) in eligible)
         {
            string count = amount > 1
               ? $" ×{amount}"
               : "";
            var label = $"{RosterGoods.NameOf(goods)}{count}: {RosterGoods.ValueOf(goods):N0} denars";
            elements.Add(new InquiryElement(RosterGoods.KeyOf(goods), label, null));
         }

         return elements;
      }

      private static int CategoryOrder(ItemObject.ItemTypeEnum type)
      {
         switch (type)
         {
            case ItemObject.ItemTypeEnum.OneHandedWeapon:
            case ItemObject.ItemTypeEnum.TwoHandedWeapon:
            case ItemObject.ItemTypeEnum.Polearm:
               return 0;
            case ItemObject.ItemTypeEnum.Shield:
               return 1;
            case ItemObject.ItemTypeEnum.Bow:
            case ItemObject.ItemTypeEnum.Crossbow:
            case ItemObject.ItemTypeEnum.Thrown:
               return 2;
            case ItemObject.ItemTypeEnum.Arrows:
            case ItemObject.ItemTypeEnum.Bolts:
               return 3;
            case ItemObject.ItemTypeEnum.HeadArmor:
            case ItemObject.ItemTypeEnum.BodyArmor:
            case ItemObject.ItemTypeEnum.LegArmor:
            case ItemObject.ItemTypeEnum.HandArmor:
            case ItemObject.ItemTypeEnum.ChestArmor:
            case ItemObject.ItemTypeEnum.Cape:
               return 4;
            case ItemObject.ItemTypeEnum.Horse:
            case ItemObject.ItemTypeEnum.Animal:
               return 5;
            default:
               return 6; // goods, books, ammunition, etc.
         }
      }

      private static int CompareItems(
         (EquipmentElement goods, int amount) a,
         (EquipmentElement goods, int amount) b)
      {
         int catDiff = CategoryOrder(a.goods.Item.ItemType) - CategoryOrder(b.goods.Item.ItemType);

         if (catDiff != 0) return catDiff;

         return RosterGoods.ValueOf(b.goods) - RosterGoods.ValueOf(a.goods); // value descending within category
      }

      /// <summary>How many of the given stack (item and quality) the player currently holds (0 if none).</summary>
      private static int CountInRoster(string key)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return 0;

         return RosterGoods.Find(MobileParty.MainParty.ItemRoster, key).Amount;
      }

      #endregion
   }
}