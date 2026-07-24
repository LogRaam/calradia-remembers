// Code written by Gabriel Mailhot, 24/07/2026.
// Scattered-loot recovery (ratified 2026-07-24, AUDIT-NEMESIS M1): the mod-side, save-persisted ledger of the
// player's gear scattered among bandit bands after a FLED bandit escape, and the recovery route that returns
// it a piece at a time. This REPLACES the bandit branch of the old holder model (which minted a nemesis to
// hold the whole stock, resurrecting a boss during his own quiet years and inflating his counters): a bandit
// escape now scatters each item by CULTURE, and defeating a band of that culture has a chance to return the
// oldest-lost matching piece into the player's inventory. The LORD branch is untouched: a lord is a specific
// person who took your gear, recovered whole by beating him (CaptiveGearBehavior). The strip/grant slot glue
// deliberately mirrors CaptiveGearBehavior rather than refactoring the shipped path. Save-safe: an absent key
// or a corrupt blob degrades to an empty ledger, never a broken load (the NemesisBehavior/CaptiveGear pattern).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
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
   /// <summary>Persists the player's scattered captivity loot and returns it, a piece at a time, from defeated bandit bands.</summary>
   public sealed class ScatteredLootBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_ScatteredLoot_v1bytes";

      public static ScatteredLootBehavior Instance;

      // The same 12 real equipment slots CaptiveGearBehavior strips, kept as its own copy so the shipped
      // holder path is not touched (behaviour-neutral duplication, flagged rather than refactored under risk).
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

      private ScatteredLootLedger _ledger = new ScatteredLootLedger();

      public ScatteredLootBehavior()
      {
         Instance = this;
      }

      /// <summary>Every scattered item still out there, for diagnostics (cr.loot_stock).</summary>
      public IReadOnlyList<ScatteredLootItem> All => _ledger.Items;

      public override void RegisterEvents()
      {
         // Recovery: after any battle the player's side wins, a defeated bandit band of a matching culture may
         // yield back one scattered piece. Swept on the map-event outcome (not the killing blow) so a win the
         // player's side achieves counts even when an AI troop landed the final hit.
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      /// <summary>
      ///   A fled BANDIT escape: reads the player's current battle equipment (all 12 slots), records each
      ///   populated slot into the ledger under the capturing band's <paramref name="cultureKey" />, then
      ///   strips the player. Returns how many pieces were scattered (0 = nothing was equipped, so nothing is
      ///   lost). The player recovers these later by defeating bands of the same culture.
      /// </summary>
      public int ScatterPlayerGear(string cultureKey, int today)
      {
         Equipment playerEquipment = Hero.MainHero?.BattleEquipment;
         if (playerEquipment == null) return 0;

         string culture = string.IsNullOrWhiteSpace(cultureKey) ? "looters" : cultureKey;

         var taken = new List<HeldItem>();
         foreach (KeyValuePair<HeldEquipmentSlot, EquipmentIndex> kvp in SlotMap)
         {
            HeldItem item = ToHeldItem(playerEquipment[kvp.Value], kvp.Key);
            if (item != null) taken.Add(item);
         }

         if (taken.Count == 0) return 0; // nothing equipped to take

         foreach (KeyValuePair<HeldEquipmentSlot, EquipmentIndex> kvp in SlotMap)
            playerEquipment[kvp.Value] = default;

         foreach (HeldItem item in taken)
            _ledger.Items.Add(new ScatteredLootItem {Item = item, CultureKey = culture, LostDay = today});

         ModLog.Info($"[LOOT] scattered {taken.Count} item(s) among the {culture} bands; {_ledger.Items.Count} piece(s) now out there.");

         return taken.Count;
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // A frozen copy, not the live list, so an autosave racing a main-thread mutation never
               // throws mid-serialize and drops the blob (the NemesisBehavior/CaptiveGearBehavior pattern).
               var snapshot = new ScatteredLootLedger {
                  Items = new List<ScatteredLootItem>(_ledger.Items),
                  MigrationVersion = _ledger.MigrationVersion
               };
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[LOOT] serialize failed, skipping this save: {ex.GetType().Name}: {ex.Message}");

               return;
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[LOOT] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch { bytes = null; }

            if (bytes is {Length: > 0})
               try { _ledger = JsonConvert.DeserializeObject<ScatteredLootLedger>(Encoding.UTF8.GetString(bytes)) ?? new ScatteredLootLedger(); }
               catch (Exception ex)
               {
                  _ledger = new ScatteredLootLedger();
                  ModLog.Error($"[LOOT] load failed, ledger reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _ledger = new ScatteredLootLedger(); // absent key (old save, or never triggered): empty, no error

            ModLog.Info($"[LOOT] loaded {_ledger.Items.Count} scattered item(s).");
         }
      }

      #region private

      /// <summary>Recovery: after a player-won battle, a defeated bandit band of a matching culture may yield one scattered piece.</summary>
      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (mapEvent is not {IsPlayerMapEvent: true, HasWinner: true}) return;
            if (mapEvent.WinningSide != mapEvent.PlayerSide) return;
            if (_ledger.Items.Count == 0) return;

            BattleSideEnum losing = mapEvent.WinningSide == BattleSideEnum.Attacker
               ? BattleSideEnum.Defender
               : BattleSideEnum.Attacker;

            List<string> defeatedCultures = DefeatedBanditCultures(mapEvent, losing);
            if (defeatedCultures.Count == 0) return;

            ScatteredLootItem piece = ScatteredLootPolicy.SelectRecoverable(_ledger.Items, defeatedCultures);
            if (piece == null) return; // nothing scattered matches these bands

            // The chance the recovered piece surfaces THIS fight, so a stock drains over several battles.
            if (!ScatteredLootPolicy.RollRecovers(MBRandom.RandomInt(100)))
            {
               ModLog.Info($"[LOOT] a matching band fell, but no piece surfaced this time ({_ledger.Items.Count} still scattered).");

               return;
            }

            GrantAndRemove(piece);
         }
         catch (Exception ex) { ModLog.Error($"[LOOT] recovery sweep failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The bandit-clan culture keys of the parties that LOST this battle (the bands the player just beat).</summary>
      private static List<string> DefeatedBanditCultures(MapEvent mapEvent, BattleSideEnum losing)
      {
         var cultures = new List<string>();

         foreach (MapEventParty mep in mapEvent.PartiesOnSide(losing))
         {
            MobileParty party = mep?.Party?.MobileParty;
            if (party is not {IsBandit: true}) continue;

            string key = party.ActualClan?.StringId;
            cultures.Add(string.IsNullOrEmpty(key) ? "looters" : key);
         }

         return cultures;
      }

      /// <summary>Grants the recovered piece to the player's inventory and drops it from the ledger, with a notice.</summary>
      private void GrantAndRemove(ScatteredLootItem piece)
      {
         ItemRoster roster = MobileParty.MainParty?.ItemRoster;
         if (roster == null)
         {
            ModLog.Error("[LOOT] recovery could not run: no player ItemRoster (no active campaign?).");

            return;
         }

         EquipmentElement? element = ToEquipmentElement(piece.Item);
         _ledger.Items.Remove(piece);

         if (!element.HasValue) return; // the item no longer resolves (mod removed/renamed); dropped from the ledger

         roster.AddToCounts(element.Value, 1);

         string itemName = element.Value.Item?.Name?.ToString() ?? "a piece of your gear";
         MBInformationManager.AddQuickInformation(new TextObject(
            $"Among the spoils you recover {itemName}, taken from you in captivity."));
         ModLog.Info($"[LOOT] recovered '{piece.Item.ItemStringId}' from a defeated band; {_ledger.Items.Count} piece(s) still scattered.");
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
            ModLog.Error($"[LOOT] unknown ItemObject '{item.ItemStringId}' (mod removed/renamed?), skipped.");

            return null;
         }

         ItemModifier modifier = string.IsNullOrEmpty(item.ModifierStringId)
            ? null
            : MBObjectManager.Instance?.GetObject<ItemModifier>(item.ModifierStringId);

         return new EquipmentElement(itemObject, modifier, null, false);
      }

      #endregion
   }
}
