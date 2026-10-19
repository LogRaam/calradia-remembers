// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence (ratified 2026-07-2x): owns the persisted, MOD-SIDE ledger of gear taken
// from the player on a FLED escape (see CaptiveGearLossPolicy for the isolated "when" seam), keyed by the
// holder's Hero StringId so ONE ledger shape covers both inheritance paths (a bandit escape's gear lands on
// the region's nemesis's Hero via NemesisBehavior.ResolveGearHolderForBand; a lord's captivity keeps it on
// the lord's own Hero). The ledger, NOT the holder's live BattleEquipment/ItemRoster, is the source of
// truth: a lord is a real persistent hero whose equipment/inventory the game can otherwise touch over time,
// so trusting his live roster for recovery could let escaped gear quietly vanish. Setting the holder's
// BattleEquipment to the "worn" selection is purely cosmetic (best-effort, never load-bearing). Recovery
// (the nemesis dies to the player, is captured alive, or a lord holder is defeated/captured by the player)
// grants the WHOLE stock straight to the player's own roster with a notification (deliberately not the
// native post-battle loot screen, which only surfaces items already sitting in the defeated party's OWN
// roster; putting escaped gear there first would reintroduce exactly the "AI can trade it away" risk this
// design avoids). Save-safe like NemesisBehavior: an absent key or a corrupt blob degrades to an empty ledger.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Captivity;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>
   ///   The save-persisted ledger of who currently holds gear taken from the player on a fled escape, and
   ///   the recovery routes that return it (holder dies to the player, is captured alive by the player, or
   ///   is otherwise defeated by the player). Additive and save-safe: absent key or corrupt blob yields an
   ///   empty ledger, never a broken load.
   /// </summary>
   public sealed class CaptiveGearBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_CaptiveGear_v1bytes";

      public static CaptiveGearBehavior Instance;

      /// <summary>Maps a HeldEquipmentSlot to its real EquipmentIndex counterpart (the one place this glue lives).</summary>
      private static readonly Dictionary<HeldEquipmentSlot, EquipmentIndex> SlotMap = new Dictionary<HeldEquipmentSlot, EquipmentIndex> {
         {HeldEquipmentSlot.Weapon0, EquipmentIndex.Weapon0},
         {HeldEquipmentSlot.Weapon1, EquipmentIndex.Weapon1},
         {HeldEquipmentSlot.Weapon2, EquipmentIndex.Weapon2},
         {HeldEquipmentSlot.Weapon3, EquipmentIndex.Weapon3},
         {HeldEquipmentSlot.ExtraWeapon, EquipmentIndex.ExtraWeaponSlot},
         {HeldEquipmentSlot.Head, EquipmentIndex.Head},
         {HeldEquipmentSlot.Body, EquipmentIndex.Body},
         {HeldEquipmentSlot.Leg, EquipmentIndex.Leg},
         {HeldEquipmentSlot.Gloves, EquipmentIndex.Gloves},
         {HeldEquipmentSlot.Cape, EquipmentIndex.Cape},
         {HeldEquipmentSlot.Horse, EquipmentIndex.Horse},
         {HeldEquipmentSlot.HorseHarness, EquipmentIndex.HorseHarness}
      };

      private HolderGearLedger _ledger = new HolderGearLedger();

      public CaptiveGearBehavior()
      {
         Instance = this;
      }

      /// <summary>Every holder currently keeping player gear, for diagnostics (cr.gear_stock).</summary>
      public IReadOnlyList<HolderGearStock> All => _ledger.Holders;

      public override void RegisterEvents()
      {
         // Recovery route 1: the player personally captures the holder alive (nemesis or lord): "the
         // gear is returned too" (ratified explicitly for the nemesis case; the lord case is the same act).
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         // Recovery route 2: a battle the player's side WINS ends with a tracked holder dead. Swept by
         // MapEvent outcome rather than gated on the literal killing blow (HeroKilledEvent's `killer` is
         // often an AI troop, not the player personally) so "defeated by the player" is not read so
         // narrowly that the "never lost forever" guarantee fails in the common case.
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      /// <summary>
      ///   The player's escape from THIS holder's hold: reads the player's CURRENT battle equipment (all
      ///   12 real slots: weapons, armour, mount), records every populated slot into the holder's stock,
      ///   applies the best-per-slot selection to the holder's own BattleEquipment (cosmetic), then strips
      ///   the player. Stripping happens ONLY after a holder was actually resolved: if no holder exists,
      ///   nothing is taken at all (fail safe rather than lose gear with nowhere to go).
      /// </summary>
      public bool TransferPlayerGearTo(Hero holder)
      {
         if (holder == null) return false;

         Equipment playerEquipment = Hero.MainHero?.BattleEquipment;
         if (playerEquipment == null) return false;

         var taken = new List<HeldItem>();
         foreach (KeyValuePair<HeldEquipmentSlot, EquipmentIndex> kvp in SlotMap)
         {
            HeldItem item = ToHeldItem(playerEquipment[kvp.Value], kvp.Key);
            if (item != null) taken.Add(item);
         }

         if (taken.Count == 0) return true; // nothing equipped to take; not a failure

         foreach (KeyValuePair<HeldEquipmentSlot, EquipmentIndex> kvp in SlotMap)
            playerEquipment[kvp.Value] = default;

         RecordFled(holder, taken);

         return true;
      }

      /// <summary>Merges a freshly-taken haul into the holder's persisted stock and best-effort dresses him in the winners.</summary>
      public void RecordFled(Hero holder, IReadOnlyList<HeldItem> items)
      {
         if (holder == null || items == null || items.Count == 0) return;

         HolderGearStock stock = GetOrCreateStock(holder.StringId);
         stock.Items = CaptiveGearStockPolicy.Merge(stock.Items, items).ToList();
         ApplyWornEquipment(holder, stock.Items);
         ModLog.Info($"[GEAR] {holder.Name} now holds {stock.Items.Count} escaped item(s) taken from the player.");
      }

      /// <summary>The named holder's stock, or null when he holds nothing on record.</summary>
      public HolderGearStock GetStock(string holderHeroStringId)
         => string.IsNullOrEmpty(holderHeroStringId)
            ? null
            : _ledger.Holders.FirstOrDefault(h => h != null && h.HolderHeroStringId == holderHeroStringId);

      /// <summary>
      ///   Pops the holder's ENTIRE stock (worn + inventory alike, per the ratified "no scenario where
      ///   gear is lost forever") and grants it straight to the player, with a notification. Idempotent: a
      ///   holder with no recorded stock (already recovered, or never held anything) is a no-op.
      /// </summary>
      public void RecoverAndGrantToPlayer(Hero holder)
      {
         if (holder == null) return;

         HolderGearStock stock = GetStock(holder.StringId);
         if (stock == null || stock.Items == null || stock.Items.Count == 0) return;

         List<HeldItem> items = new List<HeldItem>(stock.Items);
         _ledger.Holders.Remove(stock);

         ItemRoster roster = MobileParty.MainParty?.ItemRoster;
         if (roster == null)
         {
            ModLog.Error("[GEAR] recovery could not run: no player ItemRoster (no active campaign?).");

            return;
         }

         var granted = 0;
         foreach (HeldItem item in items)
         {
            EquipmentElement? element = ToEquipmentElement(item);
            if (!element.HasValue) continue;
            roster.AddToCounts(element.Value, 1);
            granted++;
         }

         if (granted > 0)
            MBInformationManager.AddQuickInformation(new TextObject(
               $"You recover {granted} item(s) {holder.Name} had taken from you."));

         ModLog.Info($"[GEAR] recovered {granted}/{items.Count} item(s) held by {holder.Name} into the player's inventory.");
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // A frozen copy, not the live list, so an autosave racing a main-thread mutation never
               // throws mid-serialize and drops the whole blob (the NemesisBehavior/GrudgeBehavior pattern).
               var snapshot = new HolderGearLedger {
                  Holders = new List<HolderGearStock>(_ledger.Holders),
                  MigrationVersion = _ledger.MigrationVersion
               };
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[GEAR] serialize failed, skipping this save: {ex.GetType().Name}: {ex.Message}");

               return;
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[GEAR] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch { bytes = null; }

            if (bytes is {Length: > 0})
               try { _ledger = JsonConvert.DeserializeObject<HolderGearLedger>(Encoding.UTF8.GetString(bytes)) ?? new HolderGearLedger(); }
               catch (Exception ex)
               {
                  _ledger = new HolderGearLedger();
                  ModLog.Error($"[GEAR] load failed, ledger reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _ledger = new HolderGearLedger(); // absent key (old save, or never triggered): empty ledger, no error

            ModLog.Info($"[GEAR] loaded {_ledger.Holders.Count} holder(s) with escaped-gear stock.");
         }
      }

      #region private

      private HolderGearStock GetOrCreateStock(string holderHeroStringId)
      {
         HolderGearStock stock = GetStock(holderHeroStringId);
         if (stock != null) return stock;

         stock = new HolderGearStock {HolderHeroStringId = holderHeroStringId};
         _ledger.Holders.Add(stock);

         return stock;
      }

      /// <summary>Best-effort, cosmetic only: dresses the holder in the current best-per-slot selection. Never touches the ledger.</summary>
      private static void ApplyWornEquipment(Hero holder, IReadOnlyList<HeldItem> pool)
      {
         try
         {
            Equipment eq = holder?.BattleEquipment;
            if (eq == null) return;

            IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> worn = CaptiveGearStockPolicy.SelectWorn(pool);
            foreach (KeyValuePair<HeldEquipmentSlot, EquipmentIndex> kvp in SlotMap)
            {
               if (!worn.TryGetValue(kvp.Key, out HeldItem item)) continue;
               EquipmentElement? element = ToEquipmentElement(item);
               if (element.HasValue) eq[kvp.Value] = element.Value;
            }
         }
         catch (Exception ex) { ModLog.Error($"[GEAR] dressing {holder?.Name} in his stock failed (cosmetic only): {ex.GetType().Name}: {ex.Message}"); }
      }

      private static HeldItem ToHeldItem(EquipmentElement element, HeldEquipmentSlot slot)
      {
         if (element.IsEmpty || element.Item == null) return null;

         return new HeldItem {
            ItemStringId = element.Item.StringId,
            ModifierStringId = element.ItemModifier?.StringId,
            Slot = slot,
            Tier = (int) element.Item.Tier,
            Value = element.Item.Value
         };
      }

      private static EquipmentElement? ToEquipmentElement(HeldItem item)
      {
         if (item == null || string.IsNullOrEmpty(item.ItemStringId)) return null;

         ItemObject itemObject = MBObjectManager.Instance?.GetObject<ItemObject>(item.ItemStringId);
         if (itemObject == null)
         {
            ModLog.Error($"[GEAR] unknown ItemObject '{item.ItemStringId}' (mod removed/renamed?), skipped.");

            return null;
         }

         ItemModifier modifier = string.IsNullOrEmpty(item.ModifierStringId)
            ? null
            : MBObjectManager.Instance?.GetObject<ItemModifier>(item.ModifierStringId);

         return new EquipmentElement(itemObject, modifier, null, false);
      }

      /// <summary>Recovery route 1: the player personally captures the holder (nemesis or lord) alive.</summary>
      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         try
         {
            if (prisoner == null || capturer?.MobileParty != MobileParty.MainParty) return;
            RecoverAndGrantToPlayer(prisoner);
         }
         catch (Exception ex) { ModLog.Error($"[GEAR] prisoner-taken recovery hook failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Recovery route 2: after any battle the player's side wins, sweep the (small) ledger for a holder who died in it.</summary>
      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (mapEvent is not {IsPlayerMapEvent: true, HasWinner: true}) return;
            if (mapEvent.WinningSide != mapEvent.PlayerSide) return;

            foreach (HolderGearStock stock in _ledger.Holders.ToList())
            {
               Hero holder = Hero.FindFirst(h => h != null && h.StringId == stock.HolderHeroStringId);
               if (holder != null && holder.IsAlive) continue; // still alive: not defeated in THIS battle

               if (holder == null)
               {
                  // Left on the ledger rather than lost: the next player-won battle tries the sweep again.
                  ModLog.Error($"[GEAR] holder Hero '{stock.HolderHeroStringId}' could not be resolved; his stock stays on record.");

                  continue;
               }

               RecoverAndGrantToPlayer(holder);
            }
         }
         catch (Exception ex) { ModLog.Error($"[GEAR] map-event recovery sweep failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}
