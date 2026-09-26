// Code written by Gabriel Mailhot, 26/09/2026.
// Courier Inc 4a: an intercepted letter becomes a secret that travels in the world (raphareish, Nexus, 24/09/2026;
// Gabriel's ruling the same day). Not the old instant leak to an antagonist picked by a roll, but a thing a party
// carries:
//
//   - Bandits who take a courier CARRY its letter. Another band that beats them carries it on; a band that goes home
//     brings it to its hideout, where it waits.
//   - Whoever else beats the carrier sends it up to the lord who answers for that troop: the lord at its head, else its
//     owner (a caravan), else the owner of its home settlement (a Pravend patrol, Pravend's owner).
//   - A troop at war with the courier's clan that takes it directly hands it to its own lord at once.
//   - The player's side beating the carrier, or clearing the hideout that holds it, recovers the letter unread.
//   - A secret is only worth something for CourierSecretPolicy.SecretShelfLifeDays.
//
// Every decision is CourierSecretPolicy's (pure, tested); this is the engine half: reading parties, following the
// carrier, and filing what the lord who ends up with it now knows. What that lord then DOES about it, in proportion to
// what the letter says, is increment 4b. Additive save key.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>An intercepted letter, and where it is now.</summary>
   public sealed class CarriedSecret
   {
      /// <summary>The bandit party carrying it, or null once it rests in a hideout.</summary>
      public string CarrierPartyId { get; set; }

      /// <summary>The hideout it rests in, or null while a party carries it.</summary>
      public string HideoutId { get; set; }

      public string SenderId { get; set; }
      public string SenderName { get; set; }
      public string RecipientId { get; set; }
      public string RecipientName { get; set; }

      /// <summary>What the letter said.</summary>
      public string Content { get; set; }

      /// <summary>True when the player wrote it.</summary>
      public bool FromPlayer { get; set; }

      /// <summary>The day it was taken from its courier.</summary>
      public int CapturedDay { get; set; }
   }

   /// <summary>Follows intercepted letters from their captors to whoever ends up reading them.</summary>
   public sealed class CourierSecretBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_CourierSecrets_v1bytes";

      /// <summary>How close the player's party must stand to an emptied hideout to have cleared it (map units).</summary>
      private const float HideoutClearedByPlayerDistance = 3f;

      public static CourierSecretBehavior Instance { get; private set; }

      private List<CarriedSecret> _secrets = new();

      public CourierSecretBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>The secrets in the world now, for diagnostics and cr.testall.</summary>
      internal IReadOnlyList<CarriedSecret> Secrets => _secrets;

      public override void RegisterEvents()
      {
         CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
         CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
         CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(this, OnHideoutDeactivated);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      /// <summary>
      ///   A courier was intercepted and its letter is worth hiding (CourierSecretPolicy.BecomesSecret, checked by the
      ///   caller). Bandits carry it off; an enemy troop hands it straight to its own lord.
      /// </summary>
      public void Capture(CarriedSecret secret, PartyBase captor, CourierLoss loss)
      {
         try
         {
            if (secret == null || captor == null) return;

            secret.CapturedDay = Today;

            if (loss == CourierLoss.InterceptedByBandits && captor.MobileParty != null)
            {
               secret.CarrierPartyId = captor.MobileParty.StringId;
               _secrets.Add(secret);
               ModLog.Info($"[SECRET] {captor.Name} carry off {secret.SenderName}'s letter to {secret.RecipientName}.");

               return;
            }

            HandOff(secret, captor);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] capture failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_secrets, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[SECRET] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[SECRET] save failed: {ex.GetType().Name}: {ex.Message}"); }

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
            _secrets = JsonConvert.DeserializeObject<List<CarriedSecret>>(Encoding.UTF8.GetString(loaded)) ?? new List<CarriedSecret>();
         }
         catch (Exception ex)
         {
            _secrets = new List<CarriedSecret>();
            ModLog.Error($"[SECRET] load failed, secrets dropped for this load. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>The carrier was beaten, or vanished: its secrets pass to whoever answers for the victor.</summary>
      private void OnPartyDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         try
         {
            string id = destroyed?.StringId;
            if (string.IsNullOrEmpty(id)) return;

            List<CarriedSecret> carried = _secrets.Where(s => s.CarrierPartyId == id).ToList();
            foreach (CarriedSecret secret in carried)
            {
               _secrets.Remove(secret);
               HandOff(secret, destroyer);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] party-destroyed handling failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>A band carrying a secret goes home: the letter rests in its hideout.</summary>
      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         try
         {
            if (party == null || settlement is not {IsHideout: true}) return;

            foreach (CarriedSecret secret in _secrets.Where(s => s.CarrierPartyId == party.StringId))
            {
               secret.CarrierPartyId = null;
               secret.HideoutId = settlement.StringId;
               ModLog.Info($"[SECRET] {secret.SenderName}'s letter to {secret.RecipientName} now rests in {settlement.Name}.");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] settlement-entered handling failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   A hideout emptied. The engine says so however it emptied, so only a player standing at it has cleared it and
      ///   takes back what it held; otherwise the letters wait there for the next band.
      /// </summary>
      private void OnHideoutDeactivated(Settlement hideout)
      {
         try
         {
            if (hideout == null) return;

            MobileParty main = MobileParty.MainParty;
            bool playerClearedIt = main != null
                                   && (main.CurrentSettlement == hideout
                                       || main.GetPosition2D.Distance(hideout.GetPosition2D) <= HideoutClearedByPlayerDistance);
            if (!playerClearedIt) return;

            foreach (CarriedSecret secret in _secrets.Where(s => s.HideoutId == hideout.StringId).ToList())
            {
               _secrets.Remove(secret);
               Recover(secret);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] hideout handling failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Old secrets stop mattering; a carrier gone without a trace takes its letter with it.</summary>
      private void OnDailyTick()
      {
         try
         {
            int today = Today;
            int dropped = _secrets.RemoveAll(s =>
               CourierSecretPolicy.IsStale(s.CapturedDay, today)
               || (s.HideoutId == null && (string.IsNullOrEmpty(s.CarrierPartyId) || MobileParty.All.All(p => p.StringId != s.CarrierPartyId))));

            if (dropped > 0) ModLog.Info($"[SECRET] {dropped} secret(s) no longer matter or were lost with their carriers.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] daily tick failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Passes a secret to whoever answers for <paramref name="party" />, as CourierSecretPolicy decides.</summary>
      private void HandOff(CarriedSecret secret, PartyBase party)
      {
         SecretHandoff handoff = CourierSecretPolicy.WhoGetsTheSecret(DefeaterFacts(party, secret));

         switch (handoff.Kind)
         {
            case SecretHandoffKind.CarriedOn:
               secret.CarrierPartyId = party?.MobileParty?.StringId;
               secret.HideoutId = null;
               if (secret.CarrierPartyId != null) _secrets.Add(secret);
               ModLog.Info($"[SECRET] {party?.Name} take {secret.SenderName}'s letter from the band that held it.");

               break;
            case SecretHandoffKind.RecoveredByPlayer:
               Recover(secret);

               break;
            case SecretHandoffKind.Lord:
               Hero reader = Hero.FindFirst(h => h != null && h.StringId == handoff.HeroId);
               if (reader is {IsAlive: true}) Learn(secret, reader);

               break;
            default:
               ModLog.Info($"[SECRET] {secret.SenderName}'s letter to {secret.RecipientName} reaches no one ({handoff.Kind}).");

               break;
         }
      }

      /// <summary>What CourierSecretPolicy needs to know about the troop that took a secret.</summary>
      private static SecretDefeaterFacts DefeaterFacts(PartyBase party, CarriedSecret secret)
      {
         if (party == null) return null;

         MobileParty mobile = party.MobileParty;
         Clan playerClan = Clan.PlayerClan;
         bool playerSide = party == PartyBase.MainParty
                           || (playerClan != null && (mobile?.ActualClan == playerClan || party.Owner?.Clan == playerClan));

         Settlement home = mobile?.HomeSettlement ?? party.Settlement;

         return new SecretDefeaterFacts {
            IsPlayerSide = playerSide,
            IsBandit = mobile?.IsBandit == true,
            LeaderHeroId = party.LeaderHero?.StringId,
            OwnerHeroId = party.Owner?.StringId,
            SettlementOwnerHeroId = home?.OwnerClan?.Leader?.StringId,
            IntendedRecipientId = secret.RecipientId,
            SenderId = secret.SenderId
         };
      }

      /// <summary>The player's side took the letter back, unread by anyone.</summary>
      private static void Recover(CarriedSecret secret)
      {
         string text = secret.FromPlayer
            ? $"Among the bandits' loot you find your own letter to {secret.RecipientName}. Its words are safe."
            : $"Among the bandits' loot you find the letter {secret.SenderName} once sent you.";
         MBInformationManager.AddQuickInformation(new TextObject(text));
         ModLog.Info($"[SECRET] recovered by the player: {secret.SenderName}'s letter to {secret.RecipientName}.");
      }

      /// <summary>
      ///   A lord now holds the letter: they remember whose it was, to whom, and what it said, and word of it gets about.
      ///   What they then do about it, in proportion to what it said, is increment 4b.
      /// </summary>
      private static void Learn(CarriedSecret secret, Hero reader)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.Store == null) return;

            NpcProfile profile = behavior.Store.Get(reader.StringId);
            if (profile == null)
            {
               profile = HeroProfileMapper.FromHero(reader);
               behavior.Store.Set(profile);
            }

            int today = Today;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
               CourierSecretPolicy.ReaderMemory(secret.SenderName, secret.RecipientName, secret.Content)));
            behavior.Store.Set(profile);

            WorldEventStore.Instance?.Record(new WorldEvent {
               Type = WorldEventType.Other,
               Day = today,
               InvolvesPlayer = true,
               Magnitude = 25,
               Status = "letter_leaked",
               Summary = $"A private letter {secret.SenderName} sent to {secret.RecipientName} was taken on the road and came into {reader.Name}'s hands.",
               ActorIds = new List<string> {reader.StringId},
               FactionIds = new List<string>()
            });

            ModLog.Info($"[SECRET] {reader.Name} now knows what {secret.SenderName} wrote to {secret.RecipientName}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] learning failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}
