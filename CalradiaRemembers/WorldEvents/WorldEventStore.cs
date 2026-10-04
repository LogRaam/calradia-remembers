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
      public static WorldEventStore Instance;

      private List<WorldEvent> _events = new();

      public WorldEventStore()
      {
         Instance = this;
      }

      public int Count => _events.Count;

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

         int removed = _events.RemoveAll(e => e.Id == id);
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

      // ── Recording ─────────────────────────────────────────────────────────

      /// <summary>Appends an event (assigning an id/day if absent) and prunes the log to its bounds.</summary>
      public void Record(WorldEvent e)
      {
         if (e == null || string.IsNullOrWhiteSpace(e.Summary)) return;
         if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
         if (e.Day <= 0) e.Day = (int) CampaignTime.Now.ToDays;

         _events.Add(e);
         WorldEventPruner.Prune(_events, (int) CampaignTime.Now.ToDays);
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

            ModLog.Info($"[WORLD] loaded {_events.Count} world event(s) from save.");
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

      private void OnDailyTick() => WorldEventPruner.Prune(_events, (int) CampaignTime.Now.ToDays);

      #endregion
   }
}