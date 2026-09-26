// Code written by Gabriel Mailhot, 26/09/2026.
// Arrests Inc 2: the player's case, kept across saves, and the grave acts that open it (Gabriel's rulings, 26/09/2026;
// the rules are ArrestPolicy's and the case is ArrestCase, both pure). Three kinds of act bring a warrant in the liege's
// name, and only while the player is a vassal or a mercenary:
//
//   - a treasonous letter of theirs, read by a lord of their own realm (the courier secrets, CourierSecretBehavior);
//   - violence in conversation against a member of their own realm (ViolenceConsequenceService);
//   - the game's own crime rating in their realm turning SEVERE. Charged on the day it turns severe, not every day it
//     stays so: an acquittal wipes the charge, and a rating left severe must not bring the same warrant straight back.
//     Only a new grave act can (Gabriel).
//
// What the warrant then sets in motion (the sergeants, the trial, the outlaw) is the next increments. Additive save key.

#region

using System;
using System.Text;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Arrests
{
   /// <summary>Keeps the player's case and opens it when they do something grave.</summary>
   public sealed class ArrestBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_ArrestCase_v1bytes";

      public static ArrestBehavior Instance { get; private set; }

      private SavedState _state = new();

      public ArrestBehavior()
      {
         Instance = this;
      }

      /// <summary>The player's case as it stands.</summary>
      internal ArrestCase Case => _state.Case;

      public override void RegisterEvents()
         => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

      /// <summary>
      ///   A grave act. Opens a warrant in the liege's name, or adds to the case already open. Nothing happens for a
      ///   player who answers to no liege (a faction leader, or outside any realm). <paramref name="what" /> says it in
      ///   words, for the player and the log.
      /// </summary>
      public void Charge(ArrestCharge charge, string what)
      {
         try
         {
            Hero liege = Clan.PlayerClan?.Kingdom?.Leader;
            bool canBeArrested = ArrestPolicy.CanBeArrested(Standing()) && liege != null && liege != Hero.MainHero;
            bool wasOpen = Case.Stage != ArrestStage.None;

            Case.Charge(charge, liege?.StringId, canBeArrested);

            if (!canBeArrested) return;

            if (!wasOpen && Case.Stage == ArrestStage.WarrantOut)
            {
               MBInformationManager.AddQuickInformation(new TextObject($"Word reaches you: {liege.Name} has issued a warrant for your arrest, for {what}."));
               ModLog.Info($"[ARREST] warrant in {liege.Name}'s name: {charge} ({what}).");
            }
            else
            {
               ModLog.Info($"[ARREST] charge added to the open case: {charge} ({what}).");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] charging failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Where the player stands: a faction leader, a vassal, a mercenary, or none of these.</summary>
      internal static ArrestStandingFacts Standing()
      {
         Clan clan = Clan.PlayerClan;
         var kingdom = clan?.Kingdom;

         return new ArrestStandingFacts {
            IsFactionLeader = kingdom != null && kingdom.RulingClan == clan,
            IsMercenary = kingdom != null && clan.IsUnderMercenaryService,
            IsVassal = kingdom != null && !clan.IsUnderMercenaryService
         };
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_state, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[ARREST] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[ARREST] save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] loaded = null;
         try { dataStore.SyncData(SaveKey, ref loaded); }
         catch
         {
            /* key absent in older saves */
         }

         if (loaded is not {Length: > 0}) return;

         try
         {
            _state = JsonConvert.DeserializeObject<SavedState>(Encoding.UTF8.GetString(loaded)) ?? new SavedState();
            _state.Case ??= new ArrestCase();
         }
         catch (Exception ex)
         {
            _state = new SavedState();
            ModLog.Error($"[ARREST] load failed, the case is dropped for this load. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>A severe crime rating brings a warrant on the day it turns severe, once.</summary>
      private void OnDailyTick()
      {
         try
         {
            var kingdom = Clan.PlayerClan?.Kingdom;
            bool severe = kingdom != null && Campaign.Current?.Models?.CrimeModel?.IsPlayerCrimeRatingSevere(kingdom) == true;

            if (severe && !_state.CrimeWasSevere) Charge(ArrestCharge.SevereCrimeRating, "your crimes in the realm");
            _state.CrimeWasSevere = severe;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] daily tick failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>What is saved: the case, and whether the crime rating was already severe yesterday.</summary>
      private sealed class SavedState
      {
         public ArrestCase Case { get; set; } = new();
         public bool CrimeWasSevere { get; set; }
      }

      #endregion
   }
}
