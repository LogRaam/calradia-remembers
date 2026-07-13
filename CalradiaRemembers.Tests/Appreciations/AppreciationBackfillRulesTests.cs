// Code written by Gabriel Mailhot, 04/07/2026.
// AppreciationBackfillRules decides which persisted markers in an EXISTING save (before this pillar
// existed) can be safely read back as a felt kindness, feeding AppreciationMigration so old saves gain
// Kindnesses without a new game. Conservative by construction, mirroring GrudgeBackfillRules: recognizes
// ONLY the two exact markers documented in the production file's header (a Completed InformalQuest, a
// persisted Intimacy NotableEvent) and returns null for anything else, per the project's hard save-
// migration rule (never guess). Too eager here fabricates a kindness the player never earned; too strict
// silently drops a real one. These tests pin exactly which markers convert and which are skipped.

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

      // Stake: Rule 2 is the ONE confirmed, structured marker for this pillar (the production file's header:
      // "a real, structured, unambiguous marker"); anchoring on the wrong day would make an old completed
      // quest look freshly done, since AppreciationPolicy.EffectiveMagnitude decays from EventDay on every
      // read, overstating how warmly the NPC should currently feel.
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

      // Stake: SatisfiedOnDay is not always recorded; without this fallback the anchor day would default
      // to 0, mis-dating the deed to the dawn of time and leaving the migrated kindness fully decayed
      // (invisible) instead of correctly aged from when the quest was actually issued.
      [Test]
      public void GIVEN_a_completed_quest_with_no_satisfied_day_WHEN_mapping_THEN_it_anchors_on_the_issued_day()
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc_giver", StatusName = "Completed", IssuedOnDay = 50, SatisfiedOnDay = null, Description = ""};

         MigrationCandidate candidate = AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.EventDay.Should().Be(50);
      }

      // Stake: the conservative-by-construction rule (this file's header, mirroring GrudgeBackfillRules):
      // recognizing anything beyond the exact Intimacy ordinal would risk fabricating an appreciation for
      // an unrelated recorded event (here, a Conflict), a false kindness the player never actually earned.
      [Test]
      public void GIVEN_a_non_intimacy_event_WHEN_mapping_THEN_nothing_is_produced()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Conflict, Summary = "a skirmish", GameDay = 1};

         AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      // Stake: a missing HolderId means the engine-side scan could not attribute the event to any NPC;
      // forming a candidate anyway would hand AppreciationMigration a StringId-less kindness attached to
      // nobody, the exact half-match the hard save-migration rule says to skip rather than guess at.
      [Test]
      public void GIVEN_a_profile_event_with_no_holder_id_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new ProfileEventFacts {HolderId = null, EventTypeOrdinal = (int) NotableEventType.Intimacy, Summary = "x", GameDay = 1};

         AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      // Stake: same guard on the quest path; a missing GiverId means there is no NPC to hold the felt
      // kindness, so the candidate must be dropped rather than synthesized with a blank or guessed holder.
      [Test]
      public void GIVEN_a_quest_outcome_with_no_giver_id_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new QuestOutcomeFacts {GiverId = null, StatusName = "Completed", IssuedOnDay = 1};

         AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      // Stake: Rule 2 must fire ONLY on "Completed" (this file's header: "the ONLY marker converted").
      // Expired/Abandoned are precisely the statuses GrudgeBackfillRules reads as a FAILED quest on the
      // Grudges side; if this rule were sloppy, the same broken promise could plant a REWARD instead of
      // (or alongside) the grievance it should actually cause.
      [TestCase("Active")]
      [TestCase("Expired")]
      [TestCase("Abandoned")]
      [TestCase("Cancelled")]
      public void GIVEN_a_quest_status_other_than_completed_WHEN_mapping_THEN_nothing_is_produced(string status)
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc", StatusName = status, IssuedOnDay = 1};

         AppreciationBackfillRules.FromQuestOutcome(facts, PlayerId, PlainTraits()).Should().BeNull();
      }

      // Stake: below the truncation cap the exact wording must survive untouched, since this Summary text
      // doubles as the CauseSummary later injected into both the LLM prompt and the encyclopedia line; any
      // unintended mutation on the happy path would show the player a subtly altered account of the deed.
      [Test]
      public void GIVEN_a_short_summary_WHEN_mapping_THEN_it_is_carried_verbatim()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Intimacy, Summary = "short", GameDay = 1};

         AppreciationBackfillRules.FromProfileEvent(facts, PlayerId, PlainTraits()).Summary.Should().Be("short");
      }

      // ── Truncation ───────────────────────────────────────────────────────

      // Stake: MaxCauseLength keeps this "chronicler prose, not a data dump" (the constant's own doc); an
      // untruncated summary could bloat the LLM prompt budget and spill an unreadably long line into the
      // encyclopedia's Kindnesses section.
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

      // Stake: pins Rule 1's output shape. Note this file's own header flags Intimacy as PLACEHOLDER-GRADE,
      // not a confirmed-exclusive marker like Jealousy is for Grudges (Intimacy also feeds pregnancy and
      // jealousy downstream), so treating every persisted Intimacy event as a felt kindness is an
      // UNCONFIRMED heuristic the production code itself calls out for Gabriel's review before going live.
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