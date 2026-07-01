// Code written by Gabriel Mailhot, 30/06/2026.
// Companion war-weariness & honorable retirement — the engine glue. Wounds (a daily IsWounded transition) and
// captures (HeroPrisonerTaken) accrue the toll; a weekly rest eases it. When a companion is spent (past their
// Valor-scaled tolerance) they ASK to retire: a LANDLESS one leaves your service warmly; a LANDED one (a
// governor or party-leader) steps back from the FIELD to tend their fief but stays in the clan. Drag a
// stepped-back veteran back to war and their retirement curdles into real unhappiness (the contentment system
// then takes over). The bands/tolls/tolerance are the pure WarWeariness model; everything here is glue. Gated by
// the same "Companion contentment" MCM toggle as the happiness/retention loop.

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
      // Tuning (flagged — Gabriel adjusts by feel).
      private const int RestStep = WarWeariness.RestStepPerWeek; // weekly recovery
      private const int RetireDeclineCooldownDays = 14;          // after a decline, wait before asking again
      private const int ForcedBackToBreak = 3;                   // weeks dragged back to war before the retirement breaks

      private const string DeclinedSaveKey = "CalradiaRemembers_WearinessDeclined_v1";
      private const string ForcedBackSaveKey = "CalradiaRemembers_WearinessForcedBack_v1";
      private const string WoundCountedSaveKey = "CalradiaRemembers_WearinessWounded_v1";
      public static WarWearinessBehavior Instance;

      private Dictionary<string, int> _declinedUntil = new(); // companion id → day we may ask again
      private Dictionary<string, int> _forcedBack = new();    // stepped-back id → weeks dragged back to war so far
      private Dictionary<string, int> _woundCounted = new();  // companion id → 1 while their current wound is counted
      private bool _requestOpen;                              // an inquiry is on screen; don't stack another

      public WarWearinessBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

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

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      private static int Valor(Hero h)
      {
         try { return h.GetTraitLevel(DefaultTraits.Valor); }
         catch { return 0; }
      }

      /// <summary>A companion is INVESTED (never leaves) when they govern a settlement or lead a party of their own.</summary>
      private static bool IsInvested(Hero h)
         => h.GovernorOf != null || (h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h);

      // ── Captures ─────────────────────────────────────────────────────────

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         if (!ModSettings.CompanionContentment) return;
         if (prisoner == null || prisoner == Hero.MainHero || !prisoner.IsPlayerCompanion) return;

         WarWearinessService.AddCapture(prisoner); // being taken prisoner is the heaviest single toll
      }

      // ── Wounds (a not-wounded → wounded transition, once per wound episode) ─

      private void OnDailyTick()
      {
         if (!ModSettings.CompanionContentment) return;

         foreach (Hero h in Companions())
         {
            bool wounded = h.IsWounded;
            bool counted = _woundCounted.ContainsKey(h.StringId);

            if (wounded && !counted)
            {
               WarWearinessService.AddWound(h);
               _woundCounted[h.StringId] = 1;
            }
            else if (!wounded && counted)
            {
               _woundCounted.Remove(h.StringId); // recovered — their next wound will count again
            }
         }
      }

      // ── Weekly rest, retirement request, and the drag-back enforcement ────

      private void OnWeeklyTick()
      {
         if (!ModSettings.CompanionContentment) return;

         Hero toAsk = null;
         foreach (Hero h in Companions())
         {
            WarWearinessService.Rest(h, RestStep); // time and rest ease the toll; heavy fighting outruns it

            // A veteran who already stepped back from the field: if the player has since marched them back to
            // war (into the main party), their granted rest curdles into resentment, and eventually breaks.
            if (WarWearinessService.HasSteppedBack(h))
            {
               EnforceStepBack(h);

               continue; // a stepped-back companion is not asked to retire again
            }

            if (toAsk == null
                && WarWeariness.WantsToRetire(WarWearinessService.Get(h), Valor(h))
                && !DeclinedRecently(h))
               toAsk = h;
         }

         if (toAsk != null && !_requestOpen) PresentRetirement(toAsk); // at most one request per week
      }

      private bool DeclinedRecently(Hero h)
         => _declinedUntil.TryGetValue(h.StringId, out int until) && Today < until;

      private void EnforceStepBack(Hero h)
      {
         // Only the PLAYER's marching host counts as "dragged back to war"; a governor sitting in their town or a
         // companion leading their own party is exactly what stepping back allows.
         bool draggedToWar = h.PartyBelongedTo == MobileParty.MainParty;
         if (!draggedToWar) { _forcedBack.Remove(h.StringId); return; }

         int n = (_forcedBack.TryGetValue(h.StringId, out int c) ? c : 0) + 1;
         _forcedBack[h.StringId] = n;

         // They resent being marched to war after you granted them their rest.
         CompanionHappinessService.ApplyDriver(h, HappinessDriver.Mistreated);
         MBInformationManager.AddQuickInformation(
            new TextObject($"{h.Name} chafes at being marched back to war after you granted them their rest."), 0, h.CharacterObject);

         if (n >= ForcedBackToBreak)
         {
            WarWearinessService.SetSteppedBack(h, false); // the honorable retirement is over — now just an unhappy soldier
            _forcedBack.Remove(h.StringId);
            MBInformationManager.AddQuickInformation(
               new TextObject($"{h.Name}'s patience is spent. The retirement you granted them means nothing now."), 0, h.CharacterObject);
            ModLog.Info($"[WEARINESS] {h.Name}'s step-back broke after being dragged back to war {n} time(s).");
         }
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
         InformationManager.ShowInquiry(new InquiryData(
            $"{h.Name} asks for a word",
            body,
            true, true,
            "Let us speak of it…", "I've no time for this.",
            () => OnTalk(h, landed),
            () => OnDismiss(h)), true);
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

      #endregion
   }
}
