// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>
   ///   Owns the time-based and capture-based stance movements. Holds no state of its own — stance
   ///   lives on each <see cref="NpcProfile" /> and persists with it — so <see cref="SyncData" /> is empty.
   /// </summary>
   public sealed class StanceBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
      }

      public override void SyncData(IDataStore dataStore) { }

      #region private

      private void OnDailyTick()
      {
         try
         {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

            if (store == null) return;

            foreach (string id in store.ListIds())
            {
               NpcProfile p = store.Get(id);

               if (!StanceService.DecayFear(p, 1)) continue;
               if (p != null)
                  store.Set(p);
            }
         }
         catch (Exception ex) { ModLog.Error($"[STANCE] fear decay failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         try
         {
            if (prisoner is not {IsLord: true} || prisoner == Hero.MainHero) return;

            // Only when it is the PLAYER's own faction that took them.
            IFaction captorFaction = capturer?.MapFaction;

            if (captorFaction == null || captorFaction != Hero.MainHero?.MapFaction) return;

            StanceService.OnTakenPrisoner(prisoner);
         }
         catch (Exception ex) { ModLog.Error($"[STANCE] prisoner hook failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}