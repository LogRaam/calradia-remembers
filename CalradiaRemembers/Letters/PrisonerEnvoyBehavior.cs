// Code written by Gabriel Mailhot, 25/09/2026.
// Prisoner exchange, increment 3c: the envoy arrives. fkasad (Nexus, 25/09/2026): a ransom offer was a letter with
// Accept or Refuse, "several llm calls for no interactions", where a lord who wants one of his own back "would dispatch a
// trusted agent to negotiate". Gabriel's rulings: always an envoy, never the lord in person; anonymous; announced by a
// popup. This replaces the PrisonerOffer letter: the same lord, the same captive, the same fair ceiling
// (PrisonerOfferPolicy), now carried by an envoy the player can talk to, who may also offer an exchange.
//
// The mandate is composed here, once, when the envoy is sent: whom the lord wants, the most he will pay, and which of
// his own captives he would trade (PrisonerExchangePolicy.ChooseCounterpart, the player's own people first). The popup
// waits until the player stands free on the open map (InterceptionBehavior.PlayerIsFreeOnMap), and one envoy per captive
// is sent at most every PrisonerExchangePolicy.EnvoyCooldownDays, heard or turned away. Additive save key.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Verbs;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Sends and announces envoys come to treat for a captive the player holds.</summary>
   public sealed class PrisonerEnvoyBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_PrisonerEnvoys_v1bytes";

      public static PrisonerEnvoyBehavior Instance { get; private set; }

      // "captiveId|lordId" -> the day that lord's envoy last came (or was sent) about that captive. Per pair, so a
      // father's envoy and a bitter enemy's can each come about the same prisoner (fkasad, increment 4).
      private Dictionary<string, int> _lastEnvoyDay = new();

      // The envoy waiting for the player to be free enough to receive him. In memory only: a reload before the
      // popup simply lets the next daily evaluation send him again once the cooldown allows.
      private PendingEnvoy _pending;

      public PrisonerEnvoyBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
         => CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

      /// <summary>Whether this lord's envoy may come about this captive today (the letter evaluator asks first).</summary>
      public bool EnvoyMayCome(string captiveId, string lordId)
         => !string.IsNullOrEmpty(captiveId) && !string.IsNullOrEmpty(lordId)
            && PrisonerExchangePolicy.EnvoyMayCome(_lastEnvoyDay.TryGetValue(Key(captiveId, lordId), out int d) ? d : (int?) null, Today);

      private static string Key(string captiveId, string lordId) => captiveId + "|" + lordId;

      /// <summary>
      ///   Sends <paramref name="lord" />'s envoy about <paramref name="captive" />, offering up to
      ///   <paramref name="ransomCeiling" />. The cooldown starts now; the popup comes when the player is free.
      /// </summary>
      public void Send(Hero lord, Hero captive, int ransomCeiling, string stake, bool wantsCaptiveBack)
      {
         if (lord == null || captive == null || _pending != null) return;

         var mandate = new EnvoyMandate {
            WantedCaptiveName = captive.Name?.ToString() ?? "",
            RansomCeiling = ransomCeiling
         };

         int exchangeGold = 0;
         Hero counterpart = ChooseCounterpart(lord, captive);
         if (counterpart != null)
         {
            mandate.ExchangeCaptiveName = counterpart.Name?.ToString() ?? "";
            exchangeGold = PrisonerExchangePolicy.GoldToSettle(BuyPrisonerVerb.RansomAnchor(captive), BuyPrisonerVerb.RansomAnchor(counterpart));
         }

         _lastEnvoyDay[Key(captive.StringId, lord.StringId)] = Today;
         _pending = new PendingEnvoy {
            LordId = lord.StringId,
            CaptiveId = captive.StringId,
            Audience = new EnvoyAudience {
               LordId = lord.StringId,
               LordRealm = lord.MapFaction?.Name?.ToString(),
               WantedStake = stake,
               WantsCaptiveBack = wantsCaptiveBack,
               ExchangeGoldToPlayer = exchangeGold,
               OpeningOffer = EnvoyMandatePolicy.OpeningOffer(ransomCeiling, PrisonerTradePolicy.MinAcceptablePayment(BuyPrisonerVerb.RansomAnchor(captive))),
               Mandate = mandate
            }
         };

         ModLog.Info($"[ENVOY] {lord.Name} sends an envoy about {captive.Name} (ceiling {ransomCeiling}"
                   + (counterpart != null ? $", or {counterpart.Name} in exchange, gold to the player {exchangeGold}" : "") + ").");
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_lastEnvoyDay, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[ENVOY] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[ENVOY] save failed: {ex.GetType().Name}: {ex.Message}"); }

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
            _lastEnvoyDay = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(loaded)) ?? new Dictionary<string, int>();
         }
         catch (Exception ex)
         {
            _lastEnvoyDay = new Dictionary<string, int>();
            ModLog.Error($"[ENVOY] load failed, cooldowns dropped for this load. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      private void OnHourlyTick()
      {
         if (_pending == null || !InterceptionBehavior.PlayerIsFreeOnMap() || Hero.MainHero?.IsPrisoner == true) return;

         // The envoy rides up; at sea he cannot (Windmolen, 28/09/2026: accepting him on a naval move crashed the
         // game). He waits, and comes on the first hour the player is ashore.
         if (!StagingHostPolicy.AnEnvoyCanReachThePlayer(MobileParty.MainParty?.IsCurrentlyAtSea == true)) return;

         PendingEnvoy envoy = _pending;
         _pending = null;

         Hero lord = Hero.FindFirst(h => h != null && h.StringId == envoy.LordId);
         bool stillHeld = BuyPrisonerVerb.PlayerHeldHeroPrisoners().Any(h => h.StringId == envoy.CaptiveId);

         // The world moved while the envoy rode: the lord is gone or himself a prisoner (fkasad, 26/09/2026: his queen's
         // envoy came while she sat in a cell, and no deal could run in her name), or the captive is no longer the player's.
         if (lord == null || !lord.IsAlive || lord.IsPrisoner || !stillHeld)
         {
            ModLog.Info("[ENVOY] the envoy turned back: the lord or the captive is no longer where the errand assumed.");

            return;
         }

         string envoyName = EnvoyMandatePolicy.DisplayName(lord.Name?.ToString());
         CharacterObject portrait = lord.Clan?.BasicTroop ?? lord.Culture?.BasicTroop;

         InformationManager.ShowInquiry(new InquiryData(
            "An envoy under a flag of truce",
            $"An envoy of {lord.Name} rides up under a flag of truce and asks to speak with you about {envoy.Audience.Mandate.WantedCaptiveName}, whom you hold.",
            true, true,
            "Receive the envoy", "Send them away",
            () => MainThreadDispatcher.Enqueue(() => ChatSceneStager.StageEnvoyAudience(envoyName, portrait, envoy.Audience)),
            () => ModLog.Info($"[ENVOY] the player sent {lord.Name}'s envoy away.")), true);
      }

      /// <summary>
      ///   Which of the captives the lord's clan holds he would trade: the player's own people first, then the closest
      ///   in worth (PrisonerExchangePolicy). Null when he holds nobody exchangeable.
      /// </summary>
      private static Hero ChooseCounterpart(Hero lord, Hero wanted)
      {
         List<Hero> held = CourtActionResolver.ExchangeablePrisonersHeldBy(lord);
         if (held.Count == 0) return null;

         var facts = held.Select(h => new ExchangeCaptiveFacts {
            HeroId = h.StringId,
            IsHero = true,
            IsTheWantedCaptive = h == wanted,
            IsLord = h.IsLord,
            IsCompanion = h.CompanionOf != null || h.IsWanderer,
            IsAlive = h.IsAlive,
            IsChild = h.IsChild,
            RansomAnchor = BuyPrisonerVerb.RansomAnchor(h),
            BelongsToPlayer = h.Clan == Clan.PlayerClan
         }).ToList();

         ExchangeCaptiveFacts chosen = PrisonerExchangePolicy.ChooseCounterpart(facts, BuyPrisonerVerb.RansomAnchor(wanted));

         return chosen == null ? null : held.FirstOrDefault(h => h.StringId == chosen.HeroId);
      }

      private sealed class PendingEnvoy
      {
         public string LordId;
         public string CaptiveId;
         public EnvoyAudience Audience;
      }

      #endregion
   }
}
