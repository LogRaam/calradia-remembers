// Code written by Gabriel Mailhot, 04/11/2026.
// Keeps where each pair's history begins (RelationBaselineLedger) and changes a relation between two NPCs where it belongs.
// tashmetu (Nexus, 04/11/2026): in the base game "a change with any of them lands on the leader": the game's relation action
// writes the two clan leaders' slot, so a younger son's slander made his father an enemy. CR now writes the pair itself,
// and goes through the game's action only when the two are the heads of their houses (or of one house), where both agree.
// The game announces a change through its action and never its starting draw, nor another mod's seeding written directly,
// so the first change heard marks the line between the start and what happened since (PairBondPolicy).

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Relations;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Marks where each pair's history begins, and changes a relation between two NPCs on their own slot.</summary>
   public sealed class RelationLedgerBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_relation_baselines";

      // Compressed through SaveBlob since 2.8.6 (tashmetu, 09/11/2026: a byte[] past 16,777,215 bytes never loads). A new key, so
      // an older CR finds none of its own here and starts this store empty rather than read GZip as text.
      private const string SaveKeyGz = "cr_relation_baselines_gz";

      public RelationLedgerBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static RelationLedgerBehavior Instance { get; private set; }

      /// <summary>Where each pair's history begins (never null).</summary>
      internal RelationBaselineLedger Ledger { get; private set; } = new RelationBaselineLedger();

      public override void RegisterEvents()
      {
         CampaignEvents.HeroRelationChanged.AddNonSerializedListener(this, OnHeroRelationChanged);
      }

      public override void SyncData(IDataStore dataStore)
      {
         // Compressed through SaveBlob (tashmetu, 09/11/2026: this ledger reached 18.2 million bytes and the save never
         // loaded again).
         if (dataStore.IsSaving)
         {
            // What the ledger no longer needs is dropped first (RelationBaselineLedger.Prune; Gabriel, 09/11/2026).
            PruneWithWorld(Ledger);

            string json;
            try { json = Ledger.Serialize(); }
            catch (Exception ex)
            {
               ModLog.Error($"[RELATION] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               json = null;
            }

            SaveBlob.Write(dataStore, SaveKeyGz, json);
         }
         else
         {
            string json = SaveBlob.Read(dataStore, SaveKeyGz, SaveKey);
            try { Ledger = RelationBaselineLedger.Deserialize(json); }
            catch (Exception ex)
            {
               ModLog.Error($"[RELATION] ledger could not be read, starting empty so the campaign still opens: {ex.GetType().Name}: {ex.Message}");
               Ledger = RelationBaselineLedger.Deserialize(null);
            }
         }
      }

      /// <summary>
      ///   Changes how <paramref name="a" /> and <paramref name="b" /> stand by <paramref name="delta" />, on their own slot.
      ///   The heads of two houses, or two of one house, go through the game's action (the slot is theirs either way, and
      ///   the game hears of it); anyone else is written directly, so their clan leader is left out of it.
      /// </summary>
      /// <summary>
      ///   Prunes <paramref name="ledger" /> against the world as it stands: pairs back where they began, and pairs with
      ///   someone dead or no longer known. Returns how many were dropped; logs the count. Never throws. cr.test
      ///   relation_ledger_prune calls this same method.
      /// </summary>
      internal static int PruneWithWorld(RelationBaselineLedger ledger)
      {
         if (ledger == null || ledger.Baselines.Count == 0) return 0;

         try
         {
            var clock = Stopwatch.StartNew();
            var living = new Dictionary<string, Hero>();
            foreach (Hero h in Hero.AllAliveHeroes)
               if (h?.StringId != null) living[h.StringId] = h;
            foreach (Hero h in Hero.DeadOrDisabledHeroes)
               if (h?.StringId != null && !h.IsDead) living[h.StringId] = h; // a disabled hero may come back

            int before = ledger.Baselines.Count;
            int dropped = ledger.Prune((a, b) => living.TryGetValue(a, out Hero ha) && living.TryGetValue(b, out Hero hb)
                                                    ? CharacterRelationManager.GetHeroRelation(ha, hb)
                                                    : (int?) null);
            if (dropped > 0)
               ModLog.Info($"[RELATION] ledger pruned: {dropped:N0} of {before:N0} pairs no longer needed (back at their start, or someone gone), in {clock.ElapsedMilliseconds} ms.");

            return dropped;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RELATION] ledger prune failed, saving it whole: {ex.GetType().Name}: {ex.Message}");

            return 0;
         }
      }

      /// <summary>The game's own number between the two, as their slot holds it.</summary>
      internal static int Between(Hero a, Hero b) => a == null || b == null || a == b ? 0 : CharacterRelationManager.GetHeroRelation(a, b);

      /// <summary>
      ///   Brings the pair's number back to even when it is below, never above: two who made their peace (kurushimi00,
      ///   06/11/2026) should not stay on the game's own enemy lists.
      /// </summary>
      internal static void EvenOut(Hero a, Hero b)
      {
         int now = Between(a, b);
         if (now < VanillaRelation.NeutralTier) Change(a, b, VanillaRelation.NeutralTier - now);
      }

      internal static void Change(Hero a, Hero b, int delta)
      {
         if (a == null || b == null || a == b || delta == 0) return;

         try
         {
            if (GameWritesThePair(a, b))
            {
               ChangeRelationAction.ApplyRelationChangeBetweenHeroes(a, b, delta, false);

               return;
            }

            int before = CharacterRelationManager.GetHeroRelation(a, b);
            Instance?.Ledger.Saw(a.StringId, b.StringId, before);
            a.SetPersonalRelation(b, before + delta);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RELATION] change {a.Name} / {b.Name} by {delta} failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Sets what CR believes between two NPCs to <paramref name="target" />, as history on their own slot (the ledger
      ///   marks a start of 0). For cr.test all and the console, which arrange a relation before driving a real path; with
      ///   the player on either side it keeps the game's action, which is what production reads there.
      /// </summary>
      internal static void SetAsHistory(Hero a, Hero b, int target)
      {
         if (a == null || b == null || a == b) return;
         if (a == Hero.MainHero || b == Hero.MainHero)
         {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(a, b, target - a.GetRelation(b), false);

            return;
         }

         Instance?.Ledger.Forget(a.StringId, b.StringId);
         Instance?.Ledger.Saw(a.StringId, b.StringId, 0);
         a.SetPersonalRelation(b, target);
      }

      /// <summary>Whether the game's own action writes this very pair (heads of their houses, one house, or no house).</summary>
      internal static bool GameWritesThePair(Hero a, Hero b)
      {
         Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(a, b, out Hero ea, out Hero eb);

         return (ea == a && eb == b) || (ea == b && eb == a);
      }

      #region private

      // The game has already written the new value; what it held before is the value less the change (a clamp at the
      // scale's end may shift it by a little, never its sign).
      private void OnHeroRelationChanged(Hero effective, Hero effectiveWith, int change, bool notify, ChangeRelationAction.ChangeRelationDetail detail,
                                         Hero original, Hero originalWith)
      {
         try
         {
            if (effective == null || effectiveWith == null || change == 0) return;

            Ledger.Saw(effective.StringId, effectiveWith.StringId, CharacterRelationManager.GetHeroRelation(effective, effectiveWith) - change);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RELATION] marking a change failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}
