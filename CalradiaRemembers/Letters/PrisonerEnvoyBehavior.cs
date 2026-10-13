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
      private const string CameSaveKey = "CR_PrisonerEnvoysCame_v1bytes";

      public static PrisonerEnvoyBehavior Instance { get; private set; }

      // "captiveId|lordId" -> the day that lord's envoy last came (or was sent) about that captive. Per pair, so a
      // father's envoy and a bitter enemy's can each come about the same prisoner (fkasad, increment 4).
      private Dictionary<string, int> _lastEnvoyDay = new();

      // "captiveId|captureDay" -> how many envoys reached the player about that captive in that captivity (fkasad,
      // 12/10/2026: "a no is a no"). Keyed by the day he was taken, so a man freed and taken again starts over.
      private Dictionary<string, int> _envoysCame = new();

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
      public bool EnvoyMayCome(Hero captive, string lordId)
         => captive != null && MayCome(captive.StringId, CaptureDay(captive), lordId, Today);

      /// <summary>
      ///   The rule on the recorded days and counts (PrisonerExchangePolicy.EnvoyMayCome): this lord's own wait, the wait
      ///   after ANY envoy about the captive, and no more envoys once enough came and left without him.
      /// </summary>
      internal bool MayCome(string captiveId, int captureDay, string lordId, int today)
      {
         if (string.IsNullOrEmpty(captiveId) || string.IsNullOrEmpty(lordId)) return false;

         string prefix = captiveId + "|";
         int? lastAbout = null, lastAny = null;
         foreach (KeyValuePair<string, int> e in _lastEnvoyDay)
         {
            if (lastAny == null || e.Value > lastAny) lastAny = e.Value;
            if (e.Key.StartsWith(prefix, StringComparison.Ordinal) && (lastAbout == null || e.Value > lastAbout))
               lastAbout = e.Value;
         }

         return PrisonerExchangePolicy.EnvoyMayCome(new EnvoyVisitFacts {
            LastFromThisLordDay = _lastEnvoyDay.TryGetValue(Key(captiveId, lordId), out int own) ? own : null,
            LastAboutTheCaptiveDay = lastAbout,
            LastAboutAnyCaptiveDay = lastAny,
            EnvoysCameThisCaptivity = _envoysCame.TryGetValue(CaptivityKey(captiveId, captureDay), out int came) ? came : 0,
            Today = today
         });
      }

      /// <summary>Envoys who reached the player about this captive in this captivity, each sent away (he is still held).</summary>
      public int EnvoysTurnedAway(Hero captive)
         => captive != null && _envoysCame.TryGetValue(CaptivityKey(captive.StringId, CaptureDay(captive)), out int n) ? n : 0;

      /// <summary>An envoy reached the player about this captive (the popup): one more who will leave without him unless a deal is struck.</summary>
      internal void RecordCame(string captiveId, int captureDay)
      {
         string key = CaptivityKey(captiveId, captureDay);
         _envoysCame[key] = (_envoysCame.TryGetValue(key, out int n) ? n : 0) + 1;
      }

      /// <summary>Self-test seam: an envoy sent by <paramref name="lordId" /> on <paramref name="day" />.</summary>
      internal void RecordSent(string captiveId, string lordId, int day) => _lastEnvoyDay[Key(captiveId, lordId)] = day;

      /// <summary>Self-test cleanup: forgets everything recorded about <paramref name="captiveId" />.</summary>
      internal void Forget(string captiveId)
      {
         string prefix = captiveId + "|";
         foreach (string k in _lastEnvoyDay.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList()) _lastEnvoyDay.Remove(k);
         foreach (string k in _envoysCame.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList()) _envoysCame.Remove(k);
      }

      private static string Key(string captiveId, string lordId) => captiveId + "|" + lordId;

      private static string CaptivityKey(string captiveId, int captureDay) => captiveId + "|" + captureDay;

      private static int CaptureDay(Hero captive) => (int) captive.CaptivityStartTime.ToDays;

      /// <summary>
      ///   Sends <paramref name="lord" />'s envoy about <paramref name="captive" />, offering up to
      ///   <paramref name="ransomCeiling" />. The cooldown starts now; the popup comes when the player is free.
      /// </summary>
      public void Send(Hero lord, Hero captive, int ransomCeiling, string stake, bool wantsCaptiveBack, bool comesAgainWithMore = false)
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
               ComesAgainWithMore = comesAgainWithMore,
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

            byte[] came;
            try { came = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_envoysCame, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[ENVOY] serialize of the envoy counts failed, persisting none this save. {ex.GetType().Name}: {ex.Message}");
               came = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(CameSaveKey, ref came); }
            catch (Exception ex) { ModLog.Error($"[ENVOY] save of the envoy counts failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         // Additive (12/10/2026): absent in older saves, where no envoy has been counted yet.
         byte[] cameLoaded = null;
         try { dataStore.SyncData(CameSaveKey, ref cameLoaded); }
         catch
         {
            /* key absent in older saves */
         }

         try
         {
            _envoysCame = cameLoaded is {Length: > 0}
               ? JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(cameLoaded)) ?? new Dictionary<string, int>()
               : new Dictionary<string, int>();
         }
         catch (Exception ex)
         {
            _envoysCame = new Dictionary<string, int>();
            ModLog.Error($"[ENVOY] load of the envoy counts failed, counts dropped for this load. {ex.GetType().Name}: {ex.Message}");
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
         // Re-asked with the kind it was sent under: the lord may have been taken out of the world as the envoy rode.
         if (!Mapping.ActionGate.Allows(lord, Logic.ActionKind.Letter) || !stillHeld)
         {
            ModLog.Info("[ENVOY] the envoy turned back: the lord or the captive is no longer where the errand assumed.");

            return;
         }

         Hero captive = BuyPrisonerVerb.PlayerHeldHeroPrisoners().First(h => h.StringId == envoy.CaptiveId);
         RecordCame(captive.StringId, CaptureDay(captive));

         string envoyName = EnvoyMandatePolicy.DisplayName(lord.Name?.ToString());
         CharacterObject portrait = lord.Clan?.BasicTroop ?? lord.Culture?.BasicTroop;

         // The envoy is nobody; the lord who sends them is who the player weighs (Lordfadooboo, 13/10/2026: "who is that?").
         Interception.AudienceInquiry.Show(lord, "An envoy under a flag of truce",
            $"An envoy of {lord.Name} rides up under a flag of truce and asks to speak with you about {envoy.Audience.Mandate.WantedCaptiveName}, whom you hold.",
            "Receive the envoy", "Send them away",
            () => MainThreadDispatcher.Enqueue(() => ChatSceneStager.StageEnvoyAudience(envoyName, portrait, envoy.Audience)),
            () => ModLog.Info($"[ENVOY] the player sent {lord.Name}'s envoy away."));
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
