// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Items;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Owns the offer ("give") flow so it no longer threads through the shared ChatViewModel partials (which
   ///   forced a surgical-commit dance every release), mirroring <see cref="RoundTableController" /> and
   ///   <see cref="ActionConfirmationController" />. The bag is the one "what do you offer?" control: it settles an
   ///   outstanding bargain or supply quest, or pre-fills the chat input with a gift of an item, gold, or a held
   ///   prisoner. Where the click routes is the pure, testable <see cref="OfferFlowGate" />; the engine glue
   ///   (pickers, inquiries, the bridge settle calls) is a straight lift of the former ChatViewModel members and
   ///   drives the view model through the <see cref="IOfferHost" /> seam, so runtime behaviour is identical. The
   ///   controller also owns <see cref="OfferedItemId" />, which the exchange loop reads when building its
   ///   ActionContext and clears once the offering turn completes.
   /// </summary>
   internal sealed class OfferController
   {
      // StringId of the item the player picked from the bag this turn: give_item transfers exactly
      // this, so a freely-given gift never depends on the LLM echoing the item's display name back.
      private string _offeredItemId;

      /// <summary>The item id the player picked from the bag this turn, or null; read when building the ActionContext.</summary>
      public string OfferedItemId => _offeredItemId;

      /// <summary>
      ///   Clears the picked item so a later, unrelated give_item never re-transfers it. Called from the exchange
      ///   loop's finally, once the offering turn has been sent.
      /// </summary>
      public void ClearOfferedItem() => _offeredItemId = null;

      /// <summary>
      ///   The bag: the ONE "what do you offer?" control. It gives an item, gold, OR (when the player holds hero
      ///   captives) a prisoner, so no separate prisoner button clutters the row. A bargain awaiting payment-in-kind
      ///   is settled directly; otherwise, when captives are held, the player first picks a category, else it goes
      ///   straight to the item/gold picker. The pick pre-fills the chat input, which the player edits and sends as
      ///   usual. Where the click routes is decided by <see cref="OfferFlowGate.Route" />.
      /// </summary>
      public void Execute(IOfferHost host)
      {
         if (!host.CanOffer) return;

         // A bargain awaiting goods: the bag settles it (multi-select, bridge enforces the value).
         InformalQuest bargain = FindOutstandingItemBargain(host);

         // A supply task (ProvideGoods) awaiting delivery: the bag hands over the matching goods by count.
         InformalQuest supply = FindOutstandingSupplyQuest(host);

         List<Hero> prisoners = Verbs.BuyPrisonerVerb.PlayerHeldHeroPrisoners();

         switch (OfferFlowGate.Route(bargain != null, supply != null, prisoners.Count))
         {
            case OfferRoute.ItemBargain:
               ItemPickerService.ShowBargainPicker(
                  bargain.RequiredItemValue, offers => SettleItemBargain(host, bargain, offers));

               return;

            case OfferRoute.SupplyQuest:
               ItemPickerService.ShowSupplyPicker(
                  supply.RequiredItemCategory, supply.RequiredItemCount, offers => SettleSupply(host, supply, offers));

               return;

            case OfferRoute.InventoryDirect:
               ShowInventoryOffer(host);

               return;

            default: // OfferRoute.CategoryChoice (the player holds hero captives)
               var choices = new List<InquiryElement> {
                  new InquiryElement("goods", "Goods or gold", null),
                  new InquiryElement("prisoner", "A prisoner", null)
               };
               MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                  "What do you offer?",
                  "",
                  choices, true, 1, 1, "Choose", "Cancel",
                  picked =>
                  {
                     if ((picked?.FirstOrDefault()?.Identifier as string) == "prisoner") OfferHeldPrisoner(host, prisoners);
                     else ShowInventoryOffer(host);
                  },
                  null));

               return;
         }
      }

      /// <summary>
      ///   The legacy "Give Gold" entry: opens the gold-amount prompt directly, bypassing the item/prisoner
      ///   routing. Kept public for ChatViewModel.ExecuteOfferGold, whose old button binding survives in the prefab.
      /// </summary>
      public void PromptGold(IOfferHost host) => PromptGoldOffer(host);

      /// <summary>Opens the item picker (with a give-gold fallback) and pre-fills the chosen item into the input.</summary>
      private void ShowInventoryOffer(IOfferHost host)
      {
         // Remember the exact item id so give_item transfers THAT item, not whatever name the LLM echoes back.
         ItemPickerService.ShowItemPicker(
            (itemId, name, value) =>
            {
               _offeredItemId = itemId;
               // Frame it as a COMPLETED handover (a stage direction), not a tentative "I would like to offer":
               // the offer wording read as a proposal, so the NPC reacted to the OFFER and the gift needed a
               // second turn to land (player report: two reactions for one gift). Handing it over now makes the
               // NPC react ONCE, to receiving it, and the give_item action fires in the same turn.
               host.SetInputText($"*I hand you {name} (worth {value:N0} denars).*");
               ModLog.Info($"[OFFER] player pre-filled gift: {name} ({value} denars, id={itemId}).");
            },
            () => PromptGoldOffer(host));
      }

      /// <summary>Opens a picker of the held hero captives so the player can offer to sell one; the pick pre-fills the chat input.</summary>
      private void OfferHeldPrisoner(IOfferHost host, List<Hero> held)
      {
         var options = held.Select(h => new InquiryElement(h, h.Name?.ToString() ?? "a captive", null)).ToList();
         MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            "Offer a Prisoner",
            "Which captive do you hand over?",
            options, true, 1, 1, "Offer", "Cancel",
            picked =>
            {
               if (picked?.FirstOrDefault()?.Identifier is not Hero captive) return;

               // THE ONLY PRISONER OPTION ANYWHERE WAS WORDED AS A SALE, so a player bringing a lord the captive
               // he had asked for was steered straight to the one verb that never keeps a bargain (Lordfadooboo,
               // 18/09/2026: "i didnt see the button to do that action"). When this captive settles something
               // this person asked for, the line is the handover itself, framed as done, like a gift, so it
               // lands in one turn.
               NpcProfile profile = host.Npc == null
                  ? null
                  : NpcMemoryCampaignBehavior.Instance?.Store?.Get(host.Npc.StringId);
               bool owed = DeliveryQuestMatcher.QuestSettledBy(
                  profile?.ActiveQuests,
                  new HeldCaptive {Id = captive.StringId, FactionId = captive.MapFaction?.StringId}) != null;

               host.SetInputText(owed
                  ? $"*I hand {captive.Name} over to you in chains, as you asked.*"
                  : $"I have {captive.Name} in my custody. Would you buy this prisoner from me?");
               ModLog.Info($"[OFFER PRISONER] pre-filled {(owed ? "a DELIVERY" : "a sale")}: {captive.Name}.");
            },
            null));
      }

      /// <summary>
      ///   Asks the player how many denars to give, then pre-fills the chat input so the NPC can respond with
      ///   take_gold, which transfers the gold and stamps any outstanding ProvideGold quest as satisfied (any
      ///   amount counts).
      /// </summary>
      private void PromptGoldOffer(IOfferHost host)
      {
         int carried = Hero.MainHero?.Gold ?? 0;
         InformationManager.ShowTextInquiry(new TextInquiryData(
            "Give Gold",
            $"How many denars? (You carry {carried:N0}.)",
            true, true,
            "Give", "Cancel",
            amountText =>
            {
               if (!int.TryParse(amountText?.Trim(), out int amount) || amount <= 0) return;
               amount = Math.Min(amount, carried);
               host.SetInputText($"I would like to give you {amount:N0} denars.");
               ModLog.Info($"[OFFER GOLD] pre-filled: {amount} denars.");
            },
            null));
      }

      /// <summary>
      ///   The current NPC's outstanding item-barter bargain (deliver goods for a favor), or null when there is
      ///   none. Read from the live conversation profile.
      /// </summary>
      private InformalQuest FindOutstandingItemBargain(IOfferHost host)
      {
         try
         {
            NpcProfile giver = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile;

            if (giver == null || host.Npc == null) return null;

            return giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.DeliverItems && q.IsOutstanding);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>The outstanding ProvideGoods supply task of the current conversation partner, or null.</summary>
      private InformalQuest FindOutstandingSupplyQuest(IOfferHost host)
      {
         try
         {
            NpcProfile giver = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile;

            if (giver == null || host.Npc == null) return null;

            return giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.ProvideGoods && q.IsOutstanding);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   Hands the chosen goods to the bridge for a ProvideGoods supply task. The bridge tallies the count, and on
      ///   a full delivery completes the quest (reward paid, journal closed) there; here we only surface the outcome
      ///   line and refresh the bag's visibility.
      /// </summary>
      private void SettleSupply(IOfferHost host, InformalQuest supply, List<ItemOffer> offers)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile giver = behavior?.DialogFlow?.Profile;

            if (behavior?.GameStateBridge == null || giver == null) return;

            ActionOutcome outcome = behavior.GameStateBridge.SettleSupplyQuest(giver, host.Npc, supply, offers);
            if (outcome == null) return;

            host.AppendSystemMessageLine(outcome.Message, outcome.IsPositive);
            if (outcome.IsPositive) host.NotifyOfferItemVisibilityChanged();
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] SettleSupply failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Hands the chosen goods to the bridge, which enforces the value floor and, if met, removes them and
      ///   honors the bargain's favor. On success the quest is closed and the NPC accepts the goods in their own
      ///   voice (one LLM turn, written to the story log) rather than a bare mechanical line, falling back to the
      ///   plain confirmation when the LLM is unavailable. A short offer keeps the plain refusal line.
      /// </summary>
      private void SettleItemBargain(IOfferHost host, InformalQuest bargain, List<ItemOffer> offers)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile giver = behavior?.DialogFlow?.Profile;

            if (behavior?.GameStateBridge == null || giver == null) return;

            // Resolve the goods' display names BEFORE the bridge removes them from the
            // roster, so the NPC's acceptance line can name what it was paid with.
            string goods = ItemBargainText.DescribeOfferedGoods(offers);

            ActionOutcome outcome = behavior.GameStateBridge
                                            .SettleItemBargain(giver, host.Npc, bargain, offers);

            if (outcome == null) return;

            // Short offer: the deal didn't close; nothing was removed. Keep the plain line.
            if (!outcome.IsPositive)
            {
               host.AppendSystemMessageLine(outcome.Message, false);

               return;
            }

            // Deal closed: stamp the bargain settled and drop the bag button.
            int day = behavior.GameStateBridge.GetCurrentGameDay();
            QuestService.MarkBargainSettled(giver, bargain, host.Npc, day, behavior.Store);
            host.NotifyOfferItemVisibilityChanged();

            // Narrative beat instead of a bare green line: the NPC accepts the goods in
            // their own voice (shown under their portrait, written to the story log) via
            // one LLM turn driven by a synthetic stage cue. Fall back to the mechanical
            // confirmation when the LLM is unavailable so the player still sees the result.
            if (behavior.LlmServices is not {IsAvailable: true})
            {
               host.AppendSystemMessageLine(outcome.Message, true);

               return;
            }

            host.BeginBargainAcceptanceTurn(ItemBargainText.BuildBargainAcceptanceStimulus(goods));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[OFFER] SettleItemBargain failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
