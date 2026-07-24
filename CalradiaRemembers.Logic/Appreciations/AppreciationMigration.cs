// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Appreciations
{
   /// <summary>
   ///   One backfill candidate: a persisted marker the engine-side scan recognized, plus every fact
   ///   <see cref="AppreciationPolicy" /> needs to decide whether it becomes an <see cref="Appreciation" />.
   /// </summary>
   public sealed class MigrationCandidate
   {
      /// <summary>In-game day the original event occurred.</summary>
      public int EventDay { get; set; }

      /// <summary>Hero StringId of the NPC who would feel the appreciation.</summary>
      public string HolderId { get; set; }

      /// <summary>
      ///   Anti-noise guardrail passthrough. Defaults true: a marker that already exists in a save is, by definition,
      ///   attributable to a real event.
      /// </summary>
      public bool IsAttributable { get; set; } = true;

      /// <summary>The original event's own intensity/magnitude, 0..100.</summary>
      public int Magnitude { get; set; }

      /// <summary>
      ///   The raw persisted marker key (e.g. "kindness_in_scene", "quest_done_for_them") the
      ///   engine-side scan read off a save. Matched case-insensitively against
      ///   <see cref="AppreciationMigration" />'s known-marker table; anything unrecognized is
      ///   conservatively skipped rather than guessed at.
      /// </summary>
      public string Marker { get; set; }

      /// <summary>
      ///   The original event's own summary text, carried onto the synthesized appreciation's
      ///   <see cref="Appreciation.CauseSummary" /> when present.
      /// </summary>
      public string Summary { get; set; }

      /// <summary>Hero StringId of who the appreciation would be felt toward (usually the player).</summary>
      public string TargetId { get; set; }

      /// <summary>The holder's trait levels.</summary>
      public TraitFacts Traits { get; set; }
   }

   /// <summary>
   ///   The outcome of one migration pass: the appreciations newly synthesized, the candidates that were
   ///   conservatively skipped (unrecognized marker, failed the formation guardrail, or already present)
   ///   so the caller can log them, and whether the pass was a no-op because the ledger was already
   ///   stamped at or above <see cref="AppreciationMigration.MigrationVersion" />.
   /// </summary>
   public sealed class AppreciationMigrationResult
   {
      /// <summary>Newly synthesized appreciations this pass. Empty (never null) on a no-op pass.</summary>
      public List<Appreciation> Appreciations { get; set; } = new();

      /// <summary>The migration version this result is stamped with.</summary>
      public int MigrationVersion { get; set; }

      /// <summary>
      ///   Candidates that did NOT become an appreciation, for caller-side logging: unrecognized marker, guardrail
      ///   refusal, or duplicate.
      /// </summary>
      public List<MigrationCandidate> Skipped { get; set; } = new();

      /// <summary>True when the ledger was already migrated at or beyond this version: nothing was evaluated.</summary>
      public bool WasNoOp { get; set; }
   }

   /// <summary>
   ///   The pure migration engine. Conservative (unknown markers skipped), idempotent (re-running with
   ///   the same candidates and the previous result's appreciations as "existing" yields no new
   ///   appreciations), and additive.
   /// </summary>
   public static class AppreciationMigration
   {
      /// <summary>
      ///   The current migration version. Bump when the backfill logic changes in a way that should re-run against
      ///   already-migrated ledgers.
      ///   Version 2 adds <see cref="ConsolidateRepeats" />, which folds already-saved twin records into one
      ///   counted appreciation. HAZARD for whoever bumps this next: the candidate SCAN must not run again on a
      ///   ledger that has already been consolidated. Consolidation collapses several days into the surviving
      ///   record's newest day, so a re-scan would re-create the absorbed days as fresh twins and the next
      ///   consolidation would add their counts on top, inflating the tally. The host
      ///   (<c>AppreciationBehavior.RunBackfillIfNeeded</c>) is what enforces this: it scans only on a ledger
      ///   that has NEVER been scanned, while consolidation is idempotent and may run on every bump.
      /// </summary>
      public const int MigrationVersion = 2;

      /// <summary>
      ///   Sources whose kindness is inherently visible to the player at formation, because the player
      ///   performed (or plainly witnessed) the deed themselves: a gift handed over, gold given, a
      ///   ransom paid, a quest reward claimed, a kindness shared in a scene. The rest
      ///   (troops/influence spent behind the scenes, a family's private divorce gratitude) default to
      ///   HIDDEN: felt, but not yet voiced by the NPC — a design judgment call, not a discovered fact
      ///   (flag for review).
      /// </summary>
      private static readonly HashSet<AppreciationSource> KnownAtFormationSources = new() {
         AppreciationSource.GiftReceived,
         AppreciationSource.GoldReceived,
         AppreciationSource.RansomedOrFreed,
         AppreciationSource.QuestDoneForThem,
         AppreciationSource.KindnessInScene
      };

      /// <summary>
      ///   Every source's marker key, case-insensitive. Listing a marker here does NOT imply a real
      ///   engine-side detector exists for it today: <see cref="AppreciationBackfillRules" /> only
      ///   produces "kindness_in_scene" and "quest_done_for_them" from an actual save today (the two
      ///   sources with an unambiguous existing marker); the rest are live-plant-only for now, exactly
      ///   mirroring how GrudgeMigration.KnownMarkers lists SchemeWarned/SchemeAbetted even though
      ///   GrudgeBackfillRules never maps them.
      /// </summary>
      private static readonly Dictionary<string, AppreciationSource> KnownMarkers =
         new(StringComparer.OrdinalIgnoreCase) {
            ["gift_received"] = AppreciationSource.GiftReceived,
            ["gold_received"] = AppreciationSource.GoldReceived,
            ["ransomed_or_freed"] = AppreciationSource.RansomedOrFreed,
            ["quest_done_for_them"] = AppreciationSource.QuestDoneForThem,
            ["troops_or_influence_spent"] = AppreciationSource.TroopsOrInfluenceSpentForThem,
            ["kindness_in_scene"] = AppreciationSource.KindnessInScene,
            ["divorce_gratitude"] = AppreciationSource.DivorceGratitude
         };

      /// <summary>
      ///   Synthesizes appreciations from backfill candidates. NO DOUBLE-COUNTING: this method never
      ///   applies, replays, or refunds any relation delta. IDEMPOTENT: feed a prior result's
      ///   <see cref="AppreciationMigrationResult.Appreciations" /> back in as
      ///   <paramref name="existingAppreciations" /> and a second pass over the SAME candidates yields no
      ///   new appreciations (matched by the same holder+source+day composite key used as
      ///   <see cref="Appreciation.Id" />). VERSIONED: once <paramref name="alreadyMigratedStamp" /> is at
      ///   or beyond <see cref="MigrationVersion" />, the pass is a pure no-op.
      /// </summary>
      public static AppreciationMigrationResult SynthesizeAppreciations(
         IEnumerable<MigrationCandidate> candidates,
         IEnumerable<Appreciation> existingAppreciations,
         int alreadyMigratedStamp)
      {
         var result = new AppreciationMigrationResult {MigrationVersion = MigrationVersion};

         if (alreadyMigratedStamp >= MigrationVersion)
         {
            result.WasNoOp = true;

            return result;
         }

         var existingIds = new HashSet<string>(StringComparer.Ordinal);
         if (existingAppreciations != null)
            foreach (Appreciation existing in existingAppreciations)
               if (existing != null)
                  existingIds.Add(BuildId(existing.HolderId, existing.Source, existing.EventDay));

         if (candidates == null) return result;

         foreach (MigrationCandidate candidate in candidates)
         {
            if (candidate == null) continue;

            if (string.IsNullOrEmpty(candidate.Marker) || !KnownMarkers.TryGetValue(candidate.Marker.Trim(), out AppreciationSource source))
            {
               result.Skipped.Add(candidate);

               continue;
            }

            var facts = new AppreciationCandidateFacts {
               Source = source,
               Magnitude = candidate.Magnitude,
               IsAttributable = candidate.IsAttributable,
               Traits = candidate.Traits
            };

            if (!AppreciationPolicy.ShouldForm(facts))
            {
               result.Skipped.Add(candidate);

               continue;
            }

            string id = BuildId(candidate.HolderId, source, candidate.EventDay);
            if (existingIds.Contains(id))
            {
               result.Skipped.Add(candidate);

               continue;
            }

            var appreciation = new Appreciation {
               Id = id,
               HolderId = candidate.HolderId,
               TargetId = candidate.TargetId,
               Source = source,
               CauseSummary = string.IsNullOrWhiteSpace(candidate.Summary)
                  ? DefaultSummaryFor(source)
                  : candidate.Summary,
               EventDay = candidate.EventDay,
               Magnitude = candidate.Magnitude,
               KnownToPlayer = KnownAtFormationSources.Contains(source)
            };

            result.Appreciations.Add(appreciation);

            // Guards duplicate candidates within THIS same batch too (e.g. a bad double-scan upstream).
            existingIds.Add(id);
         }

         return result;
      }

      /// <summary>
      ///   Folds twin records into one counted appreciation, for ledgers saved before
      ///   <see cref="Appreciation.Occurrences" /> existed. Two records are twins when the same holder felt
      ///   the same SOURCE for the same CAUSE text (case and surrounding space ignored): the only dedup the
      ///   ledger ever had was same-holder-same-source-same-DAY, so a kindness repeated on three different
      ///   days became three byte-identical entries, which then filled the entire
      ///   <see cref="AppreciationPolicy.PerNpcCap" /> and told the LLM the same fact three times over.
      ///   A different cause text is a genuinely different memory and is never merged.
      ///   The survivor is the one with the NEWEST <see cref="Appreciation.EventDay" /> (the warmth is felt
      ///   from the latest occasion), keeping the strongest magnitude, known to the player if ANY of them
      ///   was, and carrying the total count. Nothing is lost: the tally is what the merged records were.
      ///   IDEMPOTENT: a second pass over its own output changes nothing, since every group is then a single
      ///   record whose count is already correct. Order is preserved (first appearance wins), so a
      ///   consolidated ledger stays stable across saves.
      /// </summary>
      public static List<Appreciation> ConsolidateRepeats(IEnumerable<Appreciation> appreciations)
      {
         var merged = new List<Appreciation>();

         if (appreciations == null) return merged;

         var byKey = new Dictionary<string, Appreciation>(StringComparer.OrdinalIgnoreCase);

         foreach (Appreciation a in appreciations)
         {
            if (a == null) continue;

            string key = $"{a.HolderId}|{(int) a.Source}|{(a.CauseSummary ?? string.Empty).Trim()}";

            if (!byKey.TryGetValue(key, out Appreciation kept))
            {
               byKey[key] = a;
               merged.Add(a);

               continue;
            }

            kept.Occurrences = AppreciationPolicy.OccurrenceCount(kept) + AppreciationPolicy.OccurrenceCount(a);
            if (a.Magnitude > kept.Magnitude) kept.Magnitude = a.Magnitude;
            if (a.EventDay > kept.EventDay) kept.EventDay = a.EventDay;
            if (a.KnownToPlayer) kept.KnownToPlayer = true;
         }

         return merged;
      }

      #region private

      /// <summary>
      ///   The deterministic composite key doubling as <see cref="Appreciation.Id" />: same holder, same
      ///   source, same event day always resolves to the SAME id, which is exactly the idempotence and
      ///   dedup key this migration relies on.
      /// </summary>
      private static string BuildId(string holderId, AppreciationSource source, int eventDay)
         => $"{holderId}|{(int) source}|{eventDay}";

      private static string DefaultSummaryFor(AppreciationSource source) => "a kindness from before, its details lost to time";

      #endregion
   }
}