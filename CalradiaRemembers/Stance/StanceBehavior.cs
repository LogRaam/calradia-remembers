// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using System.Collections.Generic;
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
      /// <summary>
      ///   M-S2 (regard audit): how often (in days) Trust/Respect fade one step toward zero. Tuning: 6 days means
      ///   a maxed standing lingers ~7 Calradian years before fully fading and stays above the delegation/respect
      ///   gates (20) for ~5 years of neglect, so it is a slow lapse, never a reset. Raise to soften further.
      /// </summary>
      private const int StandingDecayIntervalDays = 6;

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

            // 3.4 (regard audit): no daily maintenance on the dead. Build the alive-id set ONCE (O(n)) and test
            // membership O(1), instead of a per-profile Hero.FindFirst that made this sweep O(n^2).
            var aliveIds = new HashSet<string>();
            foreach (Hero h in Hero.AllAliveHeroes)
               if (h?.StringId != null)
                  aliveIds.Add(h.StringId);

            // M-S2 (regard audit): Trust/Respect fade far slower than fear, only every few days, so a standing
            // lingers for years of neglect before a gate lapses (any deed renews it in full). Set the interval
            // large to make the fade negligible, or remove the DecayStanding call to disable it entirely.
            var today = (int) CampaignTime.Now.ToDays;
            bool decayStanding = today % StandingDecayIntervalDays == 0;

            foreach (string id in store.ListIds())
            {
               if (!aliveIds.Contains(id)) continue;

               NpcProfile p = store.Get(id);
               if (p == null) continue;

               bool changed = StanceService.DecayFear(p, 1);
               if (decayStanding && StanceService.DecayStanding(p)) changed = true;

               if (changed) store.Set(p);
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