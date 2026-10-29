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
   ///   Shows a scrollable list of the player's party items (excluding quest items)
   ///   and invokes a callback with the chosen item's name and base value. Used by
   ///   <see cref="Chat.ChatViewModel" /> to pre-fill the offer text without the player
   ///   needing to remember the exact item name.
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
            var eligible = new List<(ItemObject item, int amount)>();
            foreach ((ItemObject item, int amount) in BuildEligibleItems())
               if (SupplyItemMatcher.Matches(item, categoryKey))
                  eligible.Add((item, amount));

            if (eligible.Count == 0)
            {
               MBInformationManager.AddQuickInformation(new TextObject($"Your party carries no {label} to hand over."));

               return;
            }

            eligible.Sort(CompareItems);
            var elements = new List<InquiryElement>(eligible.Count);
            foreach ((ItemObject item, int amount) in eligible)
            {
               string count = amount > 1 ? $" ×{amount}" : "";
               elements.Add(new InquiryElement(item.StringId, $"{item.Name}{count}", null));
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
      ///   Opens the unified "give" picker: the player's eligible party items
      ///   (sorted by type category then value descending) plus, when
      ///   <paramref name="onGoldSelected" /> is supplied, a "Gold" entry pinned to the top.
      ///   Picking an item fires <paramref name="onItemSelected" /> with (stringId, name, value);
      ///   picking Gold fires <paramref name="onGoldSelected" /> (the caller then asks for an amount).
      ///   With gold offered, the picker still opens even when the party carries no items.
      ///   Quest items (Dragon Banner, etc.) are always excluded.
      /// </summary>
      public static void ShowItemPicker(
         Action<string, string, int> onItemSelected, Action onGoldSelected = null)
      {
         if (onItemSelected == null) return;
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return;

            List<(ItemObject item, int amount)> eligible = BuildEligibleItems();
            if (eligible.Count == 0 && onGoldSelected == null)
            {
               MBInformationManager.AddQuickInformation(
                  new TextObject("Your party carries no items available to offer."));

               return;
            }

            eligible.Sort(CompareItems);
            List<InquiryElement> elements = BuildInquiryElements(eligible);

            // Fold gold into the same control: a "Gold" entry pinned atop the items.
            if (onGoldSelected != null)
            {
               int gold = Hero.MainHero?.Gold ?? 0;
               elements.Insert(0, new InquiryElement(
                  GoldIdentifier, $"Gold (you carry {gold:N0} denars)", null));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
               new MultiSelectionInquiryData(
                  "Give",
                  "Choose gold or an item from your party to give:",
                  elements,
                  true, // isExitShown — Cancel = close without selecting
                  1, 1,
                  "Give", "Cancel",
                  selected =>
                  {
                     if (selected == null || selected.Count == 0) return;
                     var stringId = selected[0].Identifier as string;

                     if (string.IsNullOrEmpty(stringId)) return;
                     if (stringId == GoldIdentifier)
                     {
                        onGoldSelected?.Invoke();

                        return;
                     }

                     Resolve(stringId, onItemSelected);
                  },
                  null),
               false, // pauseGameActiveState: chat already pauses time
               false); // NOT prioritized: a prioritized inquiry opened from inside a parent inquiry's closing
                       // callback (the "What do you offer?" menu picking "Goods or gold") is swallowed by the
                       // engine, so the picker never appeared while prisoners were held. Queuing shows it right
                       // after the parent closes (exactly what the prisoner picker does), and a queued inquiry
                       // still appears over the chat.
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ItemPickerService] ShowItemPicker failed: {ex.Message}");
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

            List<(ItemObject item, int amount)> eligible = BuildEligibleItems();
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

      /// <summary>An item's display name from the party roster, or its id when it cannot be read.</summary>
      private static string NameOf(string itemId)
      {
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
            if (el.EquipmentElement.Item?.StringId == itemId)
               return el.EquipmentElement.Item.Name.ToString();

         return itemId;
      }

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