// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar: the engine store. Mirrors the WorldEvents/SchemeStore.cs byte[] persistence shape (a
// single frozen-copy JSON blob, immune to an autosave racing a main-thread mutation mid-serialize). Owns
// the one-shot idempotent backfill (GrudgeMigration.SynthesizeGrudges fed by GrudgeBackfillRules scanning
// every profile's Events/ActiveQuests and the WorldEventStore) and the live TryPlant/GrudgesFor/
// LiveGrudgesFor API the tier-1 sources call into (JealousyBehavior, SchemeStore, QuestService,
// DivorceProcessBehavior). No daily tick: decay is a live read (GrudgePolicy.EffectiveMagnitude), never a
// stored mutation, so nothing here needs to run on a schedule.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Grudges
{
    /// <summary>
    ///   Owns the persisted <see cref="GrudgeLedger" /> and the one-shot backfill that synthesizes grudges
    ///   from the markers already sitting in a save (the ROADMAP's hard save-migration requirement). Never
    ///   throws from <see cref="SyncData" /> — a corrupt blob degrades to an empty ledger, exactly like
    ///   <see cref="Bastards.BastardBehavior" /> / <see cref="SchemeStore" />.
    /// </summary>
    public sealed class GrudgeBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_Grudges_v1bytes";

        public static GrudgeBehavior Instance;

        private GrudgeLedger _ledger = new GrudgeLedger();

        public GrudgeBehavior()
        {
            Instance = this;
        }

        /// <summary>Every grudge ever formed, live and resolved — for the cr.grudges dev-truth diagnostic.</summary>
        public IReadOnlyList<Grudge> All => _ledger.Grudges;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            // Intentionally empty: no daily tick. Decay is computed live on every read
            // (GrudgePolicy.EffectiveMagnitude); the ledger itself only grows via TryPlant/backfill.
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try
                {
                    // Serialize a frozen copy, not the live list (see SchemeStore/WorldEventStore): an
                    // autosave racing a main-thread mutation would otherwise throw mid-serialize and drop
                    // the whole blob this save.
                    var snapshot = new GrudgeLedger {
                        Grudges = new List<Grudge>(_ledger.Grudges),
                        MigrationVersion = _ledger.MigrationVersion
                    };
                    bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[GRUDGE] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("{}");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[GRUDGE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in older saves — the whole point of this pillar's backfill.
                }

                if (bytes is {Length: > 0})
                    try { _ledger = JsonConvert.DeserializeObject<GrudgeLedger>(Encoding.UTF8.GetString(bytes)) ?? new GrudgeLedger(); }
                    catch (Exception ex)
                    {
                        _ledger = new GrudgeLedger();
                        ModLog.Error($"[GRUDGE] load failed — ledger reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _ledger = new GrudgeLedger();

                ModLog.Info($"[GRUDGE] loaded {_ledger.Grudges.Count} grudge(s), migration version {_ledger.MigrationVersion}.");
            }
        }

        // ── Live API (tier-1 sources call this) ─────────────────────────────────

        /// <summary>
        ///   Plants a new grudge for <paramref name="holder" /> against <paramref name="target" /> when the
        ///   anti-noise guardrails clear (<see cref="GrudgePolicy.ShouldForm" />) and the per-holder cap
        ///   admits it (<see cref="GrudgePolicy.TryAdmit" />: a full house only yields to a strictly
        ///   stronger newcomer, evicting the single weakest live grudge). Returns true when a grudge was
        ///   actually recorded. Never throws — a failure here must never break the caller's own relation
        ///   hit or memory event, which are applied independently before/after this call.
        /// </summary>
        public bool TryPlant(Hero holder, Hero target, GrudgeSource source, string cause, int magnitude, bool knownToPlayer, int today)
        {
            if (holder == null || target == null || string.IsNullOrEmpty(holder.StringId)) return false;

            try
            {
                NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(holder.StringId);
                bool possessive = source == GrudgeSource.Jealousy && GrudgeBridge.HasPossessiveAmplifier(profile);

                GrudgeCandidateFacts facts = GrudgeBridge.BuildCandidateFacts(holder, source, magnitude, possessive);
                if (!GrudgePolicy.ShouldForm(facts)) return false;

                int regard = holder.GetRelation(target);
                int rigidity = GrudgePolicy.RigidityAt(regard, facts);
                int effectiveMagnitude = GrudgePolicy.EffectiveFormationMagnitude(facts);

                List<Grudge> live = _ledger.Grudges.Where(g => g.HolderId == holder.StringId).ToList();
                if (!GrudgePolicy.TryAdmit(live, effectiveMagnitude, today, facts.Traits, out Grudge toEvict)) return false;

                if (toEvict != null) _ledger.Grudges.Remove(toEvict);

                var grudge = new Grudge {
                    Id = Guid.NewGuid().ToString("N"),
                    HolderId = holder.StringId,
                    TargetId = target.StringId,
                    Source = source,
                    CauseSummary = Truncate(cause),
                    EventDay = today,
                    Magnitude = effectiveMagnitude,
                    Rigidity = rigidity,
                    KnownToPlayer = knownToPlayer,
                    ResolutionHintDiscovered = false,
                    ResolutionKind = GrudgePolicy.ResolutionKindFor(source, rigidity),
                    IsResolved = false,
                    ResolvedDay = -1
                };

                _ledger.Grudges.Add(grudge);
                ModLog.Info($"[GRUDGE] planted: {holder.Name} vs {target.Name} ({source}, magnitude {effectiveMagnitude}, " +
                            $"rigidity {rigidity}, known={knownToPlayer}{(toEvict != null ? ", evicted weakest" : "")}).");

                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] TryPlant failed: {ex.GetType().Name}: {ex.Message}");

                return false;
            }
        }

        /// <summary>Every grudge (live and resolved) this holder has ever nursed — cr.grudges dev truth.</summary>
        public IReadOnlyList<Grudge> GrudgesFor(string holderId)
            => string.IsNullOrEmpty(holderId)
                ? Array.Empty<Grudge>()
                : _ledger.Grudges.Where(g => g.HolderId == holderId).ToList();

        /// <summary>The dominant LIVE grudges (per-NPC cap, ranked by today's effective magnitude) — feeds the prompt.</summary>
        public IReadOnlyList<Grudge> LiveGrudgesFor(Hero holder, int today)
        {
            if (holder == null) return Array.Empty<Grudge>();

            TraitFacts traits = GrudgeBridge.BuildTraitFacts(holder);

            return GrudgePolicy.SelectDominant(GrudgesFor(holder.StringId), today, traits).ToList();
        }

        /// <summary>
        ///   The dominant LIVE grudges this holder nurses that the PLAYER actually knows about — the ONLY
        ///   query the encyclopedia's "Grievances" section reads (never the full ledger). Reuses
        ///   <see cref="LiveGrudgesFor" /> for the same dominant ranking the prompt sees, then filters to
        ///   <see cref="Grudge.KnownToPlayer" />; order is preserved (already ranked by today's effective
        ///   magnitude). <c>cr.grudges</c> remains the dev-truth dump that shows everything regardless of
        ///   this gate.
        /// </summary>
        public IReadOnlyList<Grudge> KnownGrudgesFor(string holderId, int today)
        {
            if (string.IsNullOrEmpty(holderId)) return Array.Empty<Grudge>();

            Hero holder = FindHero(holderId);

            return LiveGrudgesFor(holder, today).Where(g => g.KnownToPlayer).ToList();
        }

        // ── One-shot backfill ────────────────────────────────────────────────

        /// <summary>
        ///   Runs the migration scan when the ledger's stamped version is behind
        ///   <see cref="GrudgeMigration.MigrationVersion" />; a pure no-op otherwise. Idempotent by
        ///   construction (the deterministic holder+source+day id dedups), so it is safe to call on EVERY
        ///   session launch. Returns (formed, skipped) for logging and the cr.grudge_backfill console command.
        /// </summary>
        public (int Formed, int Skipped) RunBackfillIfNeeded(BannerlordNpcMemoryStore store)
        {
            try
            {
                if (_ledger.MigrationVersion >= GrudgeMigration.MigrationVersion) return (0, 0);

                List<MigrationCandidate> candidates = ScanCandidates(store);
                GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(candidates, _ledger.Grudges, _ledger.MigrationVersion);

                if (result.Grudges.Count > 0) _ledger.Grudges.AddRange(result.Grudges);
                _ledger.MigrationVersion = result.MigrationVersion;

                ModLog.Info($"[GRUDGE] backfill: {result.Grudges.Count} formed, {result.Skipped.Count} skipped.");

                return (result.Grudges.Count, result.Skipped.Count);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] backfill failed — existing memories/relations are untouched (graceful degradation). {ex.GetType().Name}: {ex.Message}");

                return (0, 0);
            }
        }

        /// <summary>
        ///   Force-reruns the backfill scan regardless of the stamped version (cr.grudge_backfill testing).
        ///   Still idempotent — the deterministic id dedups against anything already in the ledger, so a
        ///   second call in the same session forms nothing new.
        /// </summary>
        public (int Formed, int Skipped) DebugForceBackfill(BannerlordNpcMemoryStore store)
        {
            _ledger.MigrationVersion = 0;

            return RunBackfillIfNeeded(store);
        }

        #region private

        /// <summary>
        ///   Scans every profile's Events + ActiveQuests and the WorldEventStore's player chronicle through
        ///   <see cref="GrudgeBackfillRules" />, resolving each holder's LIVE regard/traits at migration
        ///   time via <see cref="GrudgeBridge" />.
        /// </summary>
        private static List<MigrationCandidate> ScanCandidates(BannerlordNpcMemoryStore store)
        {
            var candidates = new List<MigrationCandidate>();

            string playerId = Hero.MainHero?.StringId;
            if (store == null || playerId == null) return candidates;

            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile == null) continue;

                Hero holder = FindHero(id);
                int regard = holder != null && Hero.MainHero != null ? holder.GetRelation(Hero.MainHero) : 0;
                TraitFacts traits = GrudgeBridge.BuildTraitFacts(holder);
                bool possessive = GrudgeBridge.HasPossessiveAmplifier(profile);

                foreach (NotableEvent ev in profile.Events ?? Enumerable.Empty<NotableEvent>())
                {
                    var facts = new ProfileEventFacts {
                        HolderId = id, EventTypeOrdinal = (int) ev.type, Summary = ev.summary, GameDay = ev.gameDay
                    };
                    MigrationCandidate candidate = GrudgeBackfillRules.FromProfileEvent(facts, playerId, regard, traits, possessive);
                    if (candidate != null) candidates.Add(candidate);
                }

                foreach (InformalQuest quest in profile.ActiveQuests ?? Enumerable.Empty<InformalQuest>())
                {
                    if (quest.Status != QuestStatus.Expired && quest.Status != QuestStatus.Abandoned) continue;

                    var facts = new QuestOutcomeFacts {
                        GiverId = id,
                        StatusName = quest.Status.ToString(),
                        IssuedOnDay = quest.IssuedOnDay,
                        DeadlineDay = quest.DeadlineDay,
                        Description = quest.Description
                    };
                    MigrationCandidate candidate = GrudgeBackfillRules.FromQuestOutcome(facts, playerId, regard, traits);
                    if (candidate != null) candidates.Add(candidate);
                }
            }

            WorldEventStore worldStore = WorldEventStore.Instance;
            if (worldStore != null)
                foreach (WorldEvent ev in worldStore.GetPlayerChronicle())
                {
                    string claimantId = ev.ActorIds != null && ev.ActorIds.Count > 0 ? ev.ActorIds[0] : null;
                    Hero claimant = FindHero(claimantId);
                    int regard = claimant != null && Hero.MainHero != null ? claimant.GetRelation(Hero.MainHero) : 0;
                    TraitFacts traits = GrudgeBridge.BuildTraitFacts(claimant);

                    var facts = new WorldEventFacts {
                        ActorIds = ev.ActorIds, Day = ev.Day, TypeName = ev.Type.ToString(), InvolvesPlayer = ev.InvolvesPlayer, Summary = ev.Summary
                    };
                    MigrationCandidate candidate = GrudgeBackfillRules.FromWorldEvent(facts, playerId, regard, traits);
                    if (candidate != null) candidates.Add(candidate);
                }

            return candidates;
        }

        private static Hero FindHero(string id)
            => string.IsNullOrEmpty(id)
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        private const int MaxCauseLength = GrudgeBackfillRules.MaxCauseLength;

        private static string Truncate(string cause)
        {
            if (string.IsNullOrWhiteSpace(cause)) return cause;
            string trimmed = cause.Trim();

            return trimmed.Length <= MaxCauseLength
                ? trimmed
                : trimmed.Substring(0, MaxCauseLength).TrimEnd() + "...";
        }

        #endregion
    }
}
