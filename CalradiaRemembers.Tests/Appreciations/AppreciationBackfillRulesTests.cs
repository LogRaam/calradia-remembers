// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
   [TestFixture]
   public sealed class AppreciationBackfillRulesTests
   {
      private const string PlayerId = "player";

      // ── Rule 2: QuestDoneForThem (structured InformalQuest Completed status) ──

      [Test]
      public void GIVEN_a_completed_quest_WHEN_mapping_THEN_a_quest_done_for_them_candidate_anchors_on_the_satisfied_day()
      {
         var facts = new QuestOutcomeFacts {
            GiverId = "npc_giver",
            StatusName = "Completed",
            IssuedOnDay = 100,
            SatisfiedOnDay = 118,
            Description = "clear bandits near Aldrok"
         };

         MigrationCandidate candidate = AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.Marker.Should().Be("quest_done_for_them");
         candidate.HolderId.Should().Be("npc_giver");
         candidate.EventDay.Should().Be(118, "the day the deed was verified is the best-known anchor for a completed quest");
         candidate.Magnitude.Should().Be(AppreciationBackfillRules.QuestDoneForThemMagnitude);
         candidate.Summary.Should().Contain("clear bandits near Aldrok");
      }

      [Test]
      public void GIVEN_a_completed_quest_with_no_satisfied_day_WHEN_mapping_THEN_it_anchors_on_the_issued_day()
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc_giver", StatusName = "Completed", IssuedOnDay = 50, SatisfiedOnDay = null, Description = ""};

         MigrationCandidate candidate = AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.EventDay.Should().Be(50);
      }

      [Test]
      public void GIVEN_a_non_intimacy_event_WHEN_mapping_THEN_nothing_is_produced()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Conflict, Summary = "a skirmish", GameDay = 1};

         AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_profile_event_with_no_holder_id_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new ProfileEventFacts {HolderId = null, EventTypeOrdinal = (int) NotableEventType.Intimacy, Summary = "x", GameDay = 1};

         AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_quest_outcome_with_no_giver_id_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new QuestOutcomeFacts {GiverId = null, StatusName = "Completed", IssuedOnDay = 1};

         AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      [TestCase("Active")]
      [TestCase("Expired")]
      [TestCase("Abandoned")]
      [TestCase("Cancelled")]
      public void GIVEN_a_quest_status_other_than_completed_WHEN_mapping_THEN_nothing_is_produced(string status)
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc", StatusName = status, IssuedOnDay = 1};

         AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_short_summary_WHEN_mapping_THEN_it_is_carried_verbatim()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Intimacy, Summary = "short", GameDay = 1};

         AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits()).Summary.Should().Be("short");
      }

      // ── Truncation ───────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_summary_longer_than_the_cap_WHEN_mapping_THEN_the_cause_is_truncated()
      {
         var longSummary = new string('x', AppreciationBackfillRules.MaxCauseLength + 50);
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Intimacy, Summary = longSummary, GameDay = 1};

         MigrationCandidate candidate = AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits());

         candidate.Summary.Length.Should().BeLessThanOrEqualTo(AppreciationBackfillRules.MaxCauseLength + 3);
         candidate.Summary.Should().EndWith("...");
      }

      // ── Rule 1: KindnessInScene (Intimacy NotableEvent) ─────────────────

      [Test]
      public void GIVEN_an_intimacy_ordinal_event_WHEN_mapping_THEN_a_kindness_in_scene_candidate_is_produced()
      {
         var facts = new ProfileEventFacts {
            HolderId = "npc_intimate",
            EventTypeOrdinal = (int) NotableEventType.Intimacy,
            Summary = "A gentle, unhurried night together.",
            GameDay = 4200
         };

         MigrationCandidate candidate = AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.HolderId.Should().Be("npc_intimate");
         candidate.TargetId.Should().Be(PlayerId);
         candidate.Marker.Should().Be("kindness_in_scene");
         candidate.EventDay.Should().Be(4200);
         candidate.Magnitude.Should().Be(AppreciationBackfillRules.KindnessInSceneMagnitude);
         candidate.Summary.Should().Be(facts.Summary);
      }

      #region private

      private static TraitFacts PlainTraits() => new();

      #endregion
   }
}