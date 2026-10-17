// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>
   ///   Owns the world-event log. Feeders call <see cref="Record" /> from campaign-event listeners
   ///   (zero LLM); consumers query it. Two lifetimes live here on purpose: third-party events are
   ///   rumor fodder and are pruned aggressively, while <see cref="WorldEvent.InvolvesPlayer" /> events
   ///   are the chronicle and are kept long-term. The lifetime rules (<see cref="WorldEventPruner" />)
   ///   and the awareness model (<see cref="RumorAwareness" />) are pure and unit-tested in
   ///   CalradiaRemembers.Logic; this class only adds the engine glue (positions, factions, save).
   ///   Persisted as a UTF-8 byte[] (no native string-size ceiling, like the profile store) with the
   ///   same "never brick a save" degradation.
   /// </summary>
   public sealed class WorldEventStore : CampaignBehaviorBase
   {
      // Lifetime bounds and the awareness/confidence model live in CalradiaRemembers.Logic
      // (WorldEventPruner, RumorAwareness) where they are pure and unit-tested. The only piece that
      // must stay here is the geo bonus's DISTANCE scaling, which reads the map; its ceiling
      // (RumorAwareness.GeoBonusMax) and the concerned-party lift live in Logic with the rest.

      private const string SaveKey = "CalradiaRemembers_WorldEvents_v1bytes";

      // The world's long-term memory (Gabriel, 17/10/2026): "apres 42 jours, ca devrait basculer vers une memoire long
      // terme." Additive: an older save has no such key and opens with an empty history.
      private const string HistorySaveKey = "CalradiaRemembers_WorldHistory_v1bytes";
      public static WorldEventStore Instance;

      private List<WorldEvent> _events = new();

      // Third-party events that left the news but that the world keeps (LongTermHistoryPolicy). Never scored by
      // RumorAwareness and never offered as news: they are known by tie (actor, kin, clan, place, realm), not by distance.
      private List<WorldEvent> _history = new();

      public WorldEventStore()
      {
         Instance = this;
      }

      public int Count => _events.Count;

      /// <summary>How many events the long-term tier holds.</summary>
      public int HistoryCount => _history.Count;

      /// <summary>The stored event with this id, or null if it never existed or has been pruned.</summary>
      public WorldEvent GetById(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : _events.FirstOrDefault(e => e.Id == id);

      /// <summary>
      ///   Removes every event carrying this id — used to retract or replace an externally-fed standing fact
      ///   that is no longer true (a homestead that moved, an NPC renamed). Returns true if anything was removed.
      /// </summary>
      public bool Remove(string id)
      {
         if (string.IsNullOrEmpty(id)) return false;

         int removed = _events.RemoveAll(e => e.Id == id) + _history.RemoveAll(e => e.Id == id);
         if (removed > 0) ModLog.Info($"[WORLD] removed {removed} event(s) with id '{id}'.");

         return removed > 0;
      }

      // ── Queries ───────────────────────────────────────────────────────────

      /// <summary>Every stored event, unfiltered: for the player's console tools (cr.world_events), never for a prompt.</summary>
      public IReadOnlyList<WorldEvent> All() => _events.ToList();

      /// <summary>
      ///   The events <paramref name="observer" /> would plausibly have heard of, best-known first,
      ///   scored by recency, reach (magnitude), faction relevance, and geographic proximity.
      /// </summary>
      public IReadOnlyList<WorldEvent> GetKnownEvents(Hero observer, int today, int max)
         => observer == null
            ? Array.Empty<WorldEvent>()
            : GetKnown(ObserverPosition(observer), observer.MapFaction?.StringId, today, max, observer.StringId);

      /// <summary>The events a settlement's locals would have heard of (commoner intel).</summary>
      public IReadOnlyList<WorldEvent> GetKnownEventsAt(Settlement settlement, int today, int max)
         => settlement == null
            ? Array.Empty<WorldEvent>()
            : GetKnown(settlement.GetPosition2D, settlement.MapFaction?.StringId, today, max);

      /// <summary>Known events tagged with how surely they reached a hero (distortion with distance).</summary>
      public IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> GetKnownEventsRated(Hero observer, int today, int max)
         => observer == null
            ? Array.Empty<(WorldEvent, RumorConfidence)>()
            : GetKnownRated(ObserverPosition(observer), observer.MapFaction?.StringId, today, max, observer.StringId);

      /// <summary>Known events tagged with confidence for a settlement's locals.</summary>
      public IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> GetKnownEventsRatedAt(Settlement settlement, int today, int max)
         => settlement == null
            ? Array.Empty<(WorldEvent, RumorConfidence)>()
            : GetKnownRated(settlement.GetPosition2D, settlement.MapFaction?.StringId, today, max);

      /// <summary>The player's deeds in chronological order — the source for the chronicle.</summary>
      public IReadOnlyList<WorldEvent> GetPlayerChronicle()
         => _events.Where(e => e.InvolvesPlayer).OrderBy(e => e.Day).ToList();

      /// <summary>
      ///   The most recently recorded event of <paramref name="type" /> whose subject matches
      ///   <paramref name="match" />, or null when none exists. Lets a feeder ask the PERSISTED store "did we
      ///   already record this same standing condition, and how long ago?" so its dedup survives save/reload for
      ///   free (the store persists), without a separate serialized cooldown ledger. Used by the weekly
      ///   world-conditions feeder to gate WarLooming / Hardship re-records.
      /// </summary>
      public WorldEvent MostRecentMatching(WorldEventType type, Func<WorldEvent, bool> match)
         => match == null
            ? null
            : _events.Where(e => e.Type == type && match(e)).OrderByDescending(e => e.Day).FirstOrDefault();

      /// <summary>
      ///   Recent events worth a roving agent's report — the freshest happenings at/above a magnitude,
      ///   regardless of who would passively know them. A companion sent to GATHER news digs past the
      ///   player's own horizon, so this is NOT awareness-gated (unlike <see cref="GetKnownEvents" />).
      ///   Newest first, then biggest; bounded to <paramref name="max" /> and the recency window.
      /// </summary>
      public IReadOnlyList<WorldEvent> GetRecentSignificant(int today, int max, int minMagnitude)
      {
         if (_events.Count == 0 || max <= 0) return Array.Empty<WorldEvent>();

         return _events
                .Where(e => e.Magnitude >= minMagnitude && !string.IsNullOrWhiteSpace(e.Summary) && today - e.Day <= RumorAwareness.RecencyWindowDays)
                .Where(e => !e.KnownOnlyToThoseInvolved) // a secret is not dug up by a companion sent for news
                .OrderByDescending(e => e.Day)
                .ThenByDescending(e => e.Magnitude)
                .Take(max)
                .ToList();
      }

      /// <summary>True when <paramref name="observer" /> would by now have heard of <paramref name="e" />.</summary>
      public bool IsAwareOf(Hero observer, WorldEvent e, int today)
         => observer != null && e != null && Explain(observer, e, today).Knows;

      /// <summary>
      ///   The awareness verdict for one hero and one event, with its terms: the same arithmetic the prompt's
      ///   queries gate on, exposed for diagnostics (cr.heard) so a modder can see WHY a fed fact is or is not
      ///   known without talking to the character.
      /// </summary>
      public RumorAwarenessBreakdown Explain(Hero observer, WorldEvent e, int today)
         => observer == null || e == null
            ? null
            : Explain(e, today, ObserverPosition(observer), observer.MapFaction?.StringId, observer.StringId);

      /// <summary>
      ///   Every stored event with <paramref name="observer" />'s verdict on it, best score first. Diagnostic only:
      ///   the prompt never reads the unheard ones.
      /// </summary>
      public IReadOnlyList<(WorldEvent Event, RumorAwarenessBreakdown Verdict)> ExplainAll(Hero observer, int today)
      {
         if (observer == null || _events.Count == 0) return Array.Empty<(WorldEvent, RumorAwarenessBreakdown)>();

         Vec2? pos = ObserverPosition(observer);
         string factionId = observer.MapFaction?.StringId;

         return _events
                .Select(e => (e, v: Explain(e, today, pos, factionId, observer.StringId)))
                .OrderByDescending(t => t.v.Score)
                .ToList();
      }

      // ── The long-term tier ────────────────────────────────────────────────

      /// <summary>Every long-term event, oldest first, unfiltered: for console tools (cr.history), never for a prompt as is.</summary>
      public IReadOnlyList<WorldEvent> AllHistory() => _history.OrderBy(e => e.Day).ToList();

      /// <summary>The long-term event with this id, or null.</summary>
      public WorldEvent GetHistoryById(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : _history.FirstOrDefault(e => e.Id == id);

      /// <summary>True when an event in either tier matches (a feeder's dedup that must also see what already aged out).</summary>
      public bool AnyMatching(Func<WorldEvent, bool> match)
         => match != null && (_events.Any(match) || _history.Any(match));

      /// <summary>
      ///   The long-term events a query asks for, oldest first: involving a hero, shared by a pair, at a place, of a realm
      ///   (each criterion given narrows the answer), and only those <see cref="HistoryQuery.KnownTo" /> would know when
      ///   it is set. The read door for the regard policy and the conversation (design 7.7a), built ahead of them.
      /// </summary>
      public IReadOnlyList<WorldEvent> GetHistory(HistoryQuery query)
      {
         if (query == null || _history.Count == 0) return Array.Empty<WorldEvent>();

         IEnumerable<WorldEvent> found = _history;
         if (query.Involving != null) found = found.Where(e => Names(e, query.Involving.StringId));
         if (query.AlsoInvolving != null) found = found.Where(e => Names(e, query.AlsoInvolving.StringId));
         if (query.At != null) found = found.Where(e => e.PlaceId == query.At.StringId);
         if (query.Realm != null) found = found.Where(e => e.FactionIds != null && e.FactionIds.Contains(query.Realm.StringId));
         if (query.KnownTo != null) found = found.Where(e => WouldKnow(query.KnownTo, e));

         IEnumerable<WorldEvent> ordered = found.OrderBy(e => e.Day);

         return (query.Max > 0 ? ordered.Take(query.Max) : ordered).ToList();
      }

      /// <summary>True when <paramref name="observer" /> would know this long-term event (LongTermHistoryPolicy.WouldKnow).</summary>
      public static bool WouldKnow(Hero observer, WorldEvent e)
      {
         if (observer == null || e == null) return false;

         return LongTermHistoryPolicy.WouldKnow(KnowledgeFacts(observer, e));
      }

      /// <summary>
      ///   Writes an event straight into the long-term tier: a fact that is history but never news (a lesser lord's
      ///   capture, Lordfadooboo 03/10/2026; later, the generated shared past). Assigns an id and a day when absent.
      /// </summary>
      public void RecordHistory(WorldEvent e)
      {
         if (e == null || string.IsNullOrWhiteSpace(e.Summary)) return;
         if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
         if (e.Day <= 0) e.Day = (int) CampaignTime.Now.ToDays;
         if (_history.Any(h => h.Id == e.Id)) return;

         _history.Add(e);
         LongTermHistoryPolicy.Evict(_history);
         ModLog.Info($"[WORLD] history recorded {e.Type} (day {e.Day}, mag {e.Magnitude}): {e.Summary}");
      }

      /// <summary>How the long-term memory sorts a world event (the 7.7a bar is decided on this kind).</summary>
      public static HistoryKind HistoryKindOf(WorldEvent e)
      {
         if (e == null) return HistoryKind.None;

         switch (e.Type)
         {
            case WorldEventType.Battle: return HistoryKind.Battle;
            case WorldEventType.SettlementCaptured: return HistoryKind.FiefTaken;
            case WorldEventType.Execution: return HistoryKind.Execution;
            case WorldEventType.Marriage: return HistoryKind.Marriage;
            case WorldEventType.WarDeclared: return HistoryKind.War;
            case WorldEventType.PeaceDeclared: return HistoryKind.Peace;
            case WorldEventType.Rivalry:
               // SchemeStore records a scheme's phases on this type; only one that came to light, or succeeded in the
               // open, is a discovered scheme. Ambient rivalry gossip and a whispered plot stay news.
               return e.Status == SchemeStore.DiscoveredStatus || e.Status == SchemeStore.SucceededStatus
                  ? HistoryKind.SchemeExposed
                  : HistoryKind.None;
            case WorldEventType.Rebellion: return HistoryKind.Rebellion;
            case WorldEventType.Divorce: return HistoryKind.Divorce;
            case WorldEventType.Other:
               switch (e.Status)
               {
                  case LongTermHistoryPolicy.CaptureStatus: return HistoryKind.Capture;
                  case LongTermHistoryPolicy.FellInBattleStatus: return HistoryKind.FellInBattle;
                  case LongTermHistoryPolicy.RebelReturnedStatus: return HistoryKind.Rebellion;
                  default: return HistoryKind.None;
               }
            default:
               return HistoryKind.None;
         }
      }

      // ── Recording ─────────────────────────────────────────────────────────

      /// <summary>Appends an event (assigning an id/day if absent) and prunes the log to its bounds.</summary>
      public void Record(WorldEvent e)
      {
         if (e == null || string.IsNullOrWhiteSpace(e.Summary)) return;
         if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
         if (e.Day <= 0) e.Day = (int) CampaignTime.Now.ToDays;

         _events.Add(e);
         PruneAndKeepHistory((int) CampaignTime.Now.ToDays);
         ModLog.Info($"[WORLD] recorded {e.Type} (day {e.Day}, mag {e.Magnitude}{(e.InvolvesPlayer ? ", player" : "")}): {e.Summary}");
      }

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list: an autosave can run while the main thread is
               // still firing events (or landing an async LLM continuation), and mutating the list mid-
               // serialize would otherwise throw "collection modified" and lose the whole blob this save.
               var snapshot = new List<WorldEvent>(_events);
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[WORLD] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[WORLD] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }

            byte[] historyBytes;
            try { historyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new List<WorldEvent>(_history), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[WORLD] history serialize failed, persisting no history this save. {ex.GetType().Name}: {ex.Message}");
               historyBytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(HistorySaveKey, ref historyBytes); }
            catch (Exception ex) { ModLog.Error($"[WORLD] history byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is {Length: > 0})
               try { _events = JsonConvert.DeserializeObject<List<WorldEvent>>(Encoding.UTF8.GetString(bytes)) ?? new List<WorldEvent>(); }
               catch (Exception ex)
               {
                  _events = new List<WorldEvent>();
                  ModLog.Error($"[WORLD] load failed — world events dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }

            byte[] historyBytes = null;
            try { dataStore.SyncData(HistorySaveKey, ref historyBytes); }
            catch
            {
               /* key absent in saves before 2.7.13: the history starts empty */
            }

            _history = new List<WorldEvent>();
            if (historyBytes is {Length: > 0})
               try
               {
                  List<WorldEvent> loaded = JsonConvert.DeserializeObject<List<WorldEvent>>(Encoding.UTF8.GetString(historyBytes)) ?? new List<WorldEvent>();
                  _history = loaded.Where(e => e != null).GroupBy(e => e.Id ?? Guid.NewGuid().ToString("N")).Select(g => g.First()).ToList();
               }
               catch (Exception ex)
               {
                  ModLog.Error($"[WORLD] history load failed, the long-term memory starts empty so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }

            ModLog.Info($"[WORLD] loaded {_events.Count} world event(s) and {_history.Count} long-term event(s) from save.");
         }
      }

      #region private

      private static int GeoProximityBonus(Vec2? obs, string placeId)
      {
         if (obs == null || string.IsNullOrEmpty(placeId)) return 0;
         Settlement s = Settlement.Find(placeId);

         if (s == null) return 0;

         float d = s.GetPosition2D.Distance(obs.Value);

         if (d <= 50f) return RumorAwareness.GeoBonusMax;
         if (d >= 300f) return 0;

         return (int) (RumorAwareness.GeoBonusMax * (1f - (d - 50f) / 250f));
      }

      private static Vec2? ObserverPosition(Hero h)
      {
         if (h?.PartyBelongedTo != null) return h.PartyBelongedTo.GetPosition2D;
         if (h?.CurrentSettlement != null) return h.CurrentSettlement.GetPosition2D;

         return null;
      }

      // Engine glue: gather the facts (faction match, geo bonus from map distance, age) and let the
      // pure RumorAwareness model score them.
      //
      // M-G6: a concerned party (the event names them in ActorIds) is much likelier to hear of it. Since 2.5.22 the
      // hero-facing prompt queries pass the observer too (tashmetu: a bridge names the heroes of a siege in
      // HeroIds, and the besieged lord could not speak of it while the sway check said he knew). Settlement
      // queries have no observer and pass null.
      private RumorAwarenessBreakdown Explain(WorldEvent e, int today, Vec2? obsPos, string factionId, string observerId)
      {
         bool factionMatches = factionId != null && e.FactionIds != null && e.FactionIds.Contains(factionId);
         bool concerned = observerId != null && e.ActorIds != null && e.ActorIds.Contains(observerId);

         RumorAwarenessBreakdown verdict = RumorAwareness.Explain(e.Magnitude, factionMatches, GeoProximityBonus(obsPos, e.PlaceId), concerned, today - e.Day);

         // The player's veto on top of the score (IdentityCrysys, 03/10/2026): every query goes through here.
         verdict.Withheld = RumorDiscretion.WithheldReason(new DiscretionFacts {
            ObserverId = observerId, KnownOnlyToThoseInvolved = e.KnownOnlyToThoseInvolved, HiddenFromIds = e.HiddenFromIds,
            InvolvedIds = (e.ActorIds ?? new List<string>()).Concat(e.AggrievedActorIds ?? new List<string>())
         });
         if (verdict.Withheld != null) verdict.Knows = false;

         return verdict;
      }

      private IReadOnlyList<WorldEvent> GetKnown(Vec2? pos, string factionId, int today, int max, string observerId = null)
      {
         if (_events.Count == 0) return Array.Empty<WorldEvent>();

         return _events
                .Select(e => (e, v: Explain(e, today, pos, factionId, observerId)))
                .Where(t => t.v.Knows)
                .OrderByDescending(t => t.v.Score)
                .Take(max)
                .Select(t => t.e)
                .ToList();
      }

      private IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> GetKnownRated(Vec2? pos, string factionId, int today, int max, string observerId = null)
      {
         if (_events.Count == 0) return Array.Empty<(WorldEvent, RumorConfidence)>();

         return _events
                .Select(e => (e, v: Explain(e, today, pos, factionId, observerId)))
                .Where(t => t.v.Knows)
                .OrderByDescending(t => t.v.Score)
                .Take(max)
                .Select(t => (t.e, t.v.Confidence))
                .ToList();
      }

      private void OnDailyTick() => PruneAndKeepHistory((int) CampaignTime.Now.ToDays);

      // The one road for both prune sites: what leaves the news and is history moves to the long-term tier.
      private void PruneAndKeepHistory(int today)
      {
         List<WorldEvent> aged = WorldEventPruner.PruneAndHandOff(_events, today, IsHistory);
         if (aged.Count == 0) return;

         foreach (WorldEvent e in aged)
            if (!_history.Any(h => h.Id == e.Id))
               _history.Add(e);

         LongTermHistoryPolicy.Evict(_history);
         ModLog.Info($"[WORLD] {aged.Count} event(s) left the news for the long-term memory ({_history.Count} kept).");
      }

      private static bool IsHistory(WorldEvent e)
         => LongTermHistoryPolicy.IsMemorable(new HistoryCandidateFacts {
            Kind = HistoryKindOf(e), Magnitude = e.Magnitude, MenAtStart = e.MenAtStart, IsSiege = e.IsSiege
         });

      private static bool Names(WorldEvent e, string heroId)
         => heroId != null && (e.ActorIds != null && e.ActorIds.Contains(heroId) || e.AggrievedActorIds != null && e.AggrievedActorIds.Contains(heroId));

      // Engine glue for LongTermHistoryPolicy.WouldKnow: the observer's ties to the event, read from the game.
      // Actors are resolved with Hero.Find, which also finds the dead: an executed lord's house still remembers him.
      private static HistoryKnowledgeFacts KnowledgeFacts(Hero observer, WorldEvent e)
      {
         List<string> actorIds = (e.ActorIds ?? new List<string>()).Concat(e.AggrievedActorIds ?? new List<string>()).Distinct().ToList();
         HashSet<string> kin = KinIds(observer);
         Clan clan = observer.Clan;
         Settlement place = string.IsNullOrEmpty(e.PlaceId) ? null : Settlement.Find(e.PlaceId);

         return new HistoryKnowledgeFacts {
            IsActor = actorIds.Contains(observer.StringId),
            IsKinOfAnActor = actorIds.Any(kin.Contains),
            SharesAClanWithAnActor = clan != null && actorIds.Where(id => id != observer.StringId).Any(id => FindHero(id)?.Clan == clan),
            IsOfThePlace = place != null && (observer.HomeSettlement == place || clan != null && place.OwnerClan == clan),
            IsOfARealmInvolved = observer.MapFaction != null && e.FactionIds != null && e.FactionIds.Contains(observer.MapFaction.StringId),
            Magnitude = e.Magnitude,
            WithheldByThePlayer = RumorDiscretion.WithheldReason(new DiscretionFacts {
               ObserverId = observer.StringId, KnownOnlyToThoseInvolved = e.KnownOnlyToThoseInvolved, HiddenFromIds = e.HiddenFromIds,
               InvolvedIds = actorIds
            }) != null
         };
      }

      private static HashSet<string> KinIds(Hero h)
      {
         var ids = new HashSet<string>();

         void Add(Hero k)
         {
            if (k != null && k != h) ids.Add(k.StringId);
         }

         Add(h.Father);
         Add(h.Mother);
         Add(h.Spouse);
         foreach (Hero c in h.Children ?? Enumerable.Empty<Hero>()) Add(c);
         foreach (Hero s in h.Siblings ?? Enumerable.Empty<Hero>()) Add(s);

         return ids;
      }

      private static Hero FindHero(string id)
      {
         if (string.IsNullOrEmpty(id)) return null;
         try { return Hero.Find(id); }
         catch { return null; }
      }

      #endregion
   }
}