// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>Accrues war-weariness from real battle events and runs the honorable-retirement loop for a spent companion.</summary>
   public sealed class WarWearinessBehavior : CampaignBehaviorBase
   {
      private const string DeclinedSaveKey = "CalradiaRemembers_WearinessDeclined_v1";
      private const string ForcedBackSaveKey = "CalradiaRemembers_WearinessForcedBack_v1";
      private const string ServedSinceSaveKey = "CalradiaRemembers_WearinessServedSince_v1";

      private const int ForcedBackToBreak = 3; // weeks dragged back to war before the retirement breaks

      // Tuning (flagged — Gabriel adjusts by feel).
      private const int RestStep = WarWeariness.RestStepPerWeek; // weekly recovery
      private const int RetireDeclineCooldownDays = 14; // after a decline, wait before asking again
      private const string WoundCountedSaveKey = "CalradiaRemembers_WearinessWounded_v1";
      public static WarWearinessBehavior Instance;

      private Dictionary<string, int> _declinedUntil = new(); // companion id → day we may ask again
      private Dictionary<string, int> _forcedBack = new(); // stepped-back id → weeks dragged back to war so far
      private bool _requestOpen; // an inquiry is on screen; don't stack another
      private Dictionary<string, int> _servedSince = new(); // companion id → game day service started (min of first-observed and earliest recorded event)
      private Dictionary<string, int> _woundCounted = new(); // companion id → 1 while their current wound is counted

      public WarWearinessBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>
      ///   Carries out a granted retirement (called from the audience conversation's <c>retire</c> action at close,
      ///   or as a fail-soft when the chat can't open). A LANDED companion steps back from the field but stays in
      ///   the clan; a landless one leaves your service. Re-validates nothing beyond a live hero — the grant is the
      ///   player's decision, already made.
      /// </summary>
      public void ExecuteRetirement(Hero h, bool landed)
      {
         if (h is not {IsAlive: true}) return;

         if (landed)
         {
            WarWearinessService.SetSteppedBack(h, true);
            _forcedBack.Remove(h.StringId);
            MBInformationManager.AddQuickInformation(
               new TextObject($"{h.Name} lays down the sword to tend their own. They remain of your clan, but you will not march them to war again."),
               0, h.CharacterObject);
            ModLog.Info($"[WEARINESS] {h.Name} stepped back from the field (landed retirement).");
         }
         else
         {
            Depart(h);
         }
      }

      public override void RegisterEvents()
      {
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
      }

      /// <summary>Runs a weekly weariness pass on demand (console test path).</summary>
      public void RunTick() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore)
      {
         SyncDict(dataStore, DeclinedSaveKey, ref _declinedUntil);
         SyncDict(dataStore, ForcedBackSaveKey, ref _forcedBack);
         SyncDict(dataStore, WoundCountedSaveKey, ref _woundCounted);
         SyncDict(dataStore, ServedSinceSaveKey, ref _servedSince);
      }

      /// <summary>How many days this companion has served (since first-observed or their earliest recorded memory), for the minimum-tenure gate.</summary>
      public int DaysServed(Hero h)
      {
         if (h == null) return 0;
         StampServiceIfNew(h);

         return Today - _servedSince[h.StringId];
      }

      #region private

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      /// <summary>A companion is INVESTED (never leaves) when they govern a settlement or lead a party of their own.</summary>
      private static bool IsInvested(Hero h)
         => h.GovernorOf != null || h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h;

      private static void SyncDict(IDataStore dataStore, string key, ref Dictionary<string, int> dict)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dict, Formatting.None)); }
            catch { bytes = Encoding.UTF8.GetBytes("{}"); }

            try { dataStore.SyncData(key, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[WEARINESS] save failed ({key}): {ex.Message}"); }
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

      private static int Valor(Hero h)
      {
         try { return h.GetTraitLevel(DefaultTraits.Valor); }
         catch { return 0; }
      }

      // Existing long-known companions get credit for time already served (their earliest memory), while a
      // brand-new recruit starts the clock now: without this, a companion recruited before this build would be
      // wrongly treated as a day-one recruit for the minimum-tenure gate.
      private void StampServiceIfNew(Hero h)
      {
         if (_servedSince.ContainsKey(h.StringId)) return;

         int earliest = Today;
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId);
         if (profile?.Events is {Count: > 0})
            earliest = profile.Events.Min(e => e.gameDay);

         _servedSince[h.StringId] = Math.Min(Today, earliest);
      }

      private bool DeclinedRecently(Hero h)
         => _declinedUntil.TryGetValue(h.StringId, out int until) && Today < until;

      private void Depart(Hero h)
      {
         try
         {
            RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, h);
            MBInformationManager.AddQuickInformation(
               new TextObject($"{h.Name} has laid down the sword and retired from your service, honorably. May the roads treat them kindly."),
               0, h.CharacterObject);
            ModLog.Info($"[WEARINESS] {h.Name} retired from your service (landless, honorable).");
         }
         catch (Exception ex) { ModLog.Error($"[WEARINESS] retirement departure failed for {h.Name}: {ex.Message}"); }
      }

      private void EnforceStepBack(Hero h)
      {
         // Only the PLAYER's marching host counts as "dragged back to war"; a governor sitting in their town or a
         // companion leading their own party is exactly what stepping back allows.
         bool draggedToWar = h.PartyBelongedTo == MobileParty.MainParty;
         if (!draggedToWar)
         {
            _forcedBack.Remove(h.StringId);

            return;
         }

         int n = (_forcedBack.TryGetValue(h.StringId, out int c)
                    ? c
                    : 0) +
                 1;
         _forcedBack[h.StringId] = n;

         // They resent being marched to war after you granted them their rest.
         CompanionHappinessService.ApplyDriver(h, HappinessDriver.Mistreated);
         MBInformationManager.AddQuickInformation(
            new TextObject($"{h.Name} chafes at being marched back to war after you granted them their rest."), 0, h.CharacterObject);

         if (n < ForcedBackToBreak) return;
         WarWearinessService.SetSteppedBack(h, false); // the honorable retirement is over — now just an unhappy soldier
         _forcedBack.Remove(h.StringId);
         MBInformationManager.AddQuickInformation(
            new TextObject($"{h.Name}'s patience is spent. The retirement you granted them means nothing now."), 0, h.CharacterObject);
         ModLog.Info($"[WEARINESS] {h.Name}'s step-back broke after being dragged back to war {n} time(s).");
      }

      // ── Wounds (a not-wounded → wounded transition, once per wound episode) ─

      private void OnDailyTick()
      {
         if (!ModSettings.CompanionContentment) return;

         foreach (Hero h in Companions())
         {
            Hero companion = h;
            // One companion's wound bookkeeping throwing must not skip the rest of the roster.
            SafeRun.Step($"WarWearinessBehavior.OnDailyTick({companion.Name})", () =>
            {
               bool wounded = companion.IsWounded;
               bool counted = _woundCounted.ContainsKey(companion.StringId);

               if (wounded && !counted)
               {
                  WarWearinessService.AddWound(companion);
                  _woundCounted[companion.StringId] = 1;
               }
               else if (!wounded && counted)
               {
                  _woundCounted.Remove(companion.StringId); // recovered, their next wound will count again
               }
            });
         }
      }

      private void OnDismiss(Hero h)
      {
         _requestOpen = false;
         _declinedUntil[h.StringId] = Today + RetireDeclineCooldownDays;
         // Turning a faithful, war-weary veteran away without a hearing stings.
         CompanionHappinessService.ApplyDriver(h, HappinessDriver.Mistreated);
         MBInformationManager.AddQuickInformation(
            new TextObject($"You have no time for {h.Name}. They turn away, the weariness of war still on them — and the slight of being unheard now with it."),
            0, h.CharacterObject);
      }

      // ── Captures ─────────────────────────────────────────────────────────

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         if (!ModSettings.CompanionContentment) return;
         if (prisoner == null || prisoner == Hero.MainHero || !prisoner.IsPlayerCompanion) return;

         WarWearinessService.AddCapture(prisoner); // being taken prisoner is the heaviest single toll
      }

      private void OnTalk(Hero h, bool landed)
      {
         _requestOpen = false;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId);
         if (profile == null)
         {
            ModLog.Info($"[WEARINESS] {h.Name} has no profile — cannot open the retirement audience; granting directly.");
            ExecuteRetirement(h, landed); // fail-soft: honor the request without the conversation

            return;
         }

         ChatManager.OpenForCompanionAudience(h, profile, CompanionAudienceReason.Retirement, landed);
      }

      // ── Weekly rest, retirement request, and the drag-back enforcement ────

      private void OnWeeklyTick()
      {
         if (!ModSettings.CompanionContentment) return;

         Hero toAsk = null;
         foreach (Hero h in Companions())
         {
            Hero companion = h;
            // One companion's weekly rest/retirement pass throwing must not skip the rest of the roster.
            SafeRun.Step($"WarWearinessBehavior.OnWeeklyTick({companion.Name})", () =>
            {
               StampServiceIfNew(companion);
               WarWearinessService.Rest(companion, RestStep); // time and rest ease the toll; heavy fighting outruns it

               // A veteran who already stepped back from the field: if the player has since marched them back to
               // war (into the main party), their granted rest curdles into resentment, and eventually breaks.
               if (WarWearinessService.HasSteppedBack(companion))
               {
                  EnforceStepBack(companion);

                  return; // a stepped-back companion is not asked to retire again
               }

               // A companion AWAY on an errand cannot be asked to retire mid-errand: they are abroad on your
               // business and cannot resolve to leave until they are back (else a retirement grant would depart
               // someone still away).
               bool away = CompanionMissionBehavior.Instance?.IsAway(companion.StringId) == true;

               // A companion cannot be asked to retire before 2 years of service (player report 2026-07-25):
               // a young recruit must never ask to leave in their first two years, however hard the toll.
               if (toAsk == null && CompanionLifecyclePolicy.MayDepartNow(away) &&
                   WarWeariness.WantsToRetire(WarWearinessService.Get(companion), Valor(companion)) &&
                   WarWeariness.HasServedLongEnough(DaysServed(companion)) && !DeclinedRecently(companion))
                  toAsk = companion;
            });
         }

         if (toAsk != null && !_requestOpen) PresentRetirement(toAsk); // at most one request per week
      }

      private void PresentRetirement(Hero h)
      {
         _requestOpen = true;
         bool landed = IsInvested(h);

         string body = landed
            ? $"{h.Name} has grown war-weary after long years at your side. They ask for a private word — to speak of " +
              "stepping back from the field to see to their own holdings, while remaining of your clan."
            : $"{h.Name} has grown war-weary after long years at your side. They ask for a private word — to speak of " +
              "laying down the sword and retiring from your service.";

         // Two roads: hear them out (opens a real conversation where you grant their leave or talk them round), or
         // turn them away (a slight that sours them and re-presents later).
         Interception.AudienceInquiry.Show(h, $"{h.Name} asks for a word", body, "Let us speak of it…", "I've no time for this.",
            () => OnTalk(h, landed),
            () => OnDismiss(h), sayWho: false);
      }

      #endregion
   }
}