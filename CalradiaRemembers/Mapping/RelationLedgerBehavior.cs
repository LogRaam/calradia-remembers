// Code written by Gabriel Mailhot, 04/11/2026.
// Keeps where each pair's history begins (RelationBaselineLedger) and changes a relation between two NPCs where it belongs.
// tashmetu (Nexus, 04/11/2026): in the base game "a change with any of them lands on the leader": the game's relation action
// writes the two clan leaders' slot, so a younger son's slander made his father an enemy. CR now writes the pair itself,
// and goes through the game's action only when the two are the heads of their houses (or of one house), where both agree.
// The game announces a change through its action and never its starting draw, nor another mod's seeding written directly,
// so the first change heard marks the line between the start and what happened since (PairBondPolicy).

#region

using System;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Relations;
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
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[RELATION] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[RELATION] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            Ledger = RelationBaselineLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      /// <summary>
      ///   Changes how <paramref name="a" /> and <paramref name="b" /> stand by <paramref name="delta" />, on their own slot.
      ///   The heads of two houses, or two of one house, go through the game's action (the slot is theirs either way, and
      ///   the game hears of it); anyone else is written directly, so their clan leader is left out of it.
      /// </summary>
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
