// Code written by Gabriel Mailhot, 05/11/2026.
// THROWAWAY SPIKE (cr.barter_spike). It answers whether the game's own barter screen can be the prisoner-sale panel, and
// nothing should be built on it before Gabriel has run it in game.
//
// RaveN017Mod (Nexus, 05/11/2026) asked for "a separate type of table specifically for prisoners"; Gabriel (05/11/2026):
// "Il y a deja un panneau de negociation de transactions dans le jeu vanille, je regarderais si ca peut etre reutilise."
// The decompiled 1.4.8 code says it can, with three seams, and this spike tries exactly those:
//   1. Nobody shows the screen from our chat: vanilla's BarterBegin listeners are the map and mission conversation views.
//      We host BarterVM + the "BarterScreen" movie on our own layer over the chat, and tick its hotkeys ourselves.
//   2. Vanilla only offers captives of a realm at war with the buyer, and its Apply does nothing otherwise. We build the
//      BarterData by hand with OUR captive barterable and one gold barterable a side, and never fire BarterablesRequested
//      (no fiefs, no items, no duplicate vanilla prisoner).
//   3. Vanilla judges the offer at the captive's ransom value. Our barterable values him at what OUR policy lets this buyer
//      pay, so the screen's own acceptance and auto-balance land on our price, with no patch on acceptance.
// Offer and Cancel are not virtual and end in ConversationManager.ContinueConversation, a hero cooldown and an overpay
// relation bonus. Two Harmony prefixes take them over while this spike's barter is open: nothing is applied, what WOULD
// change hands is written to the log ([BARTER-SPIKE]), and the screen closes back onto the chat.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

#endregion

namespace CalradiaRemembers.UI.Spike
{
   /// <summary>A captive the player holds, offered to a buyer at the price our policy lets that buyer pay.</summary>
   internal sealed class SpikeCaptiveBarterable : Barterable
   {
      private readonly Hero _captive;
      private readonly Hero _buyer;
      private readonly int _price;

      public SpikeCaptiveBarterable(Hero captive, Hero buyer, int price) : base(Hero.MainHero, PartyBase.MainParty)
      {
         _captive = captive;
         _buyer = buyer;
         _price = price;
      }

      public Hero Captive => _captive;

      public override string StringID => "cr_spike_captive_sale";

      public override TextObject Name => new TextObject("{=!}" + _captive.Name);

      // The sign convention of TransferPrisonerBarterable: a loss to the owner's side, a gain to anyone else's.
      public override int GetUnitValueForFaction(IFaction faction)
      {
         if (faction == OriginalOwner?.Clan || faction == OriginalOwner?.MapFaction || faction == OriginalParty?.MapFaction) return -_price;

         return faction == _buyer?.Clan || faction == _buyer?.MapFaction ? _price : 0;
      }

      public override ImageIdentifier GetVisualIdentifier() => new CharacterImageIdentifier(CharacterCode.CreateFrom(_captive.CharacterObject));

      public override string GetEncyclopediaLink() => _captive.EncyclopediaLink;

      // The spike moves nothing: the prefix below reads the offer and closes before vanilla would call this.
      public override void Apply() => ModLog.Info($"[BARTER-SPIKE] Apply reached for {_captive.Name}: nothing moved (spike).");
   }

   /// <summary>Opens the game's barter screen over the chat for one held captive and one buyer. Throwaway.</summary>
   internal static class BarterScreenSpike
   {
      private static GauntletLayer _layer;
      private static GauntletMovieIdentifier _movie;
      private static BarterVM _vm;
      private static BarterData _data;
      private static SpriteCategory _sprites;
      private static ScreenBase _host;
      private static Harmony _harmony;

      public static bool IsOpen => _layer != null;

      /// <summary>
      ///   Opens the screen for <paramref name="captive" />, sold to <paramref name="buyer" /> at <paramref name="price" />
      ///   (what our policy lets that buyer pay; the console entry reads it, so this namespace never names Verbs).
      /// </summary>
      public static string Open(Hero buyer, Hero captive, int price, int anchor)
      {
         if (IsOpen) return "The spike's barter screen is already open.";
         if (Campaign.Current?.BarterManager == null || Hero.MainHero == null) return "No campaign.";
         if (buyer == null || buyer == Hero.MainHero) return "No buyer: open a chat with a lord first, or name one (cr.barter_spike <name>).";
         if (captive == null) return "You hold no hero captive to offer.";

         ScreenBase host = ScreenManager.TopScreen;
         if (host == null) return "No screen to host the barter on.";

         try
         {
            Patch();

            PartyBase buyerParty = buyer.PartyBelongedTo?.Party;

            var sale = new SpikeCaptiveBarterable(captive, buyer, price);
            var theirGold = new GoldBarterable(buyer, Hero.MainHero, buyerParty, PartyBase.MainParty, buyer.Gold);
            var yourGold = new GoldBarterable(Hero.MainHero, buyer, PartyBase.MainParty, buyerParty, Hero.MainHero.Gold);

            // The captive and the agreed coin come in already on the table, as the chat would hand them over.
            _data = new BarterData(Hero.MainHero, buyer, PartyBase.MainParty, buyerParty, (b, args, obj) => b == sale || b == theirGold);
            _data.AddBarterable<PrisonerBarterGroup>(sale, true);
            _data.AddBarterable<GoldBarterGroup>(theirGold, true);
            _data.AddBarterable<GoldBarterGroup>(yourGold);
            theirGold.CurrentAmount = Math.Min(price, buyer.Gold);

            _vm = new BarterVM(_data);
            _vm.SetResetInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Reset"));
            _vm.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
            _vm.SetCancelInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Exit"));

            _layer = new GauntletLayer("CalradiaRemembersBarterSpike", 12000);
            _layer.InputRestrictions.SetInputRestrictions();
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("Generic"));
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            _sprites = UIResourceManager.GetSpriteCategory("ui_barter");
            _sprites?.Load();
            _movie = _layer.LoadMovie("BarterScreen", _vm);
            _host = host;
            _host.AddLayer(_layer);
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);

            string line = $"[BARTER-SPIKE] opened on {host.GetType().Name}: {captive.Name} to {buyer.Name}, our price {price} (ransom anchor {anchor}, purse {buyer.Gold}), "
                          + $"{theirGold.CurrentAmount} pre-offered; vanilla would judge it acceptable: {Campaign.Current.BarterManager.IsOfferAcceptable(_data, buyer, buyerParty)}.";
            ModLog.Info(line);

            return line;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BARTER-SPIKE] open failed: {ex.GetType().Name}: {ex.Message}");
            Close("open failed");

            return $"Open failed: {ex.GetType().Name}: {ex.Message} (see CalradiaRemembers.log).";
         }
      }

      /// <summary>Reads the screen's hotkeys, as vanilla's barter view does; called every frame.</summary>
      public static void Tick()
      {
         if (!IsOpen || _vm == null) return;

         try
         {
            if (_layer.Input.IsHotKeyReleased("Exit")) _vm.ExecuteCancel();
            else if (_layer.Input.IsHotKeyReleased("Confirm") && !_vm.IsOfferDisabled) _vm.ExecuteOffer();
            else if (_layer.Input.IsHotKeyReleased("Reset")) _vm.ExecuteReset();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BARTER-SPIKE] tick failed: {ex.GetType().Name}: {ex.Message}");
            Close("tick failed");
         }
      }

      private static void Close(string why)
      {
         try
         {
            if (_layer != null)
            {
               _layer.IsFocusLayer = false;
               ScreenManager.TryLoseFocus(_layer);
               _layer.InputRestrictions.ResetInputRestrictions();
               if (_movie != null) _layer.ReleaseMovie(_movie);
               _host?.RemoveLayer(_layer);
            }

            _sprites?.Unload();
            _vm?.OnFinalize();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BARTER-SPIKE] close failed: {ex.GetType().Name}: {ex.Message}");
         }

         _layer = null;
         _movie = null;
         _vm = null;
         _data = null;
         _sprites = null;
         _host = null;
         ModLog.Info($"[BARTER-SPIKE] closed ({why}); the chat should have the focus back.");
      }

      private static void Patch()
      {
         if (_harmony != null) return;

         _harmony = new Harmony("CalradiaRemembers.BarterScreenSpike");
         _harmony.Patch(AccessTools.Method(typeof(BarterManager), nameof(BarterManager.ApplyAndFinalizePlayerBarter)),
                        prefix: new HarmonyMethod(typeof(BarterScreenSpike), nameof(OfferPrefix)));
         _harmony.Patch(AccessTools.Method(typeof(BarterManager), nameof(BarterManager.CancelAndFinalizePlayerBarter)),
                        prefix: new HarmonyMethod(typeof(BarterScreenSpike), nameof(CancelPrefix)));
      }

      // Only this spike's barter: any other barter in the game runs untouched.
      private static bool OfferPrefix(BarterData barterData)
      {
         if (!IsOpen || barterData != _data) return true;

         var sb = new StringBuilder("[BARTER-SPIKE] Offer accepted by the screen; WOULD change hands (nothing moved):");
         foreach (Barterable b in barterData.GetOfferedBarterables())
            sb.Append($" {b.OriginalOwner?.Name}'s {b.Name} x{b.CurrentAmount};");
         ModLog.Info(sb.ToString());
         InformationManager.DisplayMessage(new InformationMessage("Barter spike: offer read, nothing moved (see the log)."));
         Close("offer");

         return false;
      }

      private static bool CancelPrefix(BarterData barterData)
      {
         if (!IsOpen || barterData != _data) return true;

         Close("cancel");

         return false;
      }
   }
}
