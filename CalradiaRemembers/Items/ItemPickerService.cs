// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Items
{
   /// <summary>
   ///   Shows a scrollable list of the player's party items (excluding quest items)
   ///   and invokes a callback with the chosen item's name and base value. Used by
   ///   <see cref="Chat.ChatViewModel" /> to pre-fill the offer text without the player
   ///   needing to remember the exact item name.
   /// </summary>
   public static class ItemPickerService
   {
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

            List<(ItemObject item, int amount)> eligible = BuildEligibleItems();
            if (eligible.Count == 0)
            {
               MBInformationManager.AddQuickInformation(
                  new TextObject("Your party carries no goods to offer."));

               return;
            }

            eligible.Sort(CompareItems);
            var elements = new List<InquiryElement>(eligible.Count);
            foreach ((ItemObject item, int amount) in eligible)
            {
               int stackValue = item.Value * amount;
               string count = amount > 1
                  ? $" ×{amount}"
                  : "";
               var label = $"{item.Name}{count} — {stackValue:N0} denars";
               elements.Add(new InquiryElement(item.StringId, label, null));
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
      ///   Opens the multi-selection inquiry with the player's eligible party items,
      ///   sorted by type category then by value descending. Callback fires with
      ///   (itemName, itemValue) when the player confirms a choice. Shows a quick notice
      ///   and returns silently if the party carries no eligible items.
      ///   Quest items (Dragon Banner, etc.) are always excluded.
      /// </summary>
      public static void ShowItemPicker(Action<string, string, int> onItemSelected)
      {
         if (onItemSelected == null) return;
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return;

            List<(ItemObject item, int amount)> eligible = BuildEligibleItems();
            if (eligible.Count == 0)
            {
               MBInformationManager.AddQuickInformation(
                  new TextObject("Your party carries no items available to offer."));

               return;
            }

            eligible.Sort(CompareItems);
            List<InquiryElement> elements = BuildInquiryElements(eligible);

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Offer an Item",
                  "Select an item from your party inventory to offer:",
                  elements,
                  true, // isExitShown — Cancel = close without selecting
                  1, 1,
                  "Offer", "Cancel",
                  selected =>
                  {
                     if (selected == null || selected.Count == 0) return;
                     var stringId = selected[0].Identifier as string;

                     if (string.IsNullOrEmpty(stringId)) return;
                     Resolve(stringId, onItemSelected);
                  },
                  null),
               false, // pauseGameActiveState — chat already pauses time
               true); // prioritize — show on top of the chat window
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ItemPickerService] ShowItemPicker failed: {ex.Message}");
         }
      }

      #region private

      // ── private ──────────────────────────────────────────────────────────

      private static List<(ItemObject item, int amount)> BuildEligibleItems()
      {
         var list = new List<(ItemObject, int)>();
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;

            if (item is not {Value: > 0}) continue;
            list.Add((item, el.Amount));
         }

         return list;
      }

      private static List<InquiryElement> BuildInquiryElements(
         List<(ItemObject item, int amount)> eligible)
      {
         var elements = new List<InquiryElement>(eligible.Count);
         foreach ((ItemObject item, int amount) in eligible)
         {
            string count = amount > 1
               ? $" ×{amount}"
               : "";
            var label = $"{item.Name}{count} — {item.Value:N0} denars";
            elements.Add(new InquiryElement(item.StringId, label, null));
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
         (ItemObject item, int amount) a,
         (ItemObject item, int amount) b)
      {
         int catDiff = CategoryOrder(a.item.ItemType) - CategoryOrder(b.item.ItemType);

         if (catDiff != 0) return catDiff;

         return b.item.Value - a.item.Value; // value descending within category
      }

      /// <summary>How many of the given item the player currently holds (0 if none).</summary>
      private static int CountInRoster(string itemId)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return 0;
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            ItemObject item = el.EquipmentElement.Item;

            if (item != null && item.StringId == itemId) return el.Amount;
         }

         return 0;
      }

      /// <summary>
      ///   Re-looks up the item by StringId in the current roster (roster may have
      ///   changed since the list was built) and fires the callback with the item's
      ///   StringId, display name, and value. Fail-soft.
      /// </summary>
      private static void Resolve(string stringId, Action<string, string, int> callback)
      {
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return;
            foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
            {
               ItemObject item = el.EquipmentElement.Item;

               if (item == null || item.StringId != stringId || el.Amount <= 0) continue;
               callback(item.StringId, item.Name.ToString(), item.Value);

               return;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ItemPickerService] Resolve failed: {ex.Message}");
         }
      }

      #endregion
   }
}