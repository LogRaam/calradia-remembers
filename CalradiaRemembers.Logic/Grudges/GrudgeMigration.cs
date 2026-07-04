// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: THE SAVE MIGRATION (Gabriel, 2026-07-03, a hard requirement). A
// one-shot, idempotent, conservative backfill that synthesizes Grudge records from the clear Tier-1
// markers a save already carries (jealousy grievances, scheme treachery, breached-pledge summaries,
// divorce set-aside, bastard claim/blackmail) — never an interpretation, never a replay of the relation
// hit that already happened. The engine-side scan of persisted NotableEvents into MigrationCandidate
// (recognizing real marker text) is a LATER increment; this layer only reasons over the candidates it
// is handed, so it stays pure and fully unit-tested.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   One backfill candidate: a persisted marker the engine-side scan recognized, plus every fact
    ///   <see cref="GrudgePolicy" /> needs to decide whether (and how) it becomes a <see cref="Grudge" />.
    /// </summary>
    public sealed class MigrationCandidate
    {
        /// <summary>Hero StringId of the NPC who would nurse the grudge.</summary>
        public string HolderId { get; set; }

        /// <summary>Hero StringId of who the grudge would be held against (usually the player).</summary>
        public string TargetId { get; set; }

        /// <summary>
        ///   The raw persisted marker key (e.g. "jealousy", "failed_quest") the engine-side scan read off
        ///   a NotableEvent. Matched case-insensitively against <see cref="GrudgeMigration" />'s known-marker
        ///   table; anything unrecognized is conservatively skipped rather than guessed at.
        /// </summary>
        public string Marker { get; set; }

        /// <summary>In-game day the original event occurred.</summary>
        public int EventDay { get; set; }

        /// <summary>The original event's own summary text, carried onto the synthesized grudge's <see cref="Grudge.CauseSummary" /> when present.</summary>
        public string Summary { get; set; }

        /// <summary>The holder's regard for the target AT MIGRATION TIME (live relation), feeding <see cref="GrudgePolicy.RigidityAt" />.</summary>
        public int RegardForTarget { get; set; }

        /// <summary>The holder's trait levels.</summary>
        public TraitFacts Traits { get; set; }

        /// <summary>True when the holder's romantic preferences amplify a Jealousy-source candidate.</summary>
        public bool PossessiveAmplifier { get; set; }

        /// <summary>The original event's own intensity/magnitude, 0..100, before any amplification.</summary>
        public int Magnitude { get; set; }

        /// <summary>Anti-noise guardrail passthrough. Defaults true: a marker that already exists in a save is, by definition, attributable to a real event.</summary>
        public bool IsAttributable { get; set; } = true;
    }

    /// <summary>
    ///   The outcome of one migration pass: the grudges newly synthesized, the candidates that were
    ///   conservatively skipped (unrecognized marker, failed the formation guardrail, or already
    ///   present) so the caller can log them, and whether the pass was a no-op because the ledger was
    ///   already stamped at or above <see cref="GrudgeMigration.MigrationVersion" />.
    /// </summary>
    public sealed class GrudgeMigrationResult
    {
        /// <summary>Newly synthesized grudges this pass. Empty (never null) on a no-op pass.</summary>
        public List<Grudge> Grudges { get; set; } = new List<Grudge>();

        /// <summary>Candidates that did NOT become a grudge, for caller-side logging: unrecognized marker, guardrail refusal, or duplicate.</summary>
        public List<MigrationCandidate> Skipped { get; set; } = new List<MigrationCandidate>();

        /// <summary>The migration version this result is stamped with.</summary>
        public int MigrationVersion { get; set; }

        /// <summary>True when the ledger was already migrated at or beyond this version: nothing was evaluated.</summary>
        public bool WasNoOp { get; set; }
    }

    /// <summary>
    ///   The pure migration engine. Conservative (unknown markers skipped), idempotent (re-running with
    ///   the same candidates and the previous result's grudges as "existing" yields no new grudges), and
    ///   additive (never replays or refunds a relation hit — see <see cref="SynthesizeGrudges" />).
    /// </summary>
    public static class GrudgeMigration
    {
        /// <summary>The current migration version. Bump when the backfill logic changes in a way that should re-run against already-migrated ledgers.</summary>
        public const int MigrationVersion = 1;

        /// <summary>
        ///   Tier-1 marker keys mapped to their unambiguous <see cref="GrudgeSource" />. Case-insensitive.
        ///   Anything not in this table is conservatively skipped rather than guessed at — the ROADMAP's
        ///   Tier 2/3 sources (delivered prisoner, captive abuse, sway, unblessed marriage, executed kin)
        ///   are NEW detections, not migration backfill, and deliberately absent here.
        /// </summary>
        private static readonly Dictionary<string, GrudgeSource> KnownMarkers =
            new Dictionary<string, GrudgeSource>(StringComparer.OrdinalIgnoreCase)
            {
                ["jealousy"] = GrudgeSource.Jealousy,
                ["scheme_warned"] = GrudgeSource.SchemeWarned,
                ["scheme_abetted"] = GrudgeSource.SchemeAbetted,
                ["failed_quest"] = GrudgeSource.FailedQuest,
                ["divorce"] = GrudgeSource.Divorce,
                ["bastard_claim"] = GrudgeSource.BastardClaim,
                ["bastard_blackmail"] = GrudgeSource.BastardBlackmail
            };

        /// <summary>
        ///   Sources whose cause is inherently public or aimed at the player, so a migrated grudge starts
        ///   KNOWN (a jealous-threat letter received, a quest the player themselves failed, a divorce
        ///   scandal, a bastard claim/demand). SchemeWarned/SchemeAbetted default to HIDDEN: the plotter
        ///   conceals their own grudge (ROADMAP's knowledge-gating), staying smooth until unmasked.
        /// </summary>
        private static readonly HashSet<GrudgeSource> KnownAtFormationSources = new HashSet<GrudgeSource>
        {
            GrudgeSource.Jealousy,
            GrudgeSource.FailedQuest,
            GrudgeSource.Divorce,
            GrudgeSource.BastardClaim,
            GrudgeSource.BastardBlackmail
        };

        /// <summary>
        ///   Synthesizes grudges from backfill candidates. NO DOUBLE-COUNTING: this method never applies,
        ///   replays, or refunds any relation delta — a synthesized <see cref="Grudge" /> only adds
        ///   RESOLVABILITY on top of a hit that already happened in the save being migrated. IDEMPOTENT:
        ///   feed a prior result's <see cref="GrudgeMigrationResult.Grudges" /> back in as
        ///   <paramref name="existingGrudges" /> and a second pass over the SAME candidates yields no new
        ///   grudges (matched by the same holder+source+day composite key used as <see cref="Grudge.Id" />).
        ///   VERSIONED: once <paramref name="alreadyMigratedStamp" /> is at or beyond
        ///   <see cref="MigrationVersion" />, the pass is a pure no-op.
        /// </summary>
        public static GrudgeMigrationResult SynthesizeGrudges(
            IEnumerable<MigrationCandidate> candidates,
            IEnumerable<Grudge> existingGrudges,
            int alreadyMigratedStamp)
        {
            var result = new GrudgeMigrationResult { MigrationVersion = MigrationVersion };

            if (alreadyMigratedStamp >= MigrationVersion)
            {
                result.WasNoOp = true;
                return result;
            }

            // Dedup by the RECOMPUTED holder+source+day composite, never by the existing grudge's stored
            // Id string: a grudge already planted LIVE for this exact event (GrudgeBehavior.TryPlant stamps
            // a random Guid, not this deterministic key) must still be recognized as already covering it —
            // otherwise a forced re-migration mid-session could double-count an event the live path already
            // turned into a grudge tonight. This keeps the migration's own no-double-counting guarantee
            // intact even against non-canonical ids.
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            if (existingGrudges != null)
                foreach (Grudge existing in existingGrudges)
                    if (existing != null)
                        existingIds.Add(BuildId(existing.HolderId, existing.Source, existing.EventDay));

            if (candidates == null) return result;

            foreach (MigrationCandidate candidate in candidates)
            {
                if (candidate == null) continue;

                if (string.IsNullOrEmpty(candidate.Marker) || !KnownMarkers.TryGetValue(candidate.Marker.Trim(), out GrudgeSource source))
                {
                    result.Skipped.Add(candidate);
                    continue;
                }

                var facts = new GrudgeCandidateFacts
                {
                    Source = source,
                    Magnitude = candidate.Magnitude,
                    IsAttributable = candidate.IsAttributable,
                    PossessiveAmplifier = candidate.PossessiveAmplifier,
                    Traits = candidate.Traits
                };

                if (!GrudgePolicy.ShouldForm(facts))
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

                int rigidity = GrudgePolicy.RigidityAt(candidate.RegardForTarget, facts);

                var grudge = new Grudge
                {
                    Id = id,
                    HolderId = candidate.HolderId,
                    TargetId = candidate.TargetId,
                    Source = source,
                    CauseSummary = string.IsNullOrWhiteSpace(candidate.Summary) ? DefaultSummaryFor(source) : candidate.Summary,
                    EventDay = candidate.EventDay,
                    Magnitude = GrudgePolicy.EffectiveFormationMagnitude(facts),
                    Rigidity = rigidity,
                    KnownToPlayer = KnownAtFormationSources.Contains(source),
                    ResolutionHintDiscovered = false,
                    ResolutionKind = GrudgePolicy.ResolutionKindFor(source, rigidity),
                    IsResolved = false,
                    ResolvedDay = -1
                };

                result.Grudges.Add(grudge);

                // Guards duplicate candidates within THIS same batch too (e.g. a bad double-scan upstream).
                existingIds.Add(id);
            }

            return result;
        }

        /// <summary>
        ///   The deterministic composite key doubling as <see cref="Grudge.Id" />: same holder, same
        ///   source, same event day always resolves to the SAME id, which is exactly the idempotence and
        ///   dedup key this migration relies on.
        /// </summary>
        private static string BuildId(string holderId, GrudgeSource source, int eventDay)
            => $"{holderId}|{(int) source}|{eventDay}";

        private static string DefaultSummaryFor(GrudgeSource source) => "a grievance from before, its details lost to time";
    }
}
