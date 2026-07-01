// Code written by Gabriel Mailhot, 28/06/2026.
// Companion happiness + RETENTION. Idleness (and being kept from a loved one) slowly sour a companion; the score
// drifts gently back toward baseline. The departure is no longer silent: an unhappy companion ASKS for an
// audience, and if the player keeps refusing it they finally leave by a farewell LETTER (they depart when it
// arrives). Hearing them out mollifies them. The whole system is gated by the MCM "Companion contentment" toggle.
// The audience decision is the pure CompanionRetentionPolicy; everything here is engine glue.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   Drives companion happiness over time and runs the retention loop (audience requests, then a farewell
   ///   letter) for one who stays unhappy. Persists the per-companion refusal counts and pending-farewell days.
   /// </summary>
   public sealed class CompanionHappinessBehavior : CampaignBehaviorBase
   {
      // Tuning (flagged — Gabriel adjusts by feel).
      private const int DriftStep = 1; // weekly easing toward the baseline
      private const int DeliveryDays = 3; // courier time for a farewell letter

      private const string RefusalsSaveKey = "CalradiaRemembers_CompanionRefusals_v1";
      private const string FarewellSaveKey = "CalradiaRemembers_CompanionFarewellDay_v1";
      public static CompanionHappinessBehavior Instance;

      private Dictionary<string, int> _refusals = new();   // companion id → refused audiences so far
      private Dictionary<string, int> _farewellDay = new(); // companion id → day the farewell arrives (they leave then)

      public CompanionHappinessBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      /// <summary>Runs a happiness tick on demand (console test path).</summary>
      public void RunTick() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore)
      {
         SyncDict(dataStore, RefusalsSaveKey, ref _refusals);
         SyncDict(dataStore, FarewellSaveKey, ref _farewellDay);
      }

      #region private

      private static void SyncDict(IDataStore dataStore, string key, ref Dictionary<string, int> dict)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dict, Formatting.None)); }
            catch { bytes = Encoding.UTF8.GetBytes("{}"); }

            try { dataStore.SyncData(key, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[HAPPINESS] save failed ({key}): {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(key, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is not {Length: > 0}) return;
            try { dict = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, int>(); }
            catch { dict = new Dictionary<string, int>(); }
         }
      }

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      private static bool IsKeptFromLovedOne(Hero h)
      {
         Hero spouse = h.Spouse;

         return spouse is {IsAlive: true} && spouse != Hero.MainHero && spouse.PartyBelongedTo != h.PartyBelongedTo;
      }

      private void OnWeeklyTick()
      {
         if (!ModSettings.CompanionContentment) return;

         foreach (Hero h in Companions())
         {
            bool away = CompanionMissionBehavior.Instance?.IsAway(h.StringId) == true;
            if (!away) // an employed companion is not idle this week
            {
               CompanionHappinessService.ApplyDriver(h, HappinessDriver.Idle);
               if (IsKeptFromLovedOne(h)) CompanionHappinessService.ApplyDriver(h, HappinessDriver.KeptFromLovedOne);
            }

            CompanionHappinessService.Drift(h, DriftStep); // gentle recovery toward baseline
            RunRetention(h);
         }
      }

      private void RunRetention(Hero h)
      {
         bool wantsToLeave = CompanionHappiness.WantsToLeave(CompanionHappinessService.Get(h));
         if (!wantsToLeave)
         {
            _refusals.Remove(h.StringId); // recovered — the grievance is gone
            _farewellDay.Remove(h.StringId);

            return;
         }

         if (_farewellDay.ContainsKey(h.StringId)) return; // already leaving — the letter is on its way

         int refusals = _refusals.TryGetValue(h.StringId, out int r) ? r : 0;

         switch (CompanionRetentionPolicy.Decide(true, true, refusals))
         {
            case RetentionStep.RequestAudience:
               RequestAudience(h);

               break;

            case RetentionStep.SendFarewell:
               SendFarewell(h);

               break;
         }
      }

      private void RequestAudience(Hero h)
      {
         var title = new TextObject($"{h.Name} asks for a word");
         var body = new TextObject(
            $"{h.Name} is unhappy in your service and asks to speak with you privately, hoping to be heard before " +
            "they decide to leave.");

         // Hear them out (opens a real conversation where you can win them back in your own words), or turn them
         // away (a slight that sours them further and counts toward their leaving).
         InformationManager.ShowInquiry(new InquiryData(
            title.ToString(), body.ToString(),
            true, true,
            "Let us speak of it…", "I've no time for this.",
            () => GrantAudience(h),
            () => RefuseAudience(h)), true);
      }

      private void GrantAudience(Hero h)
      {
         _refusals[h.StringId] = CompanionRetentionPolicy.OnAudienceGranted(); // the audience is granted — reset the escalation

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId);
         if (profile == null)
         {
            // Fail-soft (no profile to converse with): the old direct reassurance.
            CompanionHappinessService.ApplyDriver(h, HappinessDriver.WellTreated);
            CompanionHappinessService.ApplyDriver(h, HappinessDriver.WellTreated);
            MBInformationManager.AddQuickInformation(
               new TextObject($"You hear {h.Name} out. They seem reassured, for now."), 0, h.CharacterObject);
            ModLog.Info($"[HAPPINESS] {h.Name}'s audience granted (no profile — reassured directly).");

            return;
         }

         // Open the real conversation: they voice the grievance, and you may win them back in your own words
         // (the reassure_companion action, taught by their unhappy mood, mollifies them if you truly make it right).
         ChatManager.OpenForCompanionAudience(h, profile, CompanionAudienceReason.Grievance, false);
         ModLog.Info($"[HAPPINESS] {h.Name}'s grievance audience opened.");
      }

      private void RefuseAudience(Hero h)
      {
         int refusals = _refusals.TryGetValue(h.StringId, out int r) ? r : 0;
         _refusals[h.StringId] = CompanionRetentionPolicy.RegisterRefusal(refusals);
         CompanionHappinessService.ApplyDriver(h, HappinessDriver.Mistreated); // turned away unheard — it sours them further
         ModLog.Info($"[HAPPINESS] {h.Name}'s audience refused ({_refusals[h.StringId]}/{CompanionRetentionPolicy.MaxAudienceRefusals}).");
      }

      private void SendFarewell(Hero h)
      {
         int arriveDay = Today + DeliveryDays;
         _farewellDay[h.StringId] = arriveDay;
         _refusals.Remove(h.StringId);

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(h.StringId);
         if (profile == null)
         {
            ModLog.Info($"[HAPPINESS] {h.Name} has no profile — they will leave on day {arriveDay} without a letter.");

            return;
         }

         profile.SentLetters.Add(new PendingLetter {
            SenderId = h.StringId,
            SenderName = h.Name?.ToString(),
            Reason = LetterReason.CompanionFarewell,
            Content = FarewellLetter(h),
            SentOnDay = Today,
            DeliveryDay = arriveDay,
            IsDelivered = false
         });
         store.Set(profile);
         ModLog.Info($"[HAPPINESS] {h.Name} wrote a farewell — they leave when it arrives (day {arriveDay}).");
      }

      private static string FarewellLetter(Hero h)
         => "I have asked, more than once, for a moment of your time, and each time you had none to spare. " +
            "I take the lesson. By the time this letter reaches you I will already be gone from your service. " +
            "I hold no great grudge, but I will wait upon you no longer. May the roads treat you well.";

      private void OnDailyTick()
      {
         if (!ModSettings.CompanionContentment || _farewellDay.Count == 0) return;

         foreach (string id in _farewellDay.Keys.ToList())
         {
            if (Today < _farewellDay[id]) continue;

            _farewellDay.Remove(id);
            Hero h = Hero.FindFirst(x => x != null && x.StringId == id);
            if (h is {IsAlive: true, IsPlayerCompanion: true}) Depart(h);
         }
      }

      private void Depart(Hero h)
      {
         try
         {
            RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, h);
            MBInformationManager.AddQuickInformation(
               new TextObject($"{h.Name} has left your service — their farewell letter has arrived."), 0, h.CharacterObject);
            ModLog.Info($"[HAPPINESS] {h.Name} departed your service (farewell letter delivered).");
         }
         catch (Exception ex) { ModLog.Error($"[HAPPINESS] departure failed for {h.Name}: {ex.Message}"); }
      }

      #endregion
   }
}
