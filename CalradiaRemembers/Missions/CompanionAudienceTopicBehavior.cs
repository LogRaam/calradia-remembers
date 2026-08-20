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

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   On the player's party ENTERING A SETTLEMENT, scans the roster for a found-topic audience, gated by
   ///   ONE global cooldown, and opens the popup for the single strongest topic found anywhere in the party.
   ///   Refusing costs nothing beyond the shared cooldown already starting the moment the popup is raised,
   ///   never a grudge, never a happiness hit.
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
         => CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);

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
            h != null && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner
            && h.PartyBelongedTo == MobileParty.MainParty);

      private int CooldownDaysRemaining()
         => _lastAskedDay == NeverAsked
            ? 0
            : Math.Max(0, CompanionAudienceTopicPolicy.GlobalCooldownDays - (Today - _lastAskedDay));

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

         // Only NOW, with the audience truly opening, do we spend the one-shot topics, a refused popup must
         // leave them free to resurface (see the class header).
         if (topic == CompanionAudienceReason.RumourReport && !string.IsNullOrEmpty(key)) _reportedRumourIds.Add(key);
         if (topic == CompanionAudienceReason.Callback && !string.IsNullOrEmpty(key)) _usedCallbackKeys.Add(key);
         if (topic == CompanionAudienceReason.Gratitude && !string.IsNullOrEmpty(key)) _thankedKeys.Add(key);

         // SchemeWarning's audience IS the heed: this is the only place TargetHeeds(MainHero) is ever called
         // (see the class header), so it must fire exactly once, here, on a real grant, never on a mere offer.
         if (topic == CompanionAudienceReason.SchemeWarning)
         {
            if (!string.IsNullOrEmpty(key)) _warnedKeys.Add(key);
            SchemeStore.Instance?.TargetHeeds(Hero.MainHero);
         }

         // Ambition is not spend-once: the per-companion cooldown clock starts here, on a real grant, exactly
         // like the spend-once keys above are only ever marked on a real grant, never a mere offer.
         if (topic == CompanionAudienceReason.Ambition) _ambitionAskedDays[companion.StringId] = Today;

         ChatManager.OpenForCompanionAudience(companion, profile, topic, false, detail);
         ModLog.Info($"[AUDIENCE_TOPIC] {companion.Name}'s {topic} audience opened.");
      }

      private bool InCooldown() => _lastAskedDay != NeverAsked && Today - _lastAskedDay < CompanionAudienceTopicPolicy.GlobalCooldownDays;

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
      ///   own party first (the firehose filter used elsewhere in the mod, e.g. TownAmbushBehavior).
      /// </summary>
      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         if (party != MobileParty.MainParty || settlement == null) return;

         TryOfferAudience();
      }

      /// <summary>
      ///   The shared offer logic behind both the real trigger (OnSettlementEntered) and the console test
      ///   path (RunTick): gated by contentment + the GLOBAL cooldown only, no second random roll on top,
      ///   the cooldown already keeps repeated settlement visits inside its window from offering anything twice.
      /// </summary>
      private void TryOfferAudience()
      {
         if (!ModSettings.CompanionContentment) return;
         if (InCooldown()) return;

         (Hero Companion, CompanionAudienceTopicScan Scan)? found = FindBestAcrossRoster();
         if (found == null) return; // silence is the default: nothing worth saying was found anywhere in the roster

         RequestAudience(found.Value.Companion, found.Value.Scan);
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
