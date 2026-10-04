// Code written by Gabriel Mailhot, 22/07/2026.
// Player feedback (Nexus): a popup offering a private word with a companion travelling in the party, refusal
// costing nothing ("please dont trigger a grudge at the first refusal"). Drives the six found-topics
// (CompanionAudienceTopicResolver / CalradiaRemembers.Logic's pure policies) behind ONE shared, GLOBAL
// cooldown across the whole roster (CompanionAudienceTopicPolicy.GlobalCooldownDays), never a per-companion
// timer, which would still produce a popup every few days from someone in an eight-companion party.
// 22/07/2026 follow-up: the offer moment moved from a weekly tick to the player's party ENTERING A
// SETTLEMENT (player's own request: "companions could come over to you while in settlement"). A companion
// does not pull the player aside mid-march or mid-pursuit; a town/village/castle is a natural pause for a
// private word, and it teaches the player a rhythm ("reaching a settlement" rather than a random timer). The
// global cooldown is still the only gate: it already keeps repeated visits inside the window from offering
// anything twice, so no second random roll was added on top of it.
// 20/08/2026: added the sixth found-topic, GRATITUDE (a content companion thanking the player for a specific
// Appreciation), spend-once on the appreciation's own Id, exactly like RumourReport spends on a WorldEvent Id.
// 20/08/2026 follow-up: added the seventh found-topic, SCHEME WARNING (a loyal companion warning the player of
// a secret plot against them, SchemeStore's own ledger). Granting it calls SchemeStore.TargetHeeds(MainHero)
// exactly ONCE, the FIRST caller of that method with the player as the target (every existing caller warns an
// NPC other than the player: the scheme_heed action and cr.scheme_warn both warn a named NPC, and the friendly
// stance Warning letter only NAMES the schemer without ever calling TargetHeeds), so there is no double-fire
// risk. Spend-once on the plotter/target StringId pair (CompanionAudienceTopicResolver.SchemeWarningKey),
// exactly like Gratitude spends on the appreciation's own Id.
// 20/08/2026 follow-up 2: added the eighth found-topic, AMBITION (a content, ambitious companion voicing ONE of
// FOUR concrete ambitions, each already bound to an existing action verb, so granting the audience resolves
// NOTHING mechanically by itself, unlike Gratitude/SchemeWarning: the deed is the PLAYER's, through the normal
// action pipeline, once granted in the conversation). Unlike the spend-once topics above, an ambition is not a
// one-shot fact to consume: the SAME underlying condition (landless+high-standing, no party role, etc.) can
// still be true after a refusal, so this is throttled by TIME instead, a per-companion "last asked" day
// (_ambitionAskedDays), stamped ONLY on a real grant, exactly like the spend-once keys are only marked on grant.
// 03/09/2026: hostility gate on the entry (player report: the popup fired while entering a HOSTILE village or an
// enemy castle, and answering inside that hostile flow closed the window and dumped the player into the
// location's encounter). EntryIsPeaceful feeds SettlementAudienceSafetyPolicy — a private word waits for a
// peaceful visit, and the fief-audience pass (which only runs from here) is gated with it.
// 08/09/2026: player report (Nexus): "unless I remember to routinely engage with my travel buddies, they never
// say anything on their own." SettlementEntered alone starves a player who travels for long stretches without
// visiting a town. Gabriel's ruling adds a ROAD trigger on the daily tick (OnDailyTick), gated by the SAME
// shared cooldown and topic-found requirement as the settlement path (both now flow through
// FindEligibleAudience, so there is one source of truth for "may a popup fire", not two diverging copies), plus
// its own RoadIsSafeForPrivateWord safety read (RoadAudienceSafetyPolicy) for hazards a scheduled daily check
// can land in that a deliberate settlement arrival never could: mid-battle, mid-siege, inside a menu, a
// prisoner's cell. Both the shared cooldown and the road's own daily chance are now player-tunable
// (CompanionAudiencePacingRules, read live via ModSettings.CompanionAudienceCadence), mirroring
// InterceptionPacingRules for lords approaching on the road; Occasionally keeps the shipped 14-day cooldown
// unchanged (CompanionAudiencePacingRules.DefaultCooldownDays), so an existing player's settlement feel does
// not shift, only gains a modest new road chance. Two things player feedback raised but Gabriel did NOT ratify
// stay out of scope here: a "no particular reason" small-talk topic, and a per-companion (rather than global)
// cooldown.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   On the player's party ENTERING A SETTLEMENT, or (since 08/09/2026) on a plain daily tick while simply
   ///   TRAVELLING, scans the roster for a found-topic audience, gated by ONE shared cooldown (scaled by the
   ///   player's own pacing dial, <see cref="CompanionAudiencePacingRules" />), and opens the popup for the
   ///   single strongest topic found anywhere in the party. Refusing costs nothing beyond the shared cooldown
   ///   already starting the moment the popup is raised, never a grudge, never a happiness hit.
   /// </summary>
   public sealed class CompanionAudienceTopicBehavior : CampaignBehaviorBase
   {
      private const int NeverAsked = -9999;
      private const string SaveKey = "CalradiaRemembers_AudienceTopicState_v1bytes";
      public static CompanionAudienceTopicBehavior Instance;

      private int _lastAskedDay = NeverAsked;

      // Composite keys ("companionId:gameDay:type") of NotableEvents already spent as a Callback, WorldEvent
      // ids already spent as a RumourReport, Appreciation ids already spent as a Gratitude, and plotter/target
      // StringId pairs already spent as a SchemeWarning, all marked ONLY once an audience is actually GRANTED
      // (never merely offered), so a refused popup leaves the topic free to resurface.
      private HashSet<string> _reportedRumourIds = new();
      private HashSet<string> _thankedKeys = new();
      private HashSet<string> _usedCallbackKeys = new();
      private HashSet<string> _warnedKeys = new();

      // companionId -> the game-day their Ambition topic was last GRANTED (never merely offered), throttling
      // how often the SAME companion may voice a fresh ambition (CompanionAmbitionPolicy.CooldownDays), distinct
      // from the spend-once sets above: an ambition is not a one-shot fact, so it is throttled by time instead.
      private Dictionary<string, int> _ambitionAskedDays = new();

      // The offer currently on the table (between RequestAudience and the player's answer), so GrantAudience
      // knows exactly what was promised without re-scanning (live state could have shifted in the interim).
      private string _pendingCompanionId;
      private string _pendingDetail;
      private string _pendingKey;
      private CompanionAudienceReason _pendingTopic = CompanionAudienceReason.None;

      public CompanionAudienceTopicBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      /// <summary>Runs the offer pass on demand (console test path), as if the player had just entered a settlement. Still respects the global cooldown.</summary>
      public void RunTick() => TryOfferAudience();

      /// <summary>
      ///   Diagnostic dump for cr.audience_topics: every companion currently in the party, each of the eight
      ///   topics scanned, with why it did or didn't qualify. Mutates nothing.
      /// </summary>
      public string DescribeRoster()
      {
         List<Hero> companions = Companions().ToList();

         if (companions.Count == 0) return "No companions are travelling with you right now.";

         var sb = new StringBuilder();
         sb.AppendLine($"Global cooldown: {(InCooldown() ? $"active ({CooldownDaysRemaining()} day(s) remaining)" : "clear")}.");

         foreach (Hero h in companions) sb.AppendLine(DescribeScan(h));

         return sb.ToString().TrimEnd();
      }

      /// <summary>Diagnostic dump for one companion: each of the eight topics, why it did or didn't qualify.</summary>
      public string DescribeScan(Hero companion)
      {
         if (companion == null) return "No companion found.";

         List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(companion, _reportedRumourIds, _usedCallbackKeys, _thankedKeys, _warnedKeys, _ambitionAskedDays);
         CompanionAudienceReason chosen = SelectFrom(scans);

         var sb = new StringBuilder();
         sb.AppendLine($"{companion.Name}:");
         foreach (CompanionAudienceTopicScan s in scans)
         {
            string mark = s.Topic == chosen && chosen != CompanionAudienceReason.None
               ? " ← would fire"
               : "";
            sb.AppendLine(s.Holds
               ? $"  {s.Topic}: QUALIFIES (strength {s.Strength}): {s.Detail}{mark}"
               : $"  {s.Topic}: no ({s.WhyNot})");
         }

         return sb.ToString().TrimEnd();
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               var snapshot = new SavedState {
                  LastAskedDay = _lastAskedDay,
                  ReportedRumourIds = new List<string>(_reportedRumourIds),
                  UsedCallbackKeys = new List<string>(_usedCallbackKeys),
                  ThankedKeys = new List<string>(_thankedKeys),
                  WarnedKeys = new List<string>(_warnedKeys),
                  AmbitionAskedDays = new Dictionary<string, int>(_ambitionAskedDays)
               };
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[AUDIENCE_TOPIC] serialize failed, persisting nothing this save. {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[AUDIENCE_TOPIC] save failed: {ex.Message}"); }
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

            try
            {
               SavedState state = JsonConvert.DeserializeObject<SavedState>(Encoding.UTF8.GetString(bytes));
               _lastAskedDay = state?.LastAskedDay ?? NeverAsked;
               _reportedRumourIds = new HashSet<string>(state?.ReportedRumourIds ?? new List<string>());
               _usedCallbackKeys = new HashSet<string>(state?.UsedCallbackKeys ?? new List<string>());
               _thankedKeys = new HashSet<string>(state?.ThankedKeys ?? new List<string>());
               // Additive, save-safe key (absent in older saves): WarnedKeys deserializes to null, and the
               // ?? below degrades it to an empty ledger, same as every field here on an old save.
               _warnedKeys = new HashSet<string>(state?.WarnedKeys ?? new List<string>());
               // Additive, save-safe key (absent in older saves): AmbitionAskedDays deserializes to null, and
               // the ?? below degrades it to an empty map, same as every field here on an old save.
               _ambitionAskedDays = new Dictionary<string, int>(state?.AmbitionAskedDays ?? new Dictionary<string, int>());
            }
            catch (Exception ex)
            {
               ModLog.Error($"[AUDIENCE_TOPIC] load failed, resetting state. {ex.Message}");
               _lastAskedDay = NeverAsked;
               _reportedRumourIds = new HashSet<string>();
               _usedCallbackKeys = new HashSet<string>();
               _thankedKeys = new HashSet<string>();
               _warnedKeys = new HashSet<string>();
               _ambitionAskedDays = new Dictionary<string, int>();
            }
         }
      }

      /// <summary>
      ///   Force-fires the strongest topic found anywhere in the party RIGHT NOW, bypassing the global
      ///   cooldown (console test path). Returns a description, or why nothing fired.
      /// </summary>
      public string TryForceFireBest()
      {
         (Hero Companion, CompanionAudienceTopicScan Scan)? found = FindBestAcrossRoster();

         if (found == null) return "No companion currently has a found-topic worth an audience.";

         RequestAudience(found.Value.Companion, found.Value.Scan);

         return $"{found.Value.Companion.Name} asks for a private word ({found.Value.Scan.Topic}, strength {found.Value.Scan.Strength}).";
      }

      /// <summary>
      ///   cr.audience gratitude test path: force-fires the GRATITUDE topic for one NAMED companion specifically
      ///   (unlike <see cref="TryForceFireBest" />, which fires the roster's single strongest topic of ANY
      ///   reason), bypassing the global cooldown. Opens the SAME real audience a granted popup would and spends
      ///   the key on open, exactly like a real grant, so re-running immediately reports nothing left to thank
      ///   them for. Returns why nothing fired when this companion currently has no qualifying kindness.
      /// </summary>
      public string TryForceFireGratitude(Hero companion)
      {
         if (companion == null) return "No companion found.";

         List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(companion, _reportedRumourIds, _usedCallbackKeys, _thankedKeys, _warnedKeys, _ambitionAskedDays);
         CompanionAudienceTopicScan scan = scans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Gratitude);

         if (scan is not {Holds: true})
            return $"{companion.Name} has no kindness currently worth a gratitude audience ({scan?.WhyNot ?? "no qualifying appreciation"}).";

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion.StringId);
         if (profile == null) return "No profile found for that companion.";

         if (!string.IsNullOrEmpty(scan.Key)) _thankedKeys.Add(scan.Key);

         bool landed = companion.GovernorOf != null || companion.PartyBelongedTo?.LeaderHero == companion;
         ChatManager.OpenForCompanionAudience(companion, profile, CompanionAudienceReason.Gratitude, landed, scan.Detail);
         ModLog.Info($"[AUDIENCE_TOPIC] {companion.Name}'s Gratitude audience opened (forced).");

         return $"{companion.Name} asks for a private word (Gratitude, strength {scan.Strength}).";
      }

      /// <summary>
      ///   cr.audience scheme_warning test path: force-fires the SCHEME WARNING topic for one NAMED companion
      ///   specifically (unlike <see cref="TryForceFireBest" />, which fires the roster's single strongest topic
      ///   of ANY reason), bypassing the global cooldown. Opens the SAME real audience a granted popup would,
      ///   spends the key AND heeds the warning (<see cref="SchemeStore.TargetHeeds" />) on open, exactly like a
      ///   real grant, so re-running immediately reports nothing left to warn of. Returns why nothing fired when
      ///   this companion currently has no qualifying scheme to warn of.
      /// </summary>
      public string TryForceFireSchemeWarning(Hero companion)
      {
         if (companion == null) return "No companion found.";

         List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(companion, _reportedRumourIds, _usedCallbackKeys, _thankedKeys, _warnedKeys, _ambitionAskedDays);
         CompanionAudienceTopicScan scan = scans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.SchemeWarning);

         if (scan is not {Holds: true})
            return $"{companion.Name} has no plot currently worth a scheme-warning audience ({scan?.WhyNot ?? "no qualifying scheme"}).";

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion.StringId);
         if (profile == null) return "No profile found for that companion.";

         if (!string.IsNullOrEmpty(scan.Key)) _warnedKeys.Add(scan.Key);
         SchemeStore.Instance?.TargetHeeds(Hero.MainHero);

         bool landed = companion.GovernorOf != null || companion.PartyBelongedTo?.LeaderHero == companion;
         ChatManager.OpenForCompanionAudience(companion, profile, CompanionAudienceReason.SchemeWarning, landed, scan.Detail);
         ModLog.Info($"[AUDIENCE_TOPIC] {companion.Name}'s SchemeWarning audience opened (forced).");

         return $"{companion.Name} asks for a private word (SchemeWarning, strength {scan.Strength}).";
      }

      /// <summary>
      ///   cr.audience ambition test path: force-fires the AMBITION topic for one NAMED companion specifically
      ///   (unlike <see cref="TryForceFireBest" />, which fires the roster's single strongest topic of ANY
      ///   reason), bypassing the global cooldown (but NOT the per-companion ambition cooldown, which is the
      ///   thing under test). Opens the SAME real audience a granted popup would and stamps the SAME per-
      ///   companion "last asked" day a real grant would, exactly like a real grant, so re-running immediately
      ///   reports the cooldown withholding it. Returns why nothing fired when this companion currently has no
      ///   ambition of their own worth voicing.
      /// </summary>
      public string TryForceFireAmbition(Hero companion)
      {
         if (companion == null) return "No companion found.";

         List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(companion, _reportedRumourIds, _usedCallbackKeys, _thankedKeys, _warnedKeys, _ambitionAskedDays);
         CompanionAudienceTopicScan scan = scans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Ambition);

         if (scan is not {Holds: true})
            return $"{companion.Name} has no ambition of their own currently worth an audience ({scan?.WhyNot ?? "no qualifying ambition"}).";

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion.StringId);
         if (profile == null) return "No profile found for that companion.";

         _ambitionAskedDays[companion.StringId] = Today;

         bool landed = companion.GovernorOf != null || companion.PartyBelongedTo?.LeaderHero == companion;
         ChatManager.OpenForCompanionAudience(companion, profile, CompanionAudienceReason.Ambition, landed, scan.Detail);
         ModLog.Info($"[AUDIENCE_TOPIC] {companion.Name}'s Ambition audience opened (forced).");

         return $"{companion.Name} asks for a private word (Ambition, strength {scan.Strength}): {scan.Detail}.";
      }

      #region private

      private static IEnumerable<Hero> Companions()
         => Hero.AllAliveHeroes.Where(h =>
            Mapping.ActionGate.Allows(h, Logic.ActionKind.CompanionAudience) && h.IsPlayerCompanion
            && h.PartyBelongedTo == MobileParty.MainParty);

      private int CooldownDaysRemaining()
         => _lastAskedDay == NeverAsked
            ? 0
            : Math.Max(0, CompanionAudiencePacingRules.CooldownDays(ModSettings.CompanionAudienceCadence) - (Today - _lastAskedDay));

      /// <summary>The strongest (companion, topic) anywhere in the party, or null when nothing qualifies.</summary>
      private (Hero Companion, CompanionAudienceTopicScan Scan)? FindBestAcrossRoster()
      {
         (Hero Companion, CompanionAudienceTopicScan Scan)? best = null;

         // Deterministic roster order (StringId) so that, on an exact tie in strength, the SAME companion
         // always wins regardless of iteration order the engine happens to hand back Hero.AllAliveHeroes in.
         foreach (Hero h in Companions().OrderBy(h => h.StringId, StringComparer.Ordinal))
         {
            List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(h, _reportedRumourIds, _usedCallbackKeys, _thankedKeys, _warnedKeys, _ambitionAskedDays);
            CompanionAudienceReason topic = SelectFrom(scans);

            if (topic == CompanionAudienceReason.None) continue;

            CompanionAudienceTopicScan scan = scans.First(s => s.Topic == topic);

            if (best == null || scan.Strength > best.Value.Scan.Strength) best = (h, scan);
         }

         return best;
      }

      private void GrantAudience()
      {
         Hero companion = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h.StringId == _pendingCompanionId);
         CompanionAudienceReason topic = _pendingTopic;
         string detail = _pendingDetail;
         string key = _pendingKey;
         ClearPending();

         if (companion == null) return;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion.StringId);
         if (profile == null) return;

         // The one-shot topics are spent when the audience is really on screen, not at the yes: a refused popup
         // must leave them free to resurface (see the class header), and so must one accepted that never opened
         // (EnjoyingHydra, 03/10/2026: accepted, nothing appeared, and the topic was gone for good).
         string companionId = companion.StringId;
         ChatManager.AudiencePromises.Promise(companionId, () =>
         {
            if (topic == CompanionAudienceReason.RumourReport && !string.IsNullOrEmpty(key)) _reportedRumourIds.Add(key);
            if (topic == CompanionAudienceReason.Callback && !string.IsNullOrEmpty(key)) _usedCallbackKeys.Add(key);
            if (topic == CompanionAudienceReason.Gratitude && !string.IsNullOrEmpty(key)) _thankedKeys.Add(key);

            // SchemeWarning's audience IS the heed: this is the only place TargetHeeds(MainHero) is ever called
            // (see the class header), so it must fire exactly once, on an audience really heard.
            if (topic == CompanionAudienceReason.SchemeWarning)
            {
               if (!string.IsNullOrEmpty(key)) _warnedKeys.Add(key);
               SchemeStore.Instance?.TargetHeeds(Hero.MainHero);
            }

            // Ambition is not spend-once: the per-companion cooldown clock starts on an audience really heard.
            if (topic == CompanionAudienceReason.Ambition) _ambitionAskedDays[companionId] = Today;
         });

         // Defer the chat open to the next main-thread frame. Opening the focus-grabbing, time-pausing chat
         // overlay synchronously from inside this inquiry consequence, while a settlement menu is the active
         // state being restored under the dismissing inquiry, left the inquiry undismissed and the engine
         // re-presented it every frame (the "million popups" cascade). One frame later the inquiry is gone and
         // the menu has settled, so the overlay opens cleanly. See NpcMemoryServiceSubmodule.OnApplicationTick.
         MainThreadDispatcher.Enqueue(() =>
         {
            // "granted", not "opened": this line used to say opened and was written whatever happened next,
            // so a log could assert an audience had opened when no window ever appeared. Whether it opened is
            // now ChatManager's own to report (Lordfadooboo, 2.5.8).
            ModLog.Info($"[AUDIENCE_TOPIC] {companion.Name}'s {topic} audience granted; opening.");
            ChatManager.OpenForCompanionAudience(companion, profile, topic, false, detail);
         });
      }

      private bool InCooldown() => _lastAskedDay != NeverAsked && Today - _lastAskedDay < CompanionAudiencePacingRules.CooldownDays(ModSettings.CompanionAudienceCadence);

      private void ClearPending()
      {
         _pendingCompanionId = null;
         _pendingTopic = CompanionAudienceReason.None;
         _pendingDetail = null;
         _pendingKey = null;
      }

      /// <summary>
      ///   The player's own party arriving somewhere is the offer moment (player feedback): a companion does
      ///   not pull the player aside mid-march, but a settlement is a natural pause for a private word.
      ///   SettlementEntered fires for EVERY party entering EVERY settlement, so this filters to the player's
      ///   own party first (the firehose filter used elsewhere in the mod, e.g. TownAmbushBehavior), then to
      ///   a PEACEFUL entry (<see cref="EntryIsPeaceful" /> — never mid-siege, mid-raid, or in enemy territory).
      ///   COORDINATION with FiefAudienceBehavior (fief lords/ladies/notables with no field party asking for a
      ///   word): at most ONE audience popup may appear per entry, so the fief pass only runs when a companion
      ///   audience was NOT itself raised this entry (see FiefAudienceBehavior's own class header).
      /// </summary>
      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         if (party != MobileParty.MainParty || settlement == null) return;

         // Hostility gate (player report, 2026-09: the popup fired while entering a HOSTILE village or an
         // enemy castle, and answering inside that hostile entry flow closed the window and dumped the player
         // into the location's encounter). A private word is a social pause — it waits for a peaceful visit.
         // Gates the fief-audience pass too: it only ever runs from here.
         if (!EntryIsPeaceful(settlement)) return;

         // Timing guard (player report, 2026-09-05): entering a settlement can coincide with a VANILLA conversation
         // (some settlements, and total-conversion mods like TOR, auto-open one on entry). Raising our inquiry then
         // simply queues it behind the vanilla dialogue, so it surfaces seconds later and reads as "nothing happened
         // when I responded". Skip the offer this entry while a conversation or mission is active; the global
         // cooldown is left untouched, so a companion (or a fief NPC) just asks on the next clean entry.
         if (Hero.OneToOneConversationHero != null || Mission.Current != null)
         {
            ModLog.Info($"[AUDIENCE_TOPIC] entry into {settlement.Name} coincided with a conversation/mission, deferring to a clean entry.");

            return;
         }

         if (TryOfferAudience()) return; // one popup per entry: a companion audience already claimed it

         FiefAudienceBehavior.Instance?.TryOfferOnSettlementEntry();
      }

      /// <summary>
      ///   Gathers the engine facts for <see cref="SettlementAudienceSafetyPolicy.IsSafeForPrivateWord" />:
      ///   a siege in progress, the village being raided, or the owner's faction at war with the player's all
      ///   make this entry a hostile moment where no audience may pop. Fails OPEN on a read error: the gate
      ///   guards a rare hostile corner, and a broken read must not silently kill the feature everywhere.
      /// </summary>
      private static bool EntryIsPeaceful(Settlement settlement)
      {
         try
         {
            bool underSiege = settlement.IsUnderSiege;
            bool raidInProgress = settlement.IsVillage && settlement.Village?.VillageState == Village.VillageStates.BeingRaided;
            bool atWar = settlement.MapFaction != null
                         && Hero.MainHero?.MapFaction != null
                         && settlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction);

            bool peaceful = SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = underSiege, RaidInProgress = raidInProgress, AtWarWithPlayer = atWar});

            if (!peaceful)
               ModLog.Info($"[AUDIENCE_TOPIC] entry into {settlement.Name} is not peaceful " +
                           $"(siege={underSiege}, raid={raidInProgress}, atWar={atWar}) — no audience this entry.");

            return peaceful;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AUDIENCE_TOPIC] hostility check failed, allowing the offer: {ex.GetType().Name}: {ex.Message}");

            return true;
         }
      }

      /// <summary>
      ///   08/09/2026: the daily ROAD trigger (player report: companions never speak up unless the player
      ///   enters a settlement). Off entirely at <see cref="CompanionAudiencePacing.Never" />; otherwise rolls
      ///   the pacing dial's own daily chance (<see cref="CompanionAudiencePacingRules.DailyChancePercent" />)
      ///   and, only on a hit, checks <see cref="RoadIsSafeForPrivateWord" /> before falling into the SAME
      ///   <see cref="FindEligibleAudience" /> gate the settlement trigger uses (contentment, the shared
      ///   cooldown, a real topic found). One source of truth for "may a popup fire" either way.
      /// </summary>
      private void OnDailyTick()
      {
         try
         {
            if (CompanionAudiencePacingRules.IsOff(ModSettings.CompanionAudienceCadence)) return; // the road is the player's own
            if (MBRandom.RandomInt(100) >= CompanionAudiencePacingRules.DailyChancePercent(ModSettings.CompanionAudienceCadence)) return;
            if (!RoadIsSafeForPrivateWord()) return;

            TryOfferAudience();
         }
         catch (Exception ex) { ModLog.Error($"[AUDIENCE_TOPIC] road daily tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Gathers the engine facts for <see cref="RoadAudienceSafetyPolicy.IsSafeForPrivateWord" />, the road
      ///   trigger's own hazard read (deliberately separate from <see cref="EntryIsPeaceful" />, which answers a
      ///   different question: whether a SETTLEMENT being entered is hostile). A scheduled daily check can land
      ///   in far more states than a deliberate arrival ever could, so this mirrors InterceptionBehavior's own
      ///   "PlayerIsFreeOnMap" reasoning (the analogous "is it safe to raise something with the player right now"
      ///   read for a lord's road pursuit). Fails CLOSED on a read error, the opposite of EntryIsPeaceful: this
      ///   check runs unattended, every single day, so a broken read costs at most one quiet day, whereas failing
      ///   open here risks popping an inquiry mid-battle or mid-conversation on the one day the read breaks.
      /// </summary>
      private static bool RoadIsSafeForPrivateWord()
      {
         try
         {
            MobileParty main = MobileParty.MainParty;

            return RoadAudienceSafetyPolicy.IsSafeForPrivateWord(new RoadAudienceFacts {
               InMission = Mission.Current != null,
               InConversation = Hero.OneToOneConversationHero != null,
               PlayerIsPrisoner = Hero.MainHero?.IsPrisoner ?? true,
               InSettlement = Hero.MainHero?.CurrentSettlement != null,
               InMapEvent = main?.MapEvent != null,
               InSiegeEvent = main?.SiegeEvent != null,
               InEncounter = PlayerEncounter.Current != null,
               InMenu = Campaign.Current?.CurrentMenuContext != null,
               ChatOpen = ChatManager.IsOpen,
               ChatStaging = ChatSceneStager.IsStaging || CouncilConversationStager.IsStaging,
               // Open water is not the road. Crash report, 2026-09-10: a companion asked for a word at sea, the
               // popup and the chat window came up together, and ending the conversation crashed the game.
               AtSea = main?.IsCurrentlyAtSea ?? false
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AUDIENCE_TOPIC] road safety check failed, skipping the road offer this tick: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   The shared offer logic behind the real settlement trigger (OnSettlementEntered), the real road
      ///   trigger (OnDailyTick), and the console test path (RunTick): gated by contentment + the shared
      ///   cooldown only, no second random roll on top for the settlement path (the cooldown already keeps
      ///   repeated visits inside its window from offering anything twice); the road path adds its own daily
      ///   dice roll and safety read BEFORE ever reaching here. Returns true only when a popup was actually
      ///   raised, so OnSettlementEntered knows whether the fief-audience pass may still run this same entry.
      /// </summary>
      private bool TryOfferAudience()
      {
         (Hero Companion, CompanionAudienceTopicScan Scan)? found = FindEligibleAudience();
         if (found == null) return false; // silence is the default: nothing worth saying was found anywhere in the roster

         RequestAudience(found.Value.Companion, found.Value.Scan);

         return true;
      }

      /// <summary>
      ///   cr.testall seam: the SAME contentment + shared-cooldown + topic-found decision <see cref="TryOfferAudience" />
      ///   makes, without ever opening the popup or starting the cooldown clock, so a test can prove the gates
      ///   without a live UI side effect.
      /// </summary>
      internal bool DebugWouldOfferAudience() => FindEligibleAudience() != null;

      /// <summary>
      ///   cr.testall seam: replays the road trigger's own safety gate PLUS the shared offer decision, exactly
      ///   what <see cref="OnDailyTick" /> checks after its dice roll lands, without ever rolling that die and
      ///   without opening the popup (see <see cref="DebugWouldOfferAudience" />). Proves the road trigger's
      ///   wiring reaches the real gates without depending on RNG or a live inquiry.
      /// </summary>
      internal bool DebugRoadTriggerWouldFire() => RoadIsSafeForPrivateWord() && DebugWouldOfferAudience();

      /// <summary>The contentment + shared-cooldown + topic-found gate common to every offer path (settlement, road, console test).</summary>
      private (Hero Companion, CompanionAudienceTopicScan Scan)? FindEligibleAudience()
      {
         // "Never" must mean never, on EVERY path. The dial's own documentation promises silence "on the road or
         // entering a settlement", but the check lived only in the road tick, so a player who switched the
         // feature off kept being asked for a word in towns: the one place the setting was meant to reach.
         // Guarded here, in the single decision all three callers (settlement, road, console) share.
         if (CompanionAudiencePacingRules.IsOff(ModSettings.CompanionAudienceCadence)) return null;
         if (!ModSettings.CompanionContentment) return null;
         if (InCooldown()) return null;

         return FindBestAcrossRoster();
      }

      private void RequestAudience(Hero h, CompanionAudienceTopicScan scan)
      {
         // The GLOBAL clock starts the moment the popup is raised, whether or not the player answers it, the
         // same "never silently, but never spammed either" rule the existing audiences already follow.
         _lastAskedDay = Today;
         _pendingCompanionId = h.StringId;
         _pendingTopic = scan.Topic;
         _pendingDetail = scan.Detail;
         _pendingKey = scan.Key;

         var title = new TextObject($"{h.Name} asks for a private word");
         var body = new TextObject($"{h.Name} seems to have something on their mind, and asks for a quiet moment alone with you.");

         // Neutral wording on purpose (mirrors CompanionAffectionBehavior/CompanionHappinessBehavior): the
         // specific thing found belongs in the conversation, not the popup.
         InformationManager.ShowInquiry(new InquiryData(
            title.ToString(), body.ToString(),
            true, true,
            "Speak with them", "Not now",
            GrantAudience,
            () => ModLog.Info($"[AUDIENCE_TOPIC] {h.Name}'s private word was declined, no penalty, free refusal.")), true);
      }

      /// <summary>Mirrors CompanionAudienceTopicPolicy.SelectTopic over a resolver scan.</summary>
      private static CompanionAudienceReason SelectFrom(List<CompanionAudienceTopicScan> scans)
      {
         var candidates = scans
            .Select(s => new CompanionAudienceTopicCandidate(s.Topic, s.Holds, s.Strength))
            .ToList();

         return CompanionAudienceTopicPolicy.SelectTopic(candidates);
      }

      /// <summary>Plain persisted shape, deliberately NOT the live fields, so JSON shape changes stay isolated here.</summary>
      private sealed class SavedState
      {
         public int LastAskedDay { get; set; }
         public List<string> ReportedRumourIds { get; set; }
         public List<string> ThankedKeys { get; set; }
         public List<string> UsedCallbackKeys { get; set; }
         public List<string> WarnedKeys { get; set; }
         public Dictionary<string, int> AmbitionAskedDays { get; set; }
      }

      #endregion
   }
}
