// Code written by Gabriel Mailhot, 03/07/2026.

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
   [TestFixture]
   public sealed class GrudgeMigrationTests
   {
      [Test]
      public void GIVEN_a_candidate_below_the_formation_floor_WHEN_migrating_THEN_it_is_skipped_by_the_same_anti_noise_guardrail()
      {
         MigrationCandidate candidate = Candidate(magnitude: GrudgePolicy.MagnitudeFloor - 1);

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(new[] {candidate}, null, 0);

         result.Grudges.Should().BeEmpty();
         result.Skipped.Should().Contain(candidate);
      }

      [Test]
      public void GIVEN_a_candidate_marked_unattributable_WHEN_migrating_THEN_it_is_skipped_however_large()
      {
         MigrationCandidate candidate = Candidate(magnitude: 100);
         candidate.IsAttributable = false;

         GrudgeMigration.SynthesizeGrudges(new[] {candidate}, null, 0).Grudges.Should().BeEmpty();
      }

      [Test]
      public void GIVEN_a_grudge_already_covering_the_same_event_but_carrying_a_NON_canonical_id_WHEN_migrating_THEN_it_is_still_recognized_as_a_duplicate()
      {
         // GrudgeBehavior.TryPlant (the LIVE path) stamps a random Guid, not the deterministic
         // holder+source+day composite this migration uses. A forced re-migration mid-session (after a
         // live grudge already covered tonight's event) must still dedup by RECOMPUTING that composite
         // from the existing grudge's own fields — never by trusting its stored Id string — or the same
         // event would double-count. Documents the fix for that exact gap.
         MigrationCandidate candidate = Candidate("jealousy", "npc_a", eventDay: 10);
         var livePlantedEquivalent = new Grudge {
            Id = "some-random-guid-not-the-composite-key",
            HolderId = "npc_a",
            TargetId = "player",
            Source = GrudgeSource.Jealousy,
            EventDay = 10,
            Magnitude = 40,
            Rigidity = 50
         };

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(new[] {candidate}, new[] {livePlantedEquivalent}, 0);

         result.Grudges.Should().BeEmpty("a live-planted grudge for the same holder+source+day already covers this event");
         result.Skipped.Should().ContainSingle().Which.Should().BeSameAs(candidate);
      }

      // ── Versioned no-op ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_ledger_already_stamped_at_the_current_version_WHEN_migrating_again_THEN_it_is_a_pure_no_op()
      {
         MigrationCandidate[] candidates = new[] {Candidate()};

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(candidates, null, GrudgeMigration.MigrationVersion);

         result.WasNoOp.Should().BeTrue();
         result.Grudges.Should().BeEmpty();
         result.MigrationVersion.Should().Be(GrudgeMigration.MigrationVersion);
      }

      [Test]
      public void GIVEN_a_ledger_seeded_from_a_prior_pass_WHEN_migrating_the_same_candidates_again_THEN_no_new_grudges_are_produced()
      {
         MigrationCandidate[] candidates = new[] {
            Candidate(eventDay: 10),
            Candidate("failed_quest", "npc_b", eventDay: 20)
         };

         GrudgeMigrationResult first = GrudgeMigration.SynthesizeGrudges(candidates, null, 0);
         first.Grudges.Should().NotBeEmpty();

         GrudgeMigrationResult second = GrudgeMigration.SynthesizeGrudges(candidates, first.Grudges, 0);

         second.Grudges.Should().BeEmpty("every candidate now matches an entry already in the ledger");
         second.Skipped.Should().HaveCount(candidates.Length);
      }

      [Test]
      public void GIVEN_a_ledger_stamped_beyond_the_current_version_WHEN_migrating_THEN_it_is_still_a_no_op()
      {
         GrudgeMigration.SynthesizeGrudges(new[] {Candidate()}, null, GrudgeMigration.MigrationVersion + 1)
                        .WasNoOp.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_migration_pass_WHEN_it_runs_THEN_it_never_mutates_the_existing_grudges_collection_it_was_handed()
      {
         var existing = new List<Grudge>();

         GrudgeMigration.SynthesizeGrudges(new[] {Candidate()}, existing, 0);

         existing.Should().BeEmpty("the migration is a pure read of its inputs; the caller owns persisting the result");
      }

      // ── The flagship use case: backfilling a save from the old ad hoc markers ──

      [Test]
      public void GIVEN_a_save_carrying_every_tier_one_marker_WHEN_migrating_for_the_first_time_THEN_a_grudge_is_synthesized_for_each()
      {
         MigrationCandidate[] candidates = new[] {
            Candidate("jealousy", "npc_jealous"),
            Candidate("scheme_warned", "npc_plotter"),
            Candidate("scheme_abetted", "npc_wronged"),
            Candidate("failed_quest", "npc_giver"),
            Candidate("divorce", "npc_exspouse"),
            Candidate("bastard_claim", "npc_claimant"),
            Candidate("bastard_blackmail", "npc_mother")
         };

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(candidates, null, 0);

         result.WasNoOp.Should().BeFalse();
         result.Grudges.Should().HaveCount(candidates.Length);
         result.Skipped.Should().BeEmpty();

         result.Grudges.Select(g => g.Source).Should().BeEquivalentTo(new[] {
            GrudgeSource.Jealousy,
            GrudgeSource.SchemeWarned,
            GrudgeSource.SchemeAbetted,
            GrudgeSource.FailedQuest,
            GrudgeSource.Divorce,
            GrudgeSource.BastardClaim,
            GrudgeSource.BastardBlackmail
         });
      }

      [Test]
      public void GIVEN_a_synthesized_grudge_WHEN_inspecting_it_THEN_it_carries_the_original_cause_day_and_an_unresolved_state()
      {
         MigrationCandidate candidate = Candidate(eventDay: 4200, summary: "the player made advances toward my spouse");

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(new[] {candidate}, null, 0);

         Grudge grudge = result.Grudges.Single();
         grudge.HolderId.Should().Be(candidate.HolderId);
         grudge.TargetId.Should().Be(candidate.TargetId);
         grudge.EventDay.Should().Be(4200);
         grudge.CauseSummary.Should().Be(candidate.Summary);
         grudge.IsResolved.Should().BeFalse();
         grudge.ResolvedDay.Should().Be(-1);
      }

      // ── No double-counting (documentation case) ─────────────────────────

      [Test]
      public void GIVEN_a_synthesized_grudge_WHEN_inspecting_its_shape_THEN_it_exposes_no_relation_delta_to_replay_or_refund()
      {
         // Documents the ROADMAP's hard rule: migration adds RESOLVABILITY on top of a hit that
         // already landed when the original event occurred. A Grudge has no relation-delta field at
         // all, so there is nothing here for a future engine-side consumer to mistakenly re-apply.
         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(new[] {Candidate()}, null, 0);

         Grudge grudge = result.Grudges.Single();
         typeof(Grudge).GetProperties()
                       .Select(p => p.Name)
                       .Should().NotContain(name => name.ToLowerInvariant().Contains("relation"));

         grudge.Magnitude.Should().BeGreaterThan(0, "magnitude drives THIS grudge's own decay/rigidity, never a relation number");
      }

      [Test]
      public void GIVEN_a_tier_two_marker_like_delivered_prisoner_WHEN_migrating_THEN_it_is_also_skipped()
      {
         // Tier 2/3 sources are NEW detections, not migration backfill: their markers do not exist in
         // old saves under a recognizable name, so the conservative table deliberately omits them.
         MigrationCandidate candidate = Candidate("delivered_prisoner");

         GrudgeMigration.SynthesizeGrudges(new[] {candidate}, null, 0).Grudges.Should().BeEmpty();
      }

      [Test]
      public void GIVEN_an_empty_candidate_list_WHEN_migrating_THEN_nothing_is_produced()
      {
         GrudgeMigration.SynthesizeGrudges(new List<MigrationCandidate>(), new List<Grudge>(), 0).Grudges.Should().BeEmpty();
      }

      // ── Unknown markers are conservatively skipped ──────────────────────

      [Test]
      public void GIVEN_an_unrecognized_marker_WHEN_migrating_THEN_it_is_skipped_not_guessed_at()
      {
         MigrationCandidate candidate = Candidate("some_future_marker_not_yet_understood");

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(new[] {candidate}, null, 0);

         result.Grudges.Should().BeEmpty();
         result.Skipped.Should().ContainSingle().Which.Should().BeSameAs(candidate);
      }

      // ── Empty input ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_no_candidates_at_all_WHEN_migrating_THEN_the_result_is_empty_but_not_a_no_op()
      {
         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(null, null, 0);

         result.Grudges.Should().BeEmpty();
         result.Skipped.Should().BeEmpty();
         result.WasNoOp.Should().BeFalse();
      }

      // ── Existing-grudge dedup within a mixed batch ───────────────────────

      [Test]
      public void GIVEN_one_candidate_already_present_in_the_ledger_WHEN_migrating_a_mixed_batch_THEN_only_the_new_one_is_added()
      {
         MigrationCandidate alreadyMigrated = Candidate(eventDay: 10);
         MigrationCandidate brandNew = Candidate("failed_quest", "npc_b", eventDay: 20);

         GrudgeMigrationResult seed = GrudgeMigration.SynthesizeGrudges(new[] {alreadyMigrated}, null, 0);
         List<Grudge> existingLedger = seed.Grudges;

         GrudgeMigrationResult result = GrudgeMigration.SynthesizeGrudges(new[] {alreadyMigrated, brandNew}, existingLedger, 0);

         result.Grudges.Should().ContainSingle();
         result.Grudges[0].HolderId.Should().Be("npc_b");
         result.Skipped.Should().ContainSingle(c => c == alreadyMigrated);
      }

      // ── Idempotence ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_the_same_candidates_and_no_ledger_WHEN_synthesizing_twice_independently_THEN_the_two_results_are_deeply_identical()
      {
         MigrationCandidate[] candidates = new[] {
            Candidate(eventDay: 10),
            Candidate("failed_quest", "npc_b", eventDay: 20)
         };

         GrudgeMigrationResult first = GrudgeMigration.SynthesizeGrudges(candidates, null, 0);
         GrudgeMigrationResult second = GrudgeMigration.SynthesizeGrudges(candidates, null, 0);

         second.Grudges.Should().BeEquivalentTo(first.Grudges, "a deterministic composite id makes migration fully reproducible");
      }

      #region private

      private static MigrationCandidate Candidate(
         string marker = "jealousy",
         string holderId = "npc_a",
         string targetId = "player",
         int eventDay = 100,
         int magnitude = 30,
         int regard = 0,
         TraitFacts? traits = null,
         string summary = "a testable cause")
         => new() {
            HolderId = holderId,
            TargetId = targetId,
            Marker = marker,
            EventDay = eventDay,
            Summary = summary,
            RegardForTarget = regard,
            Traits = traits ?? new TraitFacts(),
            Magnitude = magnitude
         };

      #endregion
   }
}