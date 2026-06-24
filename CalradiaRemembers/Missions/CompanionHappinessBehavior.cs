// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   Drives companion happiness over time and parts ways with one who stays bitterly unhappy. Persists
   ///   only the "at the leave floor since day N" grace timers; the score itself lives on the profile.
   /// </summary>
   public sealed class CompanionHappinessBehavior : CampaignBehaviorBase
   {
      // Tuning (flagged — Gabriel adjusts by feel).
      private const int DriftStep = 1; // weekly easing toward the baseline
      private const int LeaveGraceDays = 21; // a season at the floor before they actually leave

      private const string SaveKey = "CalradiaRemembers_HappinessFloor_v1bytes";
      public static CompanionHappinessBehavior Instance;

      private Dictionary<string, int> _atFloorSince = new();

      public CompanionHappinessBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      /// <summary>Runs a happiness tick on demand (console test path).</summary>
      public void RunTick() => OnWeeklyTick();

      // ── Persistence (the grace timers only; the score is on the profile) ──

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_atFloorSince, Formatting.None)); }
            catch { bytes = Encoding.UTF8.GetBytes("{}"); }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[HAPPINESS] save failed: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is not {Length: > 0}) return;
            try { _atFloorSince = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, int>(); }
            catch { _atFloorSince = new Dictionary<string, int>(); }
         }
      }

      #region private

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      private static bool IsKeptFromLovedOne(Hero h)
      {
         Hero spouse = h.Spouse;

         return spouse is {IsAlive: true} && spouse != Hero.MainHero && spouse.PartyBelongedTo != h.PartyBelongedTo;
      }

      private void CheckDeparture(Hero h)
      {
         int score = CompanionHappinessService.Get(h);
         if (!CompanionHappiness.WantsToLeave(score))
         {
            _atFloorSince.Remove(h.StringId);

            return;
         }

         int today = Today;
         if (!_atFloorSince.TryGetValue(h.StringId, out int since))
         {
            _atFloorSince[h.StringId] = today; // notice given — a grace before they actually go

            return;
         }

         if (today - since >= LeaveGraceDays) Depart(h);
      }

      private void Depart(Hero h)
      {
         _atFloorSince.Remove(h.StringId);
         try
         {
            RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, h);
            MBInformationManager.AddQuickInformation(
               new TextObject($"{h.Name} has left your service — too long unhappy."), 0, h.CharacterObject);
            ModLog.Info($"[HAPPINESS] {h.Name} departed your service (sustained discontent).");
         }
         catch (Exception ex) { ModLog.Error($"[HAPPINESS] departure failed for {h.Name}: {ex.Message}"); }
      }

      private void OnWeeklyTick()
      {
         foreach (Hero h in Companions())
         {
            bool away = CompanionMissionBehavior.Instance?.IsAway(h.StringId) == true;
            if (!away) // an employed companion is not idle this week
            {
               CompanionHappinessService.ApplyDriver(h, HappinessDriver.Idle);
               if (IsKeptFromLovedOne(h)) CompanionHappinessService.ApplyDriver(h, HappinessDriver.KeptFromLovedOne);
            }

            CompanionHappinessService.Drift(h, DriftStep); // gentle recovery toward baseline
            CheckDeparture(h);
         }
      }

      #endregion
   }
}