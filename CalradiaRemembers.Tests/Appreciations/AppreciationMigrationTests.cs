// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
   [TestFixture]
   public sealed class AppreciationMigrationTests
   {
      [Test]
      public void GIVEN_a_candidate_below_the_formation_floor_WHEN_migrating_THEN_it_is_skipped_by_the_same_anti_noise_guardrail()
      {
         MigrationCandidate candidate = Candidate(magnitude: AppreciationPolicy.MagnitudeFloor - 1);

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, null, 0);

         result.Appreciations.Should().BeEmpty();
         result.Skipped.Should().Contain(candidate);
      }

      [Test]
      public void GIVEN_a_candidate_marked_unattributable_WHEN_migrating_THEN_it_is_skipped_however_large()
      {
         MigrationCandidate candidate = Candidate(magnitude: 100);
         candidate.IsAttributable = false;

         AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, null, 0).Appreciations.Should().BeEmpty();
      }

      // ── Versioned no-op ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_ledger_already_stamped_at_the_current_version_WHEN_migrating_again_THEN_it_is_a_pure_no_op()
      {
         MigrationCandidate[] candidates = new[] {Candidate()};

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(candidates, null, AppreciationMigration.MigrationVersion);

         result.WasNoOp.Should().BeTrue();
         result.Appreciations.Should().BeEmpty();
         result.MigrationVersion.Should().Be(AppreciationMigration.MigrationVersion);
      }

      [Test]
      public void GIVEN_a_ledger_seeded_from_a_prior_pass_WHEN_migrating_the_same_candidates_again_THEN_no_new_appreciations_are_produced()
      {
         MigrationCandidate[] candidates = new[] {
            Candidate(eventDay: 10),
            Candidate("quest_done_for_them", "npc_b", eventDay: 20)
         };

         AppreciationMigrationResult first = AppreciationMigration.SynthesizeAppreciations(candidates, null, 0);
         first.Appreciations.Should().NotBeEmpty();

         AppreciationMigrationResult second = AppreciationMigration.SynthesizeAppreciations(candidates, first.Appreciations, 0);

         second.Appreciations.Should().BeEmpty("every candidate now matches an entry already in the ledger");
         second.Skipped.Should().HaveCount(candidates.Length);
      }

      [Test]
      public void GIVEN_a_ledger_stamped_beyond_the_current_version_WHEN_migrating_THEN_it_is_still_a_no_op()
      {
         AppreciationMigration.SynthesizeAppreciations(new[] {Candidate()}, null, AppreciationMigration.MigrationVersion + 1)
                              .WasNoOp.Should().BeTrue();
      }

      // ── No double-counting (documentation case) ─────────────────────────

      [Test]
      public void GIVEN_a_migration_pass_WHEN_it_runs_THEN_it_never_mutates_the_existing_appreciations_collection_it_was_handed()
      {
         var existing = new List<Appreciation>();

         AppreciationMigration.SynthesizeAppreciations(new[] {Candidate()}, existing, 0);

         existing.Should().BeEmpty("the migration is a pure read of its inputs; the caller owns persisting the result");
      }

      // ── The flagship use case: backfilling a save from the known positive markers ──

      [Test]
      public void GIVEN_a_save_carrying_every_known_marker_WHEN_migrating_for_the_first_time_THEN_an_appreciation_is_synthesized_for_each()
      {
         MigrationCandidate[] candidates = new[] {
            Candidate("gift_received", "npc_gifted"),
            Candidate("gold_received", "npc_paid"),
            Candidate("ransomed_or_freed", "npc_freed"),
            Candidate("quest_done_for_them", "npc_giver"),
            Candidate("troops_or_influence_spent", "npc_backed"),
            Candidate("kindness_in_scene", "npc_intimate"),
            Candidate("divorce_gratitude", "npc_relieved")
         };

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(candidates, null, 0);

         result.WasNoOp.Should().BeFalse();
         result.Appreciations.Should().HaveCount(candidates.Length);
         result.Skipped.Should().BeEmpty();

         result.Appreciations.Select(a => a.Source).Should().BeEquivalentTo(new[] {
            AppreciationSource.GiftReceived,
            AppreciationSource.GoldReceived,
            AppreciationSource.RansomedOrFreed,
            AppreciationSource.QuestDoneForThem,
            AppreciationSource.TroopsOrInfluenceSpentForThem,
            AppreciationSource.KindnessInScene,
            AppreciationSource.DivorceGratitude
         });
      }

      [Test]
      public void GIVEN_a_source_felt_behind_the_scenes_WHEN_migrating_THEN_it_defaults_unvoiced()
      {
         MigrationCandidate candidate = Candidate("divorce_gratitude");

         Appreciation appreciation = AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, null, 0).Appreciations.Single();

         appreciation.KnownToPlayer.Should().BeFalse("a family's private relief is not something the player has been told about yet");
      }

      [Test]
      public void GIVEN_a_synthesized_appreciation_WHEN_inspecting_it_THEN_it_carries_the_original_cause_and_day()
      {
         MigrationCandidate candidate = Candidate(eventDay: 4200, summary: "you freed my brother from a rival's dungeon");

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, null, 0);

         Appreciation appreciation = result.Appreciations.Single();
         appreciation.HolderId.Should().Be(candidate.HolderId);
         appreciation.TargetId.Should().Be(candidate.TargetId);
         appreciation.EventDay.Should().Be(4200);
         appreciation.CauseSummary.Should().Be(candidate.Summary);
      }

      [Test]
      public void GIVEN_an_appreciation_already_covering_the_same_event_but_carrying_a_NON_canonical_id_WHEN_migrating_THEN_it_is_still_recognized_as_a_duplicate()
      {
         MigrationCandidate candidate = Candidate(eventDay: 10);
         var livePlantedEquivalent = new Appreciation {
            Id = "some-random-guid-not-the-composite-key",
            HolderId = "npc_a",
            TargetId = "player",
            Source = AppreciationSource.KindnessInScene,
            EventDay = 10,
            Magnitude = 30
         };

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, new[] {livePlantedEquivalent}, 0);

         result.Appreciations.Should().BeEmpty("a live-planted appreciation for the same holder+source+day already covers this event");
         result.Skipped.Should().ContainSingle().Which.Should().BeSameAs(candidate);
      }

      [Test]
      public void GIVEN_an_empty_candidate_list_WHEN_migrating_THEN_nothing_is_produced()
      {
         AppreciationMigration.SynthesizeAppreciations(new List<MigrationCandidate>(), new List<Appreciation>(), 0).Appreciations.Should().BeEmpty();
      }

      // ── Unknown markers are conservatively skipped ──────────────────────

      [Test]
      public void GIVEN_an_unrecognized_marker_WHEN_migrating_THEN_it_is_skipped_not_guessed_at()
      {
         MigrationCandidate candidate = Candidate("some_future_marker_not_yet_understood");

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, null, 0);

         result.Appreciations.Should().BeEmpty();
         result.Skipped.Should().ContainSingle().Which.Should().BeSameAs(candidate);
      }

      // ── Empty input ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_no_candidates_at_all_WHEN_migrating_THEN_the_result_is_empty_but_not_a_no_op()
      {
         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(null, null, 0);

         result.Appreciations.Should().BeEmpty();
         result.Skipped.Should().BeEmpty();
         result.WasNoOp.Should().BeFalse();
      }

      // ── Existing-appreciation dedup within a mixed batch ─────────────────

      [Test]
      public void GIVEN_one_candidate_already_present_in_the_ledger_WHEN_migrating_a_mixed_batch_THEN_only_the_new_one_is_added()
      {
         MigrationCandidate alreadyMigrated = Candidate(eventDay: 10);
         MigrationCandidate brandNew = Candidate("quest_done_for_them", "npc_b", eventDay: 20);

         AppreciationMigrationResult seed = AppreciationMigration.SynthesizeAppreciations(new[] {alreadyMigrated}, null, 0);
         List<Appreciation> existingLedger = seed.Appreciations;

         AppreciationMigrationResult result = AppreciationMigration.SynthesizeAppreciations(new[] {alreadyMigrated, brandNew}, existingLedger, 0);

         result.Appreciations.Should().ContainSingle();
         result.Appreciations[0].HolderId.Should().Be("npc_b");
         result.Skipped.Should().ContainSingle(c => c == alreadyMigrated);
      }

      [Test]
      public void GIVEN_sources_the_player_visibly_performed_themselves_WHEN_migrating_THEN_they_are_known_to_the_player_at_formation()
      {
         MigrationCandidate candidate = Candidate("gift_received");

         Appreciation appreciation = AppreciationMigration.SynthesizeAppreciations(new[] {candidate}, null, 0).Appreciations.Single();

         appreciation.KnownToPlayer.Should().BeTrue("the player handed over the gift themselves, so they know it landed");
      }

      // ── Idempotence ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_the_same_candidates_and_no_ledger_WHEN_synthesizing_twice_independently_THEN_the_two_results_are_deeply_identical()
      {
         MigrationCandidate[] candidates = new[] {
            Candidate(eventDay: 10),
            Candidate("quest_done_for_them", "npc_b", eventDay: 20)
         };

         AppreciationMigrationResult first = AppreciationMigration.SynthesizeAppreciations(candidates, null, 0);
         AppreciationMigrationResult second = AppreciationMigration.SynthesizeAppreciations(candidates, null, 0);

         second.Appreciations.Should().BeEquivalentTo(first.Appreciations, "a deterministic composite id makes migration fully reproducible");
      }

      #region private

      private static MigrationCandidate Candidate(
         string marker = "kindness_in_scene",
         string holderId = "npc_a",
         string targetId = "player",
         int eventDay = 100,
         int magnitude = 30,
         TraitFacts? traits = null,
         string summary = "a testable cause")
         => new() {
            HolderId = holderId,
            TargetId = targetId,
            Marker = marker,
            EventDay = eventDay,
            Summary = summary,
            Traits = traits ?? new TraitFacts(),
            Magnitude = magnitude
         };

      #endregion
   }
}