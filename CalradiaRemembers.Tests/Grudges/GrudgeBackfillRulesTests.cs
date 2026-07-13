// Code written by Gabriel Mailhot, 03/07/2026.
// These rules turn markers ALREADY sitting in existing saves (a jealousy NotableEvent, a terminal
// InformalQuest status, the divorce set-aside Betrayal text, the bastard-claim Scandal WorldEvent) into
// backfilled Grudges, so players upgrading to the Grudges pillar do not silently lose every grievance that
// predates it. GrudgeMigration.SynthesizeGrudges consumes the MigrationCandidate each method here emits; a
// wrong Marker/HolderId/TargetId/EventDay fabricates a grudge that never happened or corrupts which NPC
// nurses it. The rule is deliberately CONSERVATIVE (the ROADMAP's hard save-migration requirement): every
// method recognizes ONLY its own exact marker and returns null on anything else, including a half-match,
// rather than guess.

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

      // Get this gate wrong and every third party who merely LEARNS a bastard exists (gossip, a rumour, the
      // discovery itself) would wrongly synthesize a bastard_claim grudge against the player, even though no
      // claim was ever pressed. Only BastardBehavior.RecordClaim's own exact wording qualifies.
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

      // Betrayal ordinal 7 is shared by several mechanics (scheme backlash, other betrayals), not unique to
      // divorce. Without the exact-text gate, any unrelated betrayal sitting on that ordinal would be
      // misread as a Divorce-source grudge against the player for a marriage that was never even theirs.
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

      // Pins the exact field mapping GrudgeMigration.SynthesizeGrudges depends on: Marker must be the literal
      // string "jealousy" (GrudgeMigration.KnownMarkers is a lookup table, so any typo here silently drops
      // every jealousy grudge in every old save through the "unrecognized marker, skip" path).
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

      // The conservative default: any NotableEventType other than the two recognized ordinals (Jealousy,
      // Betrayal) must yield null, not an invented grudge for an event this rule was never told to migrate.
      [Test]
      public void GIVEN_a_non_jealousy_non_betrayal_event_WHEN_mapping_THEN_nothing_is_produced()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Conflict, Summary = "a skirmish", GameDay = 1};

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Should().BeNull();
      }

      // The TypeName gate: only a "Scandal" WorldEvent is ever considered for a bastard claim. A Battle event
      // involving the player is not a claim pressed against their house, however InvolvesPlayer reads.
      [Test]
      public void GIVEN_a_non_scandal_world_event_involving_the_player_WHEN_mapping_THEN_it_is_skipped()
      {
         var facts = new WorldEventFacts {ActorIds = new List<string> {"a"}, TypeName = "Battle", InvolvesPlayer = true, Summary = "presses a claim against their house"};

         GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      // HolderId becomes the synthesized Grudge's holder key (who nurses it) and feeds GrudgeMigration's
      // dedup composite. A null/empty holder must never reach that far, or the migration would build a
      // malformed id for a grudge belonging to nobody.
      [Test]
      public void GIVEN_a_profile_event_with_no_holder_id_WHEN_mapping_THEN_it_is_conservatively_skipped()
      {
         var facts = new ProfileEventFacts {HolderId = null, EventTypeOrdinal = (int) NotableEventType.Jealousy, Summary = "x", GameDay = 1};

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Should().BeNull();
      }

      // FailedQuest (Rule 2) reads the STRUCTURED terminal status ONLY: Expired or Abandoned. A quest that is
      // still Active, or one the giver actually accepted as Completed, is not a broken word, so it must never
      // migrate into a "you gave your word" grudge against the player.
      [TestCase("Active")]
      [TestCase("Completed")]
      [TestCase("Cancelled")]
      public void GIVEN_a_quest_status_other_than_expired_or_abandoned_WHEN_mapping_THEN_nothing_is_produced(string status)
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc", StatusName = status, IssuedOnDay = 1};

         GrudgeBackfillRules.FromQuestOutcome(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      // TargetId is always the player (a bastard claim is pressed against the PLAYER's house); a Scandal that
      // does not involve the player is simply not the player's business, whatever its own summary text says.
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

      // The cause text becomes the migrated Grudge's narrative CauseSummary, the only thing the ROADMAP's
      // knowledge-gated Encyclopedia grievance panel ever shows the player. A short summary must survive
      // untouched, not get needlessly mangled by the truncation path below.
      [Test]
      public void GIVEN_a_short_summary_WHEN_mapping_THEN_it_is_carried_verbatim()
      {
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Jealousy, Summary = "short", GameDay = 1};

         GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false).Summary.Should().Be("short");
      }

      // ── Truncation ───────────────────────────────────────────────────────

      // MaxCauseLength keeps the migrated cause "chronicler prose, not a data dump" (the production header's
      // own framing): an unbounded raw engine summary would overrun the Encyclopedia grievance card instead
      // of reading like the short narrative line every other grudge source produces.
      [Test]
      public void GIVEN_a_summary_longer_than_the_cap_WHEN_mapping_THEN_the_cause_is_truncated()
      {
         var longSummary = new string('x', GrudgeBackfillRules.MaxCauseLength + 50);
         var facts = new ProfileEventFacts {HolderId = "npc", EventTypeOrdinal = (int) NotableEventType.Jealousy, Summary = longSummary, GameDay = 1};

         MigrationCandidate candidate = GrudgeBackfillRules.FromProfileEvent(facts, PlayerId, 0, PlainTraits(), false);

         candidate.Summary.Length.Should().BeLessThanOrEqualTo(GrudgeBackfillRules.MaxCauseLength + 3);
         candidate.Summary.Should().EndWith("...");
      }

      // EventDay is what GrudgePolicy.EffectiveMagnitude decays a migrated grudge from. An abandoned quest
      // with no DeadlineDay must fall back to IssuedOnDay, not silently default to day 0 (which would read as
      // an ancient event, already decayed to nothing the moment it migrates in).
      [Test]
      public void GIVEN_an_abandoned_quest_with_no_deadline_WHEN_mapping_THEN_it_anchors_on_the_issued_day()
      {
         var facts = new QuestOutcomeFacts {GiverId = "npc_giver", StatusName = "Abandoned", IssuedOnDay = 50, DeadlineDay = null, Description = ""};

         MigrationCandidate candidate = GrudgeBackfillRules.FromQuestOutcome(facts, PlayerId, 0, PlainTraits());

         candidate.Should().NotBeNull();
         candidate.EventDay.Should().Be(50);
      }

      // ── Rule 2: FailedQuest (structured InformalQuest status, the ONLY marker) ──

      // When a deadline WAS set, it is the better-known anchor than the issue day, so an Expired quest must
      // prefer it over IssuedOnDay (paired with the no-deadline test above, this fixes both branches of the
      // `?? IssuedOnDay` fallback the production code relies on).
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

      // ActorIds[0] is read as the claimant and becomes HolderId. An empty ActorIds list leaves no claimant to
      // attribute the grudge to, so this must be skipped rather than fall through to a null/default holder.
      [Test]
      public void GIVEN_no_actors_WHEN_mapping_a_bastard_claim_THEN_it_is_skipped()
      {
         var facts = new WorldEventFacts {ActorIds = new List<string>(), TypeName = "Scandal", InvolvesPlayer = true, Summary = "presses a claim against their house"};

         GrudgeBackfillRules.FromWorldEvent(facts, PlayerId, 0, PlainTraits()).Should().BeNull();
      }

      // The divorce marker requires BOTH exact substrings DivorceProcessBehavior writes, not just the
      // "set aside by" prefix: a spouse set aside for reasons unrelated to the player must not be misread as
      // a divorce the player caused, which is exactly the half-match this conservative-by-construction rule
      // guards against.
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

      // Per the ROADMAP's migration-reality inventory this is only PARTIALLY reliable (kept long-term, but
      // not compressor-protected like the divorce marker), so this test locks the exact contract with
      // BastardBehavior.RecordClaim's live wording. If that wording ever drifts, backfill silently stops
      // recognizing old claims and this pinned assertion is what would catch it.
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

      // Per the ROADMAP's migration-reality inventory, this marker is the MOST durable of the four (the
      // underlying NotableEvent is compressor-PROTECTED, unlike the others which can be pruned). This test
      // pins the exact contract with DivorceProcessBehavior.ApplySuccessConsequences' live wording, so a
      // future rewording there would be caught here instead of silently breaking every old save's backfill.
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