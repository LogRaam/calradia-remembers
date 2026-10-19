// Code written by Gabriel Mailhot, 06/07/2026.
// Grudges pillar, Tier 2 LIVE plant site: sway_opinion (SwayOpinionVerb) already turns an NPC's
// opinion of a named third party against them and lays down a Rivalry world-event rumour as it does.
// This behavior watches that very rumour for the swayed-against target: once WorldEventStore's own
// awareness model says the rumour has plausibly reached them (mirrors JealousyBehavior's own
// PendingAwareness gate exactly, no new spread mechanism invented), it plants a hidden SwayExposed
// grudge via the real Tier2GrudgeRules.SwayExposedGrudge rule and GrudgeBehavior.TryPlant gate. An
// unheard sway is simply forgotten after MaxAwaitDays, exactly like an unwitnessed liaison rumour.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Grudges
{
    /// <summary>
    ///   Owns the delayed reveal for the <see cref="GrudgeSource.SwayExposed" /> grudge source.
    ///   <see cref="QueueExposure" /> is called by <c>SwayOpinionVerb</c> whenever the player sways an
    ///   NPC's opinion AGAINST a named third party; the daily tick then checks the very Rivalry rumour
    ///   that sway already recorded, via <see cref="WorldEventStore.IsAwareOf" />, for that target.
    ///   Persisted so an in-flight watch survives save/load; a corrupt or absent blob degrades to an
    ///   empty watch list (a past sway simply never resolves into a grudge on an old save, never a
    ///   crash), and the SyncData key is brand new and purely additive.
    /// </summary>
    public sealed class SwayExposureBehavior : CampaignBehaviorBase
    {
        /// <summary>Tuning: an unheard sway is forgotten after this many days (mirrors JealousyBehavior.AwarenessMaxDays).</summary>
        public const int MaxAwaitDays = 30;

        private const string SaveKey = "CalradiaRemembers_SwayExposure_v1bytes";

        public static SwayExposureBehavior Instance;

        private List<PendingSwayExposure> _pending = new();

        public SwayExposureBehavior()
        {
            Instance = this;
        }

        /// <summary>Every in-flight watch (dev-truth diagnostic).</summary>
        public IReadOnlyList<PendingSwayExposure> Pending => _pending;

        /// <summary>
        ///   Test hook: forces the reveal for the oldest pending watch on this target, bypassing the
        ///   awareness TIMING only (never the real <see cref="Tier2GrudgeRules.SwayExposedGrudge" />
        ///   formation rule nor <see cref="GrudgeBehavior" />'s own anti-noise guardrails), and reports
        ///   the outcome. Backs <c>cr.grudge_sway_reveal</c> and the <c>grudge_sway</c> self-test.
        /// </summary>
        public string DebugForceReveal(Hero target)
        {
            if (target == null) return "No target NPC.";

            PendingSwayExposure pending = _pending.FirstOrDefault(p => p.TargetId == target.StringId);

            if (pending == null)
                return $"No pending sway exposure for {target.Name}. Sway an NPC's opinion AGAINST {target.Name} first (sway_opinion, stance=against).";

            var today = (int) CampaignTime.Now.ToDays;
            bool planted = Reveal(pending, target, today);
            _pending.Remove(pending);

            return planted
                ? $"{target.Name} learns of the sway and now nurses a hidden SwayExposed grudge against the player."
                : $"{target.Name}'s SwayExposed grudge was refused (anti-noise guardrail, or a full, stronger house).";
        }

        /// <summary>cr.testall seam: drops every watch on this rumour, so a test leaves nothing to reveal.</summary>
        internal int DebugDrop(string rumourEventId) => _pending.RemoveAll(p => p.RumourEventId == rumourEventId);

        /// <summary>
        ///   Queues <paramref name="target" />'s exposure watch for a sway just recorded as
        ///   <paramref name="rumourEventId" /> in the world-event store. Call only for a sway AGAINST the
        ///   target: a sway FOR them is a kindness, never a grudge, so nothing should ever be queued for
        ///   that direction. A null/empty <paramref name="rumourEventId" /> (e.g. WorldEventStore inactive)
        ///   is a safe no-op: there is nothing to watch for.
        /// </summary>
        public void QueueExposure(Hero target, string rumourEventId, int swayMagnitude, int today, string swayedName = null, string cause = null)
        {
            if (target == null || string.IsNullOrEmpty(rumourEventId)) return;

            _pending.Add(new PendingSwayExposure {
                TargetId = target.StringId,
                RumourEventId = rumourEventId,
                SwayMagnitude = swayMagnitude,
                CreatedDay = today,
                SwayedName = swayedName, // 5.6: named in the grudge cause when it forms
                Cause = cause
            });
        }

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
                    // Serialize a frozen copy, not the live list (mirrors WorldEventStore/SchemeStore): an
                    // autosave racing a main-thread mutation would otherwise throw mid-serialize and drop
                    // the whole blob this save.
                    var snapshot = new List<PendingSwayExposure>(_pending);
                    bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[GRUDGE] sway exposure serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[GRUDGE] sway exposure byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: the whole point of a
                    // brand new, additive SyncData key: it degrades to an empty watch list, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _pending = JsonConvert.DeserializeObject<List<PendingSwayExposure>>(Encoding.UTF8.GetString(bytes)) ?? new List<PendingSwayExposure>(); }
                    catch (Exception ex)
                    {
                        _pending = new List<PendingSwayExposure>();
                        ModLog.Error($"[GRUDGE] sway exposure load failed, watch list dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _pending = new List<PendingSwayExposure>();

                ModLog.Info($"[GRUDGE] loaded {_pending.Count} pending sway exposure watch(es) from save.");
            }
        }

        /// <summary>A sway_opinion rumour watched for this target's own awareness, per <see cref="WorldEventStore.IsAwareOf" />.</summary>
        public sealed class PendingSwayExposure
        {
            public int CreatedDay { get; set; }
            public string RumourEventId { get; set; }
            public int SwayMagnitude { get; set; }
            public string TargetId { get; set; }

            /// <summary>5.6 (regard audit): the name of the NPC the player swayed, so the grudge cause names them instead of "someone". Null on old saves.</summary>
            public string SwayedName { get; set; }

            /// <summary>
            ///   The grudge's cause when the talk was not a sway of one person's opinion: press_for_war (Gabriel, 18/10/2026)
            ///   watches its talk of war the same way. Null for a sway and on old saves.
            /// </summary>
            public string Cause { get; set; }
        }

        #region private

        private void EvaluatePending(int today)
        {
            if (_pending.Count == 0) return;

            var store = WorldEventStore.Instance;
            var resolved = new List<PendingSwayExposure>();

            foreach (PendingSwayExposure pending in _pending)
            {
                if (today - pending.CreatedDay > MaxAwaitDays)
                {
                    resolved.Add(pending); // unheard, nothing left to grudge over

                    continue;
                }

                Hero target = Hero.FindFirst(h => h.StringId == pending.TargetId);
                WorldEvent rumour = store?.GetById(pending.RumourEventId);

                if (target is not {IsAlive: true} || rumour == null)
                {
                    resolved.Add(pending);

                    continue;
                }

                if (!store.IsAwareOf(target, rumour, today)) continue;

                Reveal(pending, target, today);
                resolved.Add(pending);
            }

            foreach (PendingSwayExposure pending in resolved) _pending.Remove(pending);
        }

        private void OnDailyTick()
        {
            var today = (int) CampaignTime.Now.ToDays;
            SafeRun.Step("SwayExposureBehavior.EvaluatePending", () => EvaluatePending(today));
        }

        /// <summary>
        ///   Runs the real <see cref="Tier2GrudgeRules.SwayExposedGrudge" /> rule against a now-reached
        ///   rumour and, if it forms, plants it through the real <see cref="GrudgeBehavior.TryPlant" />
        ///   gate. Returns whether a grudge was actually recorded.
        /// </summary>
        private static bool Reveal(PendingSwayExposure pending, Hero target, int today)
        {
            if (Hero.MainHero == null || GrudgeBehavior.Instance == null) return false;

            var facts = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = pending.SwayMagnitude};
            GrudgeProposal proposal = Tier2GrudgeRules.SwayExposedGrudge(facts);

            if (!proposal.Forms) return false;

            // 5.6: name the swayed party when we recorded it (old saves fall back to "someone").
            string swayed = string.IsNullOrWhiteSpace(pending.SwayedName) ? "someone" : pending.SwayedName;
            bool planted = GrudgeBehavior.Instance.TryPlant(target, Hero.MainHero, GrudgeSource.SwayExposed,
                pending.Cause ?? $"Word reached them that the player had turned {swayed}'s opinion against them.",
                proposal.Magnitude, proposal.KnownToPlayer, today);

            if (planted)
                ModLog.Info($"[GRUDGE] sway exposed: {target.Name} learns of the sway and nurses a hidden grudge (magnitude {proposal.Magnitude}).");

            return planted;
        }

        #endregion
    }
}
