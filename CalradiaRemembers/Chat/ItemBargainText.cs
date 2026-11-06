// Code written by Gabriel Mailhot, 24/06/2026.
// SRP extraction from ChatViewModel (Phase 2): the TEXT for an item-bargain close — the human-readable list
// of offered goods (resolved against the live roster) and the synthetic stage cue handed to the LLM when the
// bargain settles. Pure relocation of working static code; no behaviour change.

#region

using System.Collections.Generic;
using CalradiaRemembers.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Builds the goods list and acceptance stimulus for an item-bargain settlement.</summary>
   public static class ItemBargainText
   {
      /// <summary>
      ///   The synthetic stage cue handed to the LLM when an item bargain closes: the player has paid in
      ///   goods and the NPC enters their service now. Sent as a suppressed player line (never shown or
      ///   logged), it asks the NPC to acknowledge the deal in character — a short, voiced acceptance to
      ///   replace the mechanical confirmation.
      /// </summary>
      public static string BuildBargainAcceptanceStimulus(string goods)
         => $"[The player has just handed over the goods agreed for your service — {goods}. " + "The bargain is met; you take these wares in place of coin and join their company now. " + "Acknowledge it in your own voice — accept the goods and pledge yourself, in character, " + "in a sentence or two. Do not ask for anything further.]";

      /// <summary>
      ///   Builds a human-readable list of the offered goods ("a fine sword, two bolts of cloth, and a
      ///   warhorse") from their roster ids, for the NPC's acceptance stimulus. Resolved against the live
      ///   roster, so call it BEFORE the bridge removes the items. Fail-soft to a generic phrase.
      /// </summary>
      public static string DescribeOfferedGoods(List<ItemOffer> offers)
      {
         ItemRoster roster = MobileParty.MainParty?.ItemRoster;

         if (offers == null || offers.Count == 0 || roster == null) return "the agreed goods";

         var names = new List<string>(offers.Count);
         foreach (ItemOffer offer in offers)
         {
            ItemRosterElement el = Items.RosterGoods.Find(roster, offer.ItemId);
            if (el.EquipmentElement.Item == null) continue;
            string name = Items.RosterGoods.NameOf(el.EquipmentElement);
            names.Add(offer.Count > 1
               ? $"{name} (×{offer.Count})"
               : name);
         }

         if (names.Count == 0) return "the agreed goods";
         if (names.Count == 1) return names[0];
         if (names.Count == 2) return $"{names[0]} and {names[1]}";

         return string.Join(", ", names.GetRange(0, names.Count - 1)) + ", and " + names[names.Count - 1];
      }
   }
}
