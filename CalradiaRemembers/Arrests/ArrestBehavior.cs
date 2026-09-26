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
// Inc 3, the sergeants: while a warrant is out, a party of the liege's own troops rides from the realm's town nearest
// the player and pursues them. When it reaches them (the same "one party has reached another" range the interception
// uses, checked every frame since a pursuer can cross it within an hour), a popup reads the warrant: surrender, and be
// taken to the liege for trial; refuse, and be named an outlaw. Fighting them off is refusing. Like the couriers, the
// sergeants are never saved: they are despawned before a save and ride out again after a load. What surrender and
// refusal then set in motion (the trial, the outlaw) is Inc 4 and 5. Additive save key.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Travel;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Arrests
{
   /// <summary>Keeps the player's case and opens it when they do something grave.</summary>
   public sealed class ArrestBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_ArrestCase_v1bytes";

      public static ArrestBehavior Instance { get; private set; }

      /// <summary>TUNE: the sergeants' number, enough to look like the law, not an army.</summary>
      private const int SergeantCount = 12;

      /// <summary>TUNE: their base speed, above a courier's, so a player on the road cannot simply outride the law.</summary>
      private const float SergeantSpeed = 8f;

      /// <summary>The range at which one party has reached another (InterceptionBehavior.ArrivalRange, CourierService.ArrivalDistance).</summary>
      private const float ArrivalRange = 2f;

      private SavedState _state = new();

      // The sergeants on the road now. Never saved: despawned before a save, sent again after a load.
      private string _sergeantsId;
      private bool _despawning;

      public ArrestBehavior()
      {
         Instance = this;
      }

      /// <summary>The player's case as it stands.</summary>
      internal ArrestCase Case => _state.Case;

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
         CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
         CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, DespawnSergeants);
         CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
      }

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

      /// <summary>While a warrant is out, the sergeants ride: sent if none are on the road, and steered at the player.</summary>
      private void OnHourlyTick()
      {
         try
         {
            if (Case.Stage != ArrestStage.WarrantOut)
            {
               DespawnSergeants();

               return;
            }

            MobileParty main = MobileParty.MainParty;
            if (main == null || Hero.MainHero.IsPrisoner || main.IsCurrentlyAtSea) return;

            MobileParty sergeants = Sergeants() ?? SendSergeants();
            if (sergeants != null) TravelingParty.DirectToMovingPoint(sergeants, main.GetPosition2D);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] hourly tick failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The sergeants reach the player: the warrant is read, once they stand free on dry land.</summary>
      private void OnTick(float dt)
      {
         try
         {
            if (Case.Stage != ArrestStage.WarrantOut || string.IsNullOrEmpty(_sergeantsId)) return;

            MobileParty sergeants = Sergeants();
            MobileParty main = MobileParty.MainParty;
            if (sergeants == null || main == null) return;
            if (sergeants.GetPosition2D.Distance(main.GetPosition2D) > ArrivalRange) return;
            if (!InterceptionBehavior.PlayerIsFreeOnMap() || main.IsCurrentlyAtSea) return;
            if (main.Army != null && main.Army.LeaderParty != main) return; // never force a scene on an army subordinate

            DespawnSergeants();
            ReadTheWarrant();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] arrival check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The warrant, read by the sergeants' captain: surrender, or refuse and be an outlaw.</summary>
      private void ReadTheWarrant()
      {
         Hero liege = Hero.FindFirst(h => h != null && h.StringId == Case.LiegeId);
         string lord = liege?.Name?.ToString() ?? "your liege";
         string charges = ArrestPolicy.Describe(Case.Charges);

         InformationManager.ShowInquiry(new InquiryData(
            $"In {lord}'s name",
            $"Sergeants of {lord} bar your road. Their captain unrolls a warrant: you are to answer before {lord} for {charges}.\n\n" +
            $"Surrender, and you will be taken to {lord} for judgement. Refuse, and you will be named an outlaw, with a price on your head.",
            true, true, "Surrender", "Refuse",
            Surrender, Refuse), true);
         ModLog.Info($"[ARREST] the sergeants reach the player and read the warrant ({charges}).");
      }

      /// <summary>The player gives themselves up. What follows (the road to the liege, the trial) is Inc 4.</summary>
      private void Surrender()
      {
         Case.Taken();
         ModLog.Info("[ARREST] the player surrenders to the sergeants.");
      }

      /// <summary>The player refuses the warrant, or fights the sergeants off: an outlaw. What follows is Inc 5.</summary>
      private void Refuse()
      {
         Case.Flee();
         MBInformationManager.AddQuickInformation(new TextObject("You refuse the warrant. From this day you are an outlaw, and there is a price on your head."));
         ModLog.Info("[ARREST] the player refuses the warrant: outlaw, with a bounty.");
      }

      /// <summary>
      ///   Sends the sergeants from the liege's realm's town nearest the player (else from the liege's own position), in
      ///   the liege's colours and culture.
      /// </summary>
      private MobileParty SendSergeants()
      {
         Hero liege = Hero.FindFirst(h => h != null && h.StringId == Case.LiegeId);
         MobileParty main = MobileParty.MainParty;
         if (liege == null || main == null) return null;

         Vec2 player = main.GetPosition2D;
         Settlement origin = Settlement.All
                                       .Where(s => s.IsTown && s.MapFaction != null && s.MapFaction == liege.MapFaction)
                                       .OrderBy(s => s.GetPosition2D.Distance(player))
                                       .FirstOrDefault();
         Vec2 from = origin?.GetPosition2D ?? liege.GetMapPoint()?.Position.ToVec2() ?? player;

         MobileParty sergeants = TravelingParty.Spawn(from, origin ?? liege.HomeSettlement, new TextObject($"Sergeants of {liege.Name}"),
            SergeantSpeed, liege.Culture, false, SergeantCount, liege.Clan, liege);
         if (sergeants == null) return null;

         _sergeantsId = sergeants.StringId;
         ModLog.Info($"[ARREST] sergeants of {liege.Name} ride from {origin?.Name?.ToString() ?? "the liege's side"} after the player.");

         return sergeants;
      }

      private MobileParty Sergeants()
         => string.IsNullOrEmpty(_sergeantsId) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == _sergeantsId && p.IsActive);

      /// <summary>Takes the sergeants off the map (before a save, on arrival, or once no warrant is out).</summary>
      private void DespawnSergeants()
      {
         MobileParty sergeants = Sergeants();
         _sergeantsId = null;
         if (sergeants == null) return;

         _despawning = true;
         try { TravelingParty.Destroy(sergeants); }
         catch (Exception ex) { ModLog.Error($"[ARREST] despawning the sergeants failed: {ex.GetType().Name}: {ex.Message}"); }
         finally { _despawning = false; }
      }

      /// <summary>Beating the sergeants off is refusing the warrant.</summary>
      private void OnPartyDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         if (_despawning || destroyed == null || destroyed.StringId != _sergeantsId) return;

         _sergeantsId = null;
         bool byPlayer = destroyer == PartyBase.MainParty || destroyer?.MobileParty?.ActualClan == Clan.PlayerClan;
         if (byPlayer && Case.Stage == ArrestStage.WarrantOut) Refuse();
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
