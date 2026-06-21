// Code written by Gabriel Mailhot, 21/06/2026.
// v1.3 schemes — the engine wiring around the pure CalradiaRemembers.Logic.SchemeAdvancer. Owns the
// list of active plots, advances them on a daily tick (zero LLM), applies the standing-damage effect
// when one succeeds, and feeds every state change to the world-event store as a rumour. The lifecycle
// and its tuning are pure and unit-tested; this class is only the engine glue (heroes, save, effects).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
    /// <summary>
    ///   Drives the v1.3 feud schemes: bitter rivals quietly plot against one another, the plots
    ///   mature or collapse mechanically each day, and word of them spreads through the rumour
    ///   network. The player never triggers the simulation — they only ever hear of it. Persisted as
    ///   a UTF-8 byte[] (mirrors <see cref="WorldEventStore"/>) with the same "never brick a save"
    ///   degradation.
    /// </summary>
    public sealed class SchemeStore : CampaignBehaviorBase
    {
        public static SchemeStore Instance;

        private const string SaveKey = "CalradiaRemembers_Schemes_v1bytes";

        private const int MaxActiveSchemes = 3;       // keep the world's intrigue ambient, not a flood
        private const int DailySpawnChancePercent = 8; // most days, no new plot is hatched
        private const int RivalryThreshold = -30;     // mutual relation at/below = bitter rivals (matches the gossip feeder)
        private const int PairCooldownDays = 40;      // don't immediately re-plot the same feud
        private const int MaxLordsPerKingdomScan = 40;

        private const int SuccessInfluenceHit = 15;   // standing damage the target's clan takes on success
        private const int SuccessRelationHit = 5;     // and the feud deepens a little more

        private List<Scheme> _schemes = new List<Scheme>();
        private readonly Dictionary<string, int> _pairCooldown = new Dictionary<string, int>(); // non-persisted; reload is rare

        public SchemeStore() => Instance = this;

        /// <summary>The plots currently in motion — for diagnostics (cr.schemes).</summary>
        public IReadOnlyList<Scheme> Active => _schemes;

        /// <summary>Runs one tick of advancement only (no spawn roll) — for console testing without waiting days.</summary>
        public void DebugAdvance() => AdvanceActive((int) CampaignTime.Now.ToDays);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnDailyTick()
        {
            int today = (int) CampaignTime.Now.ToDays;
            AdvanceActive(today);
            TrySpawn(today);
        }

        /// <summary>
        ///   Plots a scheme by force (console testing) — bypasses the spawn roll but uses the real
        ///   advance/effect/rumour pipeline from the next daily tick. Returns a short status line.
        /// </summary>
        public string ForcePlot(Hero plotter, Hero target, SchemeKind kind)
        {
            if (plotter == null || target == null || plotter == target) return "Need two distinct heroes.";
            if (HasActiveBetween(plotter.StringId, target.StringId)) return $"A scheme between {plotter.Name} and {target.Name} is already afoot.";

            _schemes.Add(new Scheme {
                PlotterId = plotter.StringId, TargetId = target.StringId, Kind = kind, State = SchemeState.Plotted
            });
            ModLog.Info($"[SCHEME] plotted (forced): {plotter.Name} → {target.Name} ({kind}).");
            return $"{plotter.Name} now plots {kind} against {target.Name}. Advance a day to see it move.";
        }

        // ── Advancing active schemes ──────────────────────────────────────────

        private void AdvanceActive(int today)
        {
            // Iterate a copy: the list is mutated as terminal schemes are removed.
            foreach (Scheme scheme in _schemes.ToList())
            {
                Hero plotter = FindHero(scheme.PlotterId);
                Hero target  = FindHero(scheme.TargetId);

                // A dead or vanished party ends the plot quietly.
                if (plotter == null || !plotter.IsAlive || target == null || !target.IsAlive)
                {
                    _schemes.Remove(scheme);
                    continue;
                }

                SchemeState before = scheme.State;
                SchemeAdvancer.Advance(scheme, BuildFactors(plotter, target), MBRandomSource.Instance);

                if (scheme.State != before)
                    OnStateChanged(scheme, plotter, target, today);

                if (SchemeAdvancer.IsTerminal(scheme.State))
                    _schemes.Remove(scheme);
            }
        }

        private void OnStateChanged(Scheme scheme, Hero plotter, Hero target, int today)
        {
            switch (scheme.State)
            {
                case SchemeState.Progressing: // the plot is now afoot — a first whisper
                    RecordRumor(scheme, plotter, target, today, "plotted", 18,
                        $"There is talk that {plotter.Name} is quietly moving against {target.Name}.");
                    break;

                case SchemeState.Discovered: // it has come to light
                    RecordRumor(scheme, plotter, target, today, "discovered", 28,
                        $"{plotter.Name}'s plot against {target.Name} has come to light.");
                    break;

                case SchemeState.Succeeded:
                    ApplyStandingDamage(plotter, target);
                    RecordRumor(scheme, plotter, target, today, "succeeded", 34, scheme.Kind == SchemeKind.Slander
                        ? $"{plotter.Name} has blackened {target.Name}'s name, and it has cost them dearly."
                        : $"{plotter.Name} has undermined {target.Name}, weakening their standing.");
                    break;

                case SchemeState.Failed:
                    RecordRumor(scheme, plotter, target, today, "failed", 20,
                        $"{plotter.Name}'s scheme against {target.Name} came to nothing.");
                    break;
            }
        }

        /// <summary>The v1 effect: a hit to the target clan's influence, and the feud deepens.</summary>
        private static void ApplyStandingDamage(Hero plotter, Hero target)
        {
            try
            {
                if (target.Clan != null)
                    ChangeClanInfluenceAction.Apply(target.Clan, -SuccessInfluenceHit);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(plotter, target, -SuccessRelationHit, false);
                ModLog.Info($"[SCHEME] success effect: {target.Name} clan influence -{SuccessInfluenceHit}, {plotter.Name}<->{target.Name} relation -{SuccessRelationHit}.");
            }
            catch (Exception ex) { ModLog.Error($"[SCHEME] effect failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        private static void RecordRumor(Scheme scheme, Hero plotter, Hero target, int today, string status, int magnitude, string summary)
        {
            WorldEventStore.Instance?.Record(new WorldEvent {
                Type           = WorldEventType.Rivalry, // a scheme is an active rivalry; Status carries its phase
                Day            = today,
                Magnitude      = magnitude,
                InvolvesPlayer = false,
                Status         = status,
                Summary        = summary,
                ActorIds       = new List<string> { plotter.StringId, target.StringId },
                FactionIds     = new[] { plotter.MapFaction?.StringId, target.MapFaction?.StringId }
                                 .Where(s => s != null).Distinct().ToList()
            });
        }

        // ── Spawning new schemes ──────────────────────────────────────────────

        private void TrySpawn(int today)
        {
            if (_schemes.Count >= MaxActiveSchemes) return;
            if (MBRandom.RandomInt(100) >= DailySpawnChancePercent) return;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                var lords = new List<Hero>();
                foreach (Clan clan in kingdom.Clans)
                {
                    foreach (Hero h in clan.Heroes)
                        if (h != null && h.IsAlive && h.IsLord && h != Hero.MainHero)
                            lords.Add(h);
                    if (lords.Count >= MaxLordsPerKingdomScan) break;
                }

                if (TryPlotAmong(lords, today)) return; // one new plot per spawning day
            }
        }

        private bool TryPlotAmong(List<Hero> lords, int today)
        {
            for (int i = 0; i < lords.Count; i++)
                for (int j = i + 1; j < lords.Count; j++)
                {
                    Hero a = lords[i], b = lords[j];
                    if (a.GetRelation(b) > RivalryThreshold) continue;

                    string key = PairKey(a.StringId, b.StringId);
                    if (_pairCooldown.TryGetValue(key, out int last) && today - last < PairCooldownDays) continue;
                    if (HasActiveBetween(a.StringId, b.StringId)) continue;

                    // The bolder schemer of the two moves first against the other.
                    Hero plotter = Cunning(a) >= Cunning(b) ? a : b;
                    Hero target  = plotter == a ? b : a;
                    SchemeKind kind = MBRandom.RandomInt(2) == 0 ? SchemeKind.Slander : SchemeKind.Sabotage;

                    _schemes.Add(new Scheme {
                        PlotterId = plotter.StringId, TargetId = target.StringId, Kind = kind, State = SchemeState.Plotted
                    });
                    _pairCooldown[key] = today;
                    ModLog.Info($"[SCHEME] plotted: {plotter.Name} → {target.Name} ({kind}).");
                    return true;
                }
            return false;
        }

        // ── Factors (engine traits → pure SchemeFactors) ──────────────────────

        private static SchemeFactors BuildFactors(Hero plotter, Hero target)
        {
            return new SchemeFactors {
                PlotterCunning  = Cunning(plotter),
                PlotterBoldness = Clamp0to100(30 + TraitLevel(plotter, DefaultTraits.Valor) * 20),
                Animosity       = Clamp0to100(-plotter.GetRelation(target)),
                TargetVigilance = Cunning(target)
            };
        }

        private static int Cunning(Hero h) => Clamp0to100(30 + TraitLevel(h, DefaultTraits.Calculating) * 20);

        private static int TraitLevel(Hero h, TraitObject trait)
        {
            try { return h?.GetTraitLevel(trait) ?? 0; }
            catch { return 0; }
        }

        private static int Clamp0to100(int v) => Math.Max(0, Math.Min(100, v));

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool HasActiveBetween(string id1, string id2)
            => _schemes.Any(s => (s.PlotterId == id1 && s.TargetId == id2) || (s.PlotterId == id2 && s.TargetId == id1));

        private static Hero FindHero(string id)
            => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);

        private static string PairKey(string a, string b)
            => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        // ── Persistence (mirrors WorldEventStore) ─────────────────────────────

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_schemes, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[SCHEME] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[SCHEME] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch { /* key absent in older saves */ }

                if (bytes != null && bytes.Length > 0)
                {
                    try { _schemes = JsonConvert.DeserializeObject<List<Scheme>>(Encoding.UTF8.GetString(bytes)) ?? new List<Scheme>(); }
                    catch (Exception ex)
                    {
                        _schemes = new List<Scheme>();
                        ModLog.Error($"[SCHEME] load failed — schemes dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
                    }
                }

                ModLog.Info($"[SCHEME] loaded {_schemes.Count} active scheme(s) from save.");
            }
        }
    }
}
