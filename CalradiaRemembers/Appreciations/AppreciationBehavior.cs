// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Storage;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Appreciations
{
   /// <summary>
   ///   Owns the persisted <see cref="AppreciationLedger" /> and the one-shot backfill that synthesizes
   ///   appreciations from the markers already sitting in a save (the ROADMAP's hard save-migration
   ///   requirement). Never throws from <see cref="SyncData" /> — a corrupt blob degrades to an empty
   ///   ledger, exactly like <see cref="Grudges.GrudgeBehavior" />.
   /// </summary>
   public sealed class AppreciationBehavior : CampaignBehaviorBase
   {
      private const int MaxCauseLength = AppreciationBackfillRules.MaxCauseLength;
      private const string SaveKey = "CalradiaRemembers_Appreciations_v1bytes";

      public static AppreciationBehavior Instance;

      private AppreciationLedger _ledger = new();

      public AppreciationBehavior()
      {
         Instance = this;
      }

      /// <summary>Every appreciation ever formed, live and fully-faded alike — for the cr.appreciations dev-truth diagnostic.</summary>
      public IReadOnlyList<Appreciation> All => _ledger.Appreciations;

      /// <summary>Every appreciation (live and fully-faded) this holder has ever felt — cr.appreciations dev truth.</summary>
      public IReadOnlyList<Appreciation> AppreciationsFor(string holderId)
         => string.IsNullOrEmpty(holderId)
            ? Array.Empty<Appreciation>()
            : _ledger.Appreciations.Where(a => a.HolderId == holderId).ToList();

      /// <summary>
      ///   Force-reruns the backfill scan regardless of the stamped version (cr.appreciation_backfill
      ///   testing). Still idempotent — the deterministic id dedups against anything already in the
      ///   ledger, so a second call in the same session forms nothing new.
      /// </summary>
      public (int Formed, int Skipped) DebugForceBackfill(BannerlordNpcMemoryStore store)
      {
         _ledger.MigrationVersion = 0;

         return RunBackfillIfNeeded(store);
      }

      /// <summary>
      ///   The dominant LIVE appreciations this holder feels that the PLAYER actually knows about — the
      ///   ONLY query the encyclopedia's "Kindnesses" section reads (never the full ledger). Reuses
      ///   <see cref="LiveAppreciationsFor" /> for the same dominant ranking the prompt sees, then filters
      ///   to <see cref="Appreciation.KnownToPlayer" />; order is preserved. <c>cr.appreciations</c>
      ///   remains the dev-truth dump that shows everything regardless of this gate.
      /// </summary>
      public IReadOnlyList<Appreciation> KnownAppreciationsFor(string holderId, int today)
      {
         if (string.IsNullOrEmpty(holderId)) return Array.Empty<Appreciation>();

         Hero holder = FindHero(holderId);

         return LiveAppreciationsFor(holder, today).Where(a => a.KnownToPlayer).ToList();
      }

      /// <summary>The dominant LIVE appreciations (per-NPC cap, ranked by today's effective magnitude) — feeds the prompt.</summary>
      public IReadOnlyList<Appreciation> LiveAppreciationsFor(Hero holder, int today)
      {
         if (holder == null) return Array.Empty<Appreciation>();

         TraitFacts traits = AppreciationBridge.BuildTraitFacts(holder);

         return AppreciationPolicy.SelectDominant(AppreciationsFor(holder.StringId), today, traits).ToList();
      }

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         // Intentionally empty: no daily tick. Decay is computed live on every read
         // (AppreciationPolicy.EffectiveMagnitude); the ledger itself only grows via TryPlant/backfill.
      }

      // ── One-shot backfill ────────────────────────────────────────────────

      /// <summary>
      ///   Runs the migration scan when the ledger's stamped version is behind
      ///   <see cref="AppreciationMigration.MigrationVersion" />; a pure no-op otherwise. Idempotent by
      ///   construction (the deterministic holder+source+day id dedups), so it is safe to call on EVERY
      ///   session launch. Returns (formed, skipped) for logging and the cr.appreciation_backfill console
      ///   command.
      /// </summary>
      public (int Formed, int Skipped) RunBackfillIfNeeded(BannerlordNpcMemoryStore store)
      {
         try
         {
            if (_ledger.MigrationVersion >= AppreciationMigration.MigrationVersion) return (0, 0);

            List<MigrationCandidate> candidates = ScanCandidates(store);
            AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(candidates, _ledger.Appreciations, _ledger.MigrationVersion);

            if (result.Appreciations.Count > 0) _ledger.Appreciations.AddRange(result.Appreciations);
            _ledger.MigrationVersion = result.MigrationVersion;

            ModLog.Info($"[APPRECIATION] backfill: {result.Appreciations.Count} formed, {result.Skipped.Count} skipped.");

            return (result.Appreciations.Count, result.Skipped.Count);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] backfill failed — existing memories/relations are untouched (graceful degradation). {ex.GetType().Name}: {ex.Message}");

            return (0, 0);
         }
      }

      /// <summary>
      ///   3.3 (regard audit): drops fully-faded appreciations (effective magnitude 0, pure ledger noise that no
      ///   query ever surfaces) and any held by a dead or vanished holder, at save, so the ledger and the save do
      ///   not grow without bound. A still-felt memory (a generous soul's lingering gratitude) is kept. Main-thread
      ///   (save); never throws.
      /// </summary>
      private void PruneFadedOrOrphaned()
      {
         try
         {
            if (_ledger?.Appreciations == null) return;

            var today = (int) CampaignTime.Now.ToDays;
            var aliveById = new Dictionary<string, Hero>();
            foreach (Hero h in Hero.AllAliveHeroes)
               if (h?.StringId != null)
                  aliveById[h.StringId] = h;

            int removed = _ledger.Appreciations.RemoveAll(a =>
               a == null
               || !aliveById.TryGetValue(a.HolderId, out Hero holder)
               || AppreciationPolicy.EffectiveMagnitude(a, today, AppreciationBridge.BuildTraitFacts(holder)) <= 0);

            if (removed > 0) ModLog.Info($"[APPRECIATION] pruned {removed} faded/orphaned appreciation(s) at save.");
         }
         catch (Exception ex) { ModLog.Error($"[APPRECIATION] prune failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            PruneFadedOrOrphaned(); // 3.3: bound ledger growth before persisting.

            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list (see GrudgeBehavior/SchemeStore): an
               // autosave racing a main-thread mutation would otherwise throw mid-serialize and drop
               // the whole blob this save.
               var snapshot = new AppreciationLedger {
                  Appreciations = new List<Appreciation>(_ledger.Appreciations),
                  MigrationVersion = _ledger.MigrationVersion
               };
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[APPRECIATION] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[APPRECIATION] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
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
               try { _ledger = JsonConvert.DeserializeObject<AppreciationLedger>(Encoding.UTF8.GetString(bytes)) ?? new AppreciationLedger(); }
               catch (Exception ex)
               {
                  _ledger = new AppreciationLedger();
                  ModLog.Error($"[APPRECIATION] load failed — ledger reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _ledger = new AppreciationLedger();

            ModLog.Info($"[APPRECIATION] loaded {_ledger.Appreciations.Count} appreciation(s), migration version {_ledger.MigrationVersion}.");
         }
      }

      // ── Live API (tier-1 sources call this) ─────────────────────────────────

      /// <summary>
      ///   Plants a new appreciation for <paramref name="holder" /> toward <paramref name="target" />
      ///   when the anti-noise guardrail clears (<see cref="AppreciationPolicy.ShouldForm" />) and the
      ///   per-holder cap admits it (<see cref="AppreciationPolicy.TryAdmit" />: a full house only yields
      ///   to a strictly stronger newcomer, evicting the single weakest live appreciation). Shows a quick
      ///   "will remember your kindness" banner ONLY when <paramref name="knownToPlayer" /> — an unvoiced
      ///   kindness stays silent, mirroring how a hidden grudge never announces itself either. Returns
      ///   true when an appreciation was actually recorded. Never throws — a failure here must never
      ///   break the caller's own relation hit or memory event, which are applied independently
      ///   before/after this call.
      /// </summary>
      public bool TryPlant(Hero holder, Hero target, AppreciationSource source, string cause, int magnitude, bool knownToPlayer, int today)
      {
         if (holder == null || target == null || string.IsNullOrEmpty(holder.StringId)) return false;

         try
         {
            AppreciationCandidateFacts facts = AppreciationBridge.BuildCandidateFacts(holder, source, magnitude);

            if (!AppreciationPolicy.ShouldForm(facts)) return false;

            List<Appreciation> live = _ledger.Appreciations.Where(a => a.HolderId == holder.StringId).ToList();

            if (!AppreciationPolicy.TryAdmit(live, magnitude, today, facts.Traits, out Appreciation toEvict)) return false;

            if (toEvict != null) _ledger.Appreciations.Remove(toEvict);

            var appreciation = new Appreciation {
               Id = Guid.NewGuid().ToString("N"),
               HolderId = holder.StringId,
               TargetId = target.StringId,
               Source = source,
               CauseSummary = Truncate(cause),
               EventDay = today,
               Magnitude = magnitude,
               KnownToPlayer = knownToPlayer
            };

            _ledger.Appreciations.Add(appreciation);
            ModLog.Info($"[APPRECIATION] planted: {holder.Name} toward {target.Name} ({source}, magnitude {magnitude}, " +
                        $"known={knownToPlayer}{(toEvict != null ? ", evicted weakest" : "")}).");

            if (knownToPlayer) NotifyFormed(holder);

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] TryPlant failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      #region private

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

      /// <summary>
      ///   Known-formation notification (P2 polish): a quick-info banner naming the holder, shown ONLY
      ///   when the appreciation forms known to the player — mirrors <see cref="Grudges.GrudgeBehavior" />'s
      ///   own known-grudge banner. Never throws.
      /// </summary>
      private static void NotifyFormed(Hero holder)
      {
         try
         {
            MBInformationManager.AddQuickInformation(
               new TextObject($"{holder.Name} will remember your kindness."), 0, holder.CharacterObject);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] quick-info banner failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Scans every profile's Events + ActiveQuests through <see cref="AppreciationBackfillRules" />
      ///   (only the two markers that have a confirmed, unambiguous existing shape today — see that
      ///   class's own header), resolving each holder's LIVE traits at migration time via
      ///   <see cref="AppreciationBridge" />.
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
            // 3.2 (regard audit): a dead (or vanished) holder cannot hold a fond memory; never backfill one.
            if (holder is not {IsAlive: true}) continue;

            TraitFacts traits = AppreciationBridge.BuildTraitFacts(holder);

            foreach (NotableEvent ev in profile.Events ?? Enumerable.Empty<NotableEvent>())
            {
               var facts = new ProfileEventFacts {
                  HolderId = id, EventTypeOrdinal = (int) ev.type, Summary = ev.summary, GameDay = ev.gameDay
               };
               MigrationCandidate candidate = AppreciationBackfillRules.FromProfileEvent(facts, playerId, traits);
               if (candidate != null) candidates.Add(candidate);
            }

            foreach (InformalQuest quest in profile.ActiveQuests ?? Enumerable.Empty<InformalQuest>())
            {
               if (quest.Status != QuestStatus.Completed) continue;

               var facts = new QuestOutcomeFacts {
                  GiverId = id,
                  StatusName = quest.Status.ToString(),
                  IssuedOnDay = quest.IssuedOnDay,
                  SatisfiedOnDay = quest.SatisfiedOnDay,
                  Description = quest.Description
               };
               MigrationCandidate candidate = AppreciationBackfillRules.FromQuestOutcome(facts, playerId, traits);
               if (candidate != null) candidates.Add(candidate);
            }
         }

         return candidates;
      }

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