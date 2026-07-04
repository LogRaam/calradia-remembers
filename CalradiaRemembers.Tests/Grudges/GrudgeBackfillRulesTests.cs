// Code written by Gabriel Mailhot, 03/07/2026.

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
   [TestFixture]
   public sealed class GrudgeBackfillRulesTests
   {
      private const string PlayerId = "player";

      [Test]
      public void GIVEN_a_bastard_DISCOVERY_scandal_not_a_claim_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         // BastardBehavior.RecordScandal's discovery text ("is the natural child of…") must NOT be
         // misread as the coming-of-age claim — only the claim's own exact substring qualifies.
         var facts = new WorldEventFacts {
            ActorIds = new List<string> {"child_id"},
            TypeName = "Scandal",
            InvolvesPlayer = true,
            Summary = "Word spreads that Aldric is the natural child of Gabriel and Elira."
         };

         GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_betrayal_event_that_is_NOT_the_divorce_marker_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         // Betrayal (ordinal 7) is shared by several mechanics — an unrelated betrayal must not be
         // misread as a divorce grudge just because the ordinal matches.
         var facts = new ProfileEventFacts {
            HolderId = "npc",
            EventTypeOrdinal = (int) NotableEventType.Betrayal,
            Summary = "Betrayed a confidence shared in trust.",
            GameDay = 10
         };

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Should().BeNull();
      }

      // ── Rule 1: Jealousy (NotableEventType ordinal 11, exclusive marker) ────

      [Test]
      public void GIVEN_a_jealousy_ordinal_event_WHEN_mapping_THEN_a_jealousy_candidate_is_produced()
      {
         var facts = new ProfileEventFacts {
            HolderId = "npc_jealous",
            EventTypeOrdinal = (int) NotableEventType.Jealousy,
            Summary = "You keep me beneath the one you favour and seldom turn your eyes to me.",
            GameDay = 4200
         };

         MigrationCandidate candidate = GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 10, PlainTraits(), false);

         candidate.Should().NotBeNull();
         candidate.HolderId.Should().Be("npc_jealous");
         candidate.TargetId.Should().Be(PlayerId);
         candidate.Marker.Should().Be("jealousy");
         candidate.EventDay.Should().Be(4200);
         candidate.Magnitude.Should().Be(GrudgeBackfillRules.JealousyMagnitude);
         candidate.Summary.Should().Be(facts.Summary);
      }

      [Test]
      public void GIVEN_a_non_jealousy_non_betrayal_event_WHEN_mapping_THEN_nothing_is_produced()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Conflict, Summary = "a skirmish", GameDay = 1};

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_non_scandal_world_event_involving_the_player_WHEN_mapping_THEN_it_is_skipped()
      {
         var facts = new WorldEventFacts {ActorIds = new List<string> {"a"}, TypeName = "Battle", InvolvesPlayer = true, Summary = "presses a claim against their house"};

         GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_profile_event_with_no_holder_id_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new ProfileEventFacts {HolderId = null, EventTypeOrdinal = (int) NotableEventType.Jealousy, Summary = "x", GameDay = 1};

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Should().BeNull();
      }

      [TestCase("Active")]
      [TestCase("Completed")]
      [TestCase("Cancelled")]
      public void GIVEN_a_quest_status_other_than_expired_or_abandoned_WHEN_mapping_THEN_nothing_is_produced(string status)
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc", StatusName = status, IssuedOnDay = 1};

         GrudgeBackfillRules.FromQuestOutcome(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_scandal_event_that_does_not_involve_the_player_WHEN_mapping_THEN_it_is_skipped()
      {
         var facts = new WorldEventFacts {
            ActorIds = new List<string> {"a"},
            TypeName = "Scandal",
            InvolvesPlayer = false,
            Summary = "presses a claim against their house"
         };

         GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_short_summary_WHEN_mapping_THEN_it_is_carried_verbatim()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Jealousy, Summary = "short", GameDay = 1};

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Summary.Should().Be("short");
      }

      // ── Truncation ───────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_summary_longer_than_the_cap_WHEN_mapping_THEN_the_cause_is_truncated()
      {
         var longSummary = new string('x', GrudgeBackfillRules.MaxCauseLength + 50);
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Jealousy, Summary = longSummary, GameDay = 1};

         MigrationCandidate candidate = GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false);

         candidate.Summary.Length.Should().BeLessThanOrEqualTo(GrudgeBackfillRules.MaxCauseLength + 3);
         candidate.Summary.Should().EndWith("...");
      }

      [Test]
      public void GIVEN_an_abandoned_quest_with_no_deadline_WHEN_mapping_THEN_it_anchors_on_the_issued_day()
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc_giver", StatusName = "Abandoned", IssuedOnDay = 50, DeadlineDay = null, Description = ""};

         MigrationCandidate candidate = GrudgeBackfillRules.FromQuestOutcome(facts, PlayerId, 0, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.EventDay.Should().Be(50);
      }

      // ── Rule 2: FailedQuest (structured InformalQuest status, the ONLY marker) ──

      [Test]
      public void GIVEN_an_expired_quest_WHEN_mapping_THEN_a_failed_quest_candidate_anchors_on_the_deadline_day()
      {
         var facts = new QuestOutcomeFacts {
            GiverId = "npc_giver",
            StatusName = "Expired",
            IssuedOnDay = 100,
            DeadlineDay = 130,
            Description = "clear bandits near Aldrok"
         };

         MigrationCandidate candidate = GrudgeBackfillRules.FromQuestOutcome(facts, PlayerId, 5, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.Marker.Should().Be("failed_quest");
         candidate.HolderId.Should().Be("npc_giver");
         candidate.EventDay.Should().Be(130, "the deadline day is the best-known anchor for an expired quest");
         candidate.Magnitude.Should().Be(GrudgeBackfillRules.FailedQuestMagnitude);
         candidate.Summary.Should().Contain("clear bandits near Aldrok");
      }

      [Test]
      public void GIVEN_no_actors_WHEN_mapping_a_bastard_claim_THEN_it_is_skipped()
      {
         var facts = new WorldEventFacts {ActorIds = new List<string>(), TypeName = "Scandal", InvolvesPlayer = true, Summary = "presses a claim against their house"};

         GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      [Test]
      public void GIVEN_only_half_the_divorce_marker_text_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new ProfileEventFacts {
            HolderId = "npc",
            EventTypeOrdinal = (int) NotableEventType.Betrayal,
            Summary = "Was set aside by Roland, for reasons unrelated to the player.",
            GameDay = 10
         };

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Should().BeNull();
      }

      // ── Rule 4: BastardClaim (Scandal WorldEvent + InvolvesPlayer + the EXACT substring) ──

      [Test]
      public void GIVEN_the_exact_bastard_claim_marker_text_WHEN_mapping_THEN_a_bastard_claim_candidate_is_produced()
      {
         // The literal shape BastardBehavior.RecordClaim writes:
         // $"{bastard.Name}, natural-born child of {parent}, has come of age and presses a claim against their house."
         var facts = new WorldEventFacts {
            ActorIds = new List<string> {"bastard_id", "player"},
            Day = 5000,
            TypeName = "Scandal",
            InvolvesPlayer = true,
            Summary = "Aldric, natural-born child of Gabriel, has come of age and presses a claim against their house."
         };

         MigrationCandidate candidate = GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, -40, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.Marker.Should().Be("bastard_claim");
         candidate.HolderId.Should().Be("bastard_id", "the claimant is ActorIds[0]");
         candidate.TargetId.Should().Be(PlayerId);
         candidate.EventDay.Should().Be(5000);
         candidate.Magnitude.Should().Be(GrudgeBackfillRules.BastardClaimMagnitude);
      }

      // ── Rule 3: divorce set-aside (Betrayal ordinal 7 + the EXACT text pair) ──

      [Test]
      public void GIVEN_the_exact_divorce_set_aside_marker_text_WHEN_mapping_THEN_a_divorce_candidate_is_produced()
      {
         // The literal shape DivorceProcessBehavior.ApplySuccessConsequences writes on the spouse profile:
         // $"Was set aside by {npc.Name}, the marriage dissolved at the player's urging."
         var facts = new ProfileEventFacts {
            HolderId = "npc_exspouse",
            EventTypeOrdinal = (int) NotableEventType.Betrayal,
            Summary = "Was set aside by Roland, the marriage dissolved at the player's urging.",
            GameDay = 900
         };

         MigrationCandidate candidate = GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, -20, PlainTraits(), false);

         candidate.Should().NotBeNull();
         candidate.Marker.Should().Be("divorce");
         candidate.HolderId.Should().Be("npc_exspouse");
         candidate.TargetId.Should().Be(PlayerId);
         candidate.EventDay.Should().Be(900);
         candidate.Magnitude.Should().Be(GrudgeBackfillRules.DivorceMagnitude);
      }

      #region private

      private static TraitFacts PlainTraits() => new();

      #endregion
   }
}