// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 (world-event / rumor store): persisted, bounded log of dated world happenings, with an
// awareness query (who would plausibly have heard what) and a player-chronicle view.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
    /// <summary>
    ///   Owns the world-event log. Feeders call <see cref="Record"/> from campaign-event listeners
    ///   (zero LLM); consumers query it. Two lifetimes live here on purpose: third-party events are
    ///   rumor fodder and are pruned aggressively (<see cref="RumorWindowDays"/> / a cap), while
    ///   <see cref="WorldEvent.InvolvesPlayer"/> events are the chronicle and are kept long-term.
    ///   Persisted as a UTF-8 byte[] (no native string-size ceiling, like the profile store) with the
    ///   same "never brick a save" degradation.
    /// </summary>
    public sealed class WorldEventStore : CampaignBehaviorBase
    {
        public static WorldEventStore Instance;

        private const string SaveKey = "CalradiaRemembers_WorldEvents_v1bytes";

        // ── Lifetimes / bounds ────────────────────────────────────────────────
        private const int RumorWindowDays  = 42;   // third-party events fade after ~2 seasons
        private const int MaxRumorEvents   = 200;  // hard cap on retained third-party events
        private const int MaxPlayerEvents  = 1000; // safety cap on the chronicle (a very long saga)

        // ── Awareness scoring (tunable) ───────────────────────────────────────
        // score = reach × recency, where reach = magnitude + faction + geography. Recency is a
        // 1→0 MULTIPLIER (not an additive bonus) so a faint rumour is NOT known everywhere merely
        // because it is fresh — it still needs reach (a big event, the observer's own faction, or
        // proximity). A town falling carries far; a whispered liaison only reaches those near.
        private const int   AwarenessThreshold = 50; // score at/above which the observer "knows" it
        private const int   RecencyWindowDays  = 40; // news fades to nothing over this many days
        private const int   FactionBonus       = 25; // the event touches the observer's own faction
        private const int   GeoBonusMax        = 25; // it happened on the observer's doorstep

        private List<WorldEvent> _events = new List<WorldEvent>();

        public WorldEventStore() => Instance = this;

        public int Count => _events.Count;

        // ── Recording ─────────────────────────────────────────────────────────

        /// <summary>Appends an event (assigning an id/day if absent) and prunes the log to its bounds.</summary>
        public void Record(WorldEvent e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Summary)) return;
            if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
            if (e.Day <= 0) e.Day = (int) CampaignTime.Now.ToDays;

            _events.Add(e);
            Prune((int) CampaignTime.Now.ToDays);
            ModLog.Info($"[WORLD] recorded {e.Type} (day {e.Day}, mag {e.Magnitude}{(e.InvolvesPlayer ? ", player" : "")}): {e.Summary}");
        }

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>
        ///   The events <paramref name="observer"/> would plausibly have heard of, best-known first,
        ///   scored by recency, reach (magnitude), faction relevance, and geographic proximity.
        /// </summary>
        public IReadOnlyList<WorldEvent> GetKnownEvents(Hero observer, int today, int max)
            => observer == null
                ? Array.Empty<WorldEvent>()
                : GetKnown(ObserverPosition(observer), observer.MapFaction?.StringId, today, max);

        /// <summary>The events a settlement's locals would have heard of (commoner intel).</summary>
        public IReadOnlyList<WorldEvent> GetKnownEventsAt(Settlement settlement, int today, int max)
            => settlement == null
                ? Array.Empty<WorldEvent>()
                : GetKnown(settlement.GetPosition2D, settlement.MapFaction?.StringId, today, max);

        private IReadOnlyList<WorldEvent> GetKnown(Vec2? pos, string factionId, int today, int max)
        {
            if (_events.Count == 0) return Array.Empty<WorldEvent>();

            return _events
                .Select(e => (e, score: AwarenessScore(e, today, pos, factionId)))
                .Where(t => t.score >= AwarenessThreshold)
                .OrderByDescending(t => t.score)
                .Take(max)
                .Select(t => t.e)
                .ToList();
        }

        /// <summary>The player's deeds in chronological order — the source for the chronicle.</summary>
        public IReadOnlyList<WorldEvent> GetPlayerChronicle()
            => _events.Where(e => e.InvolvesPlayer).OrderBy(e => e.Day).ToList();

        /// <summary>The stored event with this id, or null if it never existed or has been pruned.</summary>
        public WorldEvent GetById(string id)
            => string.IsNullOrEmpty(id) ? null : _events.FirstOrDefault(e => e.Id == id);

        /// <summary>True when <paramref name="observer"/> would by now have heard of <paramref name="e"/>.</summary>
        public bool IsAwareOf(Hero observer, WorldEvent e, int today)
            => observer != null && e != null
               && AwarenessScore(e, today, ObserverPosition(observer), observer.MapFaction?.StringId) >= AwarenessThreshold;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnDailyTick() => Prune((int) CampaignTime.Now.ToDays);

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_events, Formatting.None)); }
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
                catch { /* key absent in older saves */ }

                if (bytes != null && bytes.Length > 0)
                {
                    try { _events = JsonConvert.DeserializeObject<List<WorldEvent>>(Encoding.UTF8.GetString(bytes)) ?? new List<WorldEvent>(); }
                    catch (Exception ex)
                    {
                        _events = new List<WorldEvent>();
                        ModLog.Error($"[WORLD] load failed — world events dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
                    }
                }

                ModLog.Info($"[WORLD] loaded {_events.Count} world event(s) from save.");
            }
        }

        #region private

        private int AwarenessScore(WorldEvent e, int today, Vec2? obsPos, string factionId)
        {
            int age = Math.Max(0, today - e.Day);
            float recency = Math.Max(0f, 1f - age / (float) RecencyWindowDays);
            int faction = factionId != null && e.FactionIds != null && e.FactionIds.Contains(factionId) ? FactionBonus : 0;
            int reach = e.Magnitude + faction + GeoProximityBonus(obsPos, e.PlaceId);
            return (int) (reach * recency);
        }

        /// <summary>Keeps the log bounded: fresh-ish/capped for third-party rumors, long-lived for player deeds.</summary>
        private void Prune(int today)
        {
            // Drop stale third-party rumors (player deeds never age out).
            _events.RemoveAll(e => !e.InvolvesPlayer && today - e.Day > RumorWindowDays);

            // Cap third-party rumors (oldest first) so a long campaign can't bloat the save.
            List<WorldEvent> rumors = _events.Where(e => !e.InvolvesPlayer).ToList();
            if (rumors.Count > MaxRumorEvents)
                foreach (WorldEvent old in rumors.OrderBy(e => e.Day).Take(rumors.Count - MaxRumorEvents))
                    _events.Remove(old);

            // Safety cap on the chronicle itself (oldest deeds first) — only an extreme saga hits this.
            List<WorldEvent> deeds = _events.Where(e => e.InvolvesPlayer).ToList();
            if (deeds.Count > MaxPlayerEvents)
                foreach (WorldEvent old in deeds.OrderBy(e => e.Day).Take(deeds.Count - MaxPlayerEvents))
                    _events.Remove(old);
        }

        private static int GeoProximityBonus(Vec2? obs, string placeId)
        {
            if (obs == null || string.IsNullOrEmpty(placeId)) return 0;
            Settlement s = Settlement.Find(placeId);
            if (s == null) return 0;

            float d = s.GetPosition2D.Distance(obs.Value);
            if (d <= 50f) return GeoBonusMax;
            if (d >= 300f) return 0;
            return (int) (GeoBonusMax * (1f - (d - 50f) / 250f));
        }

        private static Vec2? ObserverPosition(Hero h)
        {
            if (h?.PartyBelongedTo != null) return h.PartyBelongedTo.GetPosition2D;
            if (h?.CurrentSettlement != null) return h.CurrentSettlement.GetPosition2D;
            return null;
        }

        #endregion
    }
}
