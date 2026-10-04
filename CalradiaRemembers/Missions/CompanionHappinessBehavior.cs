// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
      private const int DeliveryDays = 3; // courier time for a farewell letter

      // The weekly easing toward the clan's resting happiness now lives with the model it must outweigh
      // (CompanionHappiness.WeeklyDriftStep > the idle penalty), so the balance invariant stays testable.
      private const string FarewellSaveKey = "CalradiaRemembers_CompanionFarewellDay_v1";

      // Additive save key: absent in older saves, which simply read as "never heard" and lift on the next talk.
      private const string HeardSaveKey = "CalradiaRemembers_CompanionHeardDay_v1";

      private const string RefusalsSaveKey = "CalradiaRemembers_CompanionRefusals_v1";
      public static CompanionHappinessBehavior Instance;
      private Dictionary<string, int> _farewellDay = new(); // companion id → day the farewell arrives (they leave then)
      private Dictionary<string, int> _heardDay = new();    // companion id → day the player last truly heard them

      private Dictionary<string, int> _refusals = new(); // companion id → refused audiences so far

      public CompanionHappinessBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

         // The deeds of ordinary play now count: a battle won at your side, a post handed over. Before this,
         // only the mod's own errands ever lifted a companion, so a player who fought and governed with them
         // still watched them sour (player report, SuperGraham339).
         CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
         CampaignEvents.OnGovernorChangedEvent.AddNonSerializedListener(this, OnGovernorChanged);
      }

      /// <summary>
      ///   The player sat and talked with this companion: being heard lifts their spirits. Rate-limited by
      ///   <see cref="CompanionConversationPacing" /> so reopening the chat window cannot be farmed into devotion.
      ///   Called from the chat as it closes.
      /// </summary>
      public void NoteHeardOut(Hero companion)
      {
         if (!ModSettings.CompanionContentment) return;
         if (companion is not {IsAlive: true, IsPlayerCompanion: true}) return;

         int last = _heardDay.TryGetValue(companion.StringId, out int d)
            ? d
            : CompanionConversationPacing.NeverHeard;

         if (!CompanionConversationPacing.LiftsSpirits(last, Today)) return;

         _heardDay[companion.StringId] = Today;
         CompanionHappinessService.ApplyDriver(companion, HappinessDriver.HeardOut);
      }

      /// <summary>Runs a happiness tick on demand (console test path).</summary>
      public void RunTick() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore)
      {
         SyncDict(dataStore, RefusalsSaveKey, ref _refusals);
         SyncDict(dataStore, FarewellSaveKey, ref _farewellDay);
         SyncDict(dataStore, HeardSaveKey, ref _heardDay);
      }

      #region private

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

      private static string FarewellLetter(Hero h)
         => "I have asked, more than once, for a moment of your time, and each time you had none to spare. " +
            "I take the lesson. By the time this letter reaches you I will already be gone from your service. " +
            "I hold no great grudge, but I will wait upon you no longer. May the roads treat you well.";

      private static bool IsKeptFromLovedOne(Hero h)
      {
         Hero spouse = h.Spouse;

         return spouse is {IsAlive: true} && spouse != Hero.MainHero && spouse.PartyBelongedTo != h.PartyBelongedTo;
      }

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

      private void GrantAudience(Hero h)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId);
         if (profile == null)
         {
            _refusals[h.StringId] = CompanionRetentionPolicy.OnAudienceGranted(); // heard at once: no window to wait for
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
         // The escalation resets only when they are actually heard (EnjoyingHydra, 03/10/2026): an audience that
         // never opened left the grievance unvoiced, and must not read as one the player granted.
         string id = h.StringId;
         ChatManager.AudiencePromises.Promise(id, () => _refusals[id] = CompanionRetentionPolicy.OnAudienceGranted());
         ChatManager.OpenForCompanionAudience(h, profile, CompanionAudienceReason.Grievance, false);
         ModLog.Info($"[HAPPINESS] {h.Name}'s grievance audience opened.");
      }

      private void OnDailyTick()
      {
         if (!ModSettings.CompanionContentment || _farewellDay.Count == 0) return;

         foreach (string id in _farewellDay.Keys.ToList())
         {
            if (Today < _farewellDay[id]) continue;

            Hero h = Hero.FindFirst(x => x != null && x.StringId == id);

            if (h is not {IsAlive: true, IsPlayerCompanion: true})
            {
               _farewellDay.Remove(id);

               continue;
            }

            // Dispatched on an errand after the farewell was scheduled: hold the departure until they are back,
            // so a companion away on your business is never removed mid-errand (the letter waits for them).
            bool away = CompanionMissionBehavior.Instance?.IsAway(id) == true;
            if (!CompanionLifecyclePolicy.MayDepartNow(away)) continue;

            _farewellDay.Remove(id);
            Hero companion = h;
            // One companion's departure throwing must not strand the rest of the queue.
            SafeRun.Step($"CompanionHappinessBehavior.Depart({companion.Name})", () => Depart(companion));
         }
      }

      /// <summary>
      ///   True when the companion holds no charge at all in your service. A governorship, a party of your clan
      ///   to lead, or an errand abroad are all employment: only a companion with none of them is kicking their
      ///   heels (the old rule counted only the errand, so a governor still soured week after week).
      /// </summary>
      private static bool IsIdle(Hero h)
      {
         bool onErrand = CompanionMissionBehavior.Instance?.IsAway(h.StringId) == true;
         bool isGovernor = h.GovernorOf != null;
         bool leadsAParty = h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h;

         return CompanionEngagementPolicy.IsIdle(new CompanionEngagementPolicy.CompanionChargeFacts {OnErrand = onErrand, IsGovernor = isGovernor, LeadsAParty = leadsAParty});
      }

      /// <summary>
      ///   A post handed to a companion is the plainest mark of trust the player can give. The reported case:
      ///   a companion was made governor and soured anyway, because the old model saw a governorship as nothing
      ///   at all (SuperGraham339).
      /// </summary>
      private void OnGovernorChanged(Town town, Hero oldGovernor, Hero newGovernor)
      {
         if (!ModSettings.CompanionContentment) return;
         if (newGovernor is not {IsAlive: true, IsPlayerCompanion: true}) return;

         CompanionHappinessService.ApplyDriver(newGovernor, HappinessDriver.Appointed);
      }

      /// <summary>
      ///   A battle is SHARED by those who stood in the line: the elation of the win, or the sting of the rout.
      ///   The defeat is deliberately light (one week's drift erases it), so a hard campaign never spirals into
      ///   a wave of departures.
      /// </summary>
      private void OnPlayerBattleEnd(MapEvent mapEvent)
      {
         if (!ModSettings.CompanionContentment || mapEvent is not {HasWinner: true}) return;

         HappinessDriver driver = mapEvent.WinningSide == mapEvent.PlayerSide
            ? HappinessDriver.SharedVictory
            : HappinessDriver.SharedDefeat;

         foreach (Hero h in Companions())
         {
            if (h.PartyBelongedTo != MobileParty.MainParty) continue; // only those who actually stood with you
            Hero companion = h;
            SafeRun.Step($"CompanionHappinessBehavior.OnPlayerBattleEnd({companion.Name})",
               () => CompanionHappinessService.ApplyDriver(companion, driver));
         }
      }

      private void OnWeeklyTick()
      {
         if (!ModSettings.CompanionContentment) return;

         // Serving a great house is an HONOUR: the happiness companions come to rest at rises with the standing
         // of the clan they serve, so an established player never has to nurse morale. The weekly drift home is
         // deliberately stronger than the ennui of idleness, so idleness ALONE can never carry anyone to the door.
         int baseline = CompanionBaseline.From(Mapping.StatureReader.Player.ClanTier);

         // The camp's mood, which the player already tends (food, wages, victories), colours those who march in it.
         HappinessDriver? mood = PartyMoraleMood.DriverFor(MobileParty.MainParty?.Morale ?? 50f);

         // How readily they get over things is the player's own taste (MCM). It never drops below the ennui of
         // idleness, so no setting can make a merely-unoccupied companion walk out.
         int driftStep = ContentmentPacingRules.DriftStepFor(ModSettings.CompanionPacing);

         foreach (Hero h in Companions())
         {
            Hero companion = h;
            // One companion's weekly pass throwing must not skip the rest of the roster.
            SafeRun.Step($"CompanionHappinessBehavior.OnWeeklyTick({companion.Name})", () =>
            {
               if (IsIdle(companion)) // a companion with a charge to keep takes no ennui, and no grievance either
               {
                  CompanionHappinessService.ApplyDriver(companion, HappinessDriver.Idle);
                  if (IsKeptFromLovedOne(companion)) CompanionHappinessService.ApplyDriver(companion, HappinessDriver.KeptFromLovedOne);
               }

               if (mood.HasValue && companion.PartyBelongedTo == MobileParty.MainParty)
                  CompanionHappinessService.ApplyDriver(companion, mood.Value);

               CompanionHappinessService.Drift(companion, driftStep, baseline);
               RunRetention(companion);
            });
         }
      }

      private void RefuseAudience(Hero h)
      {
         int refusals = _refusals.TryGetValue(h.StringId, out int r)
            ? r
            : 0;
         _refusals[h.StringId] = CompanionRetentionPolicy.RegisterRefusal(refusals);
         CompanionHappinessService.ApplyDriver(h, HappinessDriver.Mistreated); // turned away unheard — it sours them further
         ModLog.Info($"[HAPPINESS] {h.Name}'s audience refused ({_refusals[h.StringId]}/{CompanionRetentionPolicy.MaxAudienceRefusals}).");
      }

      /// <summary>
      ///   Raises the REAL "X asks for a word" popup for <paramref name="h" />, the same inquiry the retention
      ///   pass raises, so the accept path can be walked on demand (cr.audience ask). Internal rather than
      ///   private because the console is the only way to reach a state that otherwise needs an unhappy
      ///   companion and a settlement arrival to coincide, and that flow is exactly the one two players
      ///   reported failing (Lordfadooboo 11/09/2026, Zinced121212 17/09/2026).
      /// </summary>
      internal void RequestAudienceNow(Hero h) => RequestAudience(h);

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

      private void RunRetention(Hero h)
      {
         // A companion AWAY on an errand cannot organically quit mid-errand: they are abroad on your business.
         // Leave their grievance state untouched until they are back, or they would depart while still away.
         bool away = CompanionMissionBehavior.Instance?.IsAway(h.StringId) == true;
         if (!CompanionLifecyclePolicy.MayDepartNow(away)) return;

         bool wantsToLeave = CompanionHappiness.WantsToLeave(CompanionHappinessService.Get(h));
         if (!wantsToLeave)
         {
            _refusals.Remove(h.StringId); // recovered — the grievance is gone
            _farewellDay.Remove(h.StringId);

            return;
         }

         if (_farewellDay.ContainsKey(h.StringId)) return; // already leaving — the letter is on its way

         int refusals = _refusals.TryGetValue(h.StringId, out int r)
            ? r
            : 0;

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

      private void SendFarewell(Hero h)
      {
         int arriveDay = Today + DeliveryDays;
         _farewellDay[h.StringId] = arriveDay;
         _refusals.Remove(h.StringId);

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = Mapping.ActionGate.Allows(h, Logic.ActionKind.Letter, log: true) ? store?.Get(h.StringId) : null;
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

      #endregion
   }
}