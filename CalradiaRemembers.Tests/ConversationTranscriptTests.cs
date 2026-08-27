// Code written by Gabriel Mailhot, 18/07/2026.
// The transcript is the conversation's single source of truth, and both things read from it are
// load-bearing: every participant's ChatSession is rebuilt from it before every LLM call, and the memory
// summarizer is handed a rendering of it to write what the NPC will remember FOREVER. It was a raw List of
// a class nested privately inside ChatViewModel, so none of it could be tested (the mod is net472, this
// project is net10) even though not one line of it touches the engine.
//
// The composition rules themselves are pinned in SessionHistoryComposerTests; what is pinned here is the
// transcript's own invariants and its plain-text rendering.

#region

using System;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ConversationTranscriptTests
   {
      // The ordinary case, so the guards below cannot be satisfied by recording nothing at all.
      [Test]
      public void GIVEN_a_spoken_line_WHEN_recording_THEN_it_is_kept_attributed_as_given()
      {
         var transcript = new ConversationTranscript();

         transcript.Record(false, "Derthert", "lord_derthert", "You may rise.");

         transcript.Count.Should().Be(1);
         transcript.Lines[0].IsPlayer.Should().BeFalse();
         transcript.Lines[0].SpeakerName.Should().Be("Derthert");
         transcript.Lines[0].SpeakerHeroId.Should().Be("lord_derthert");
         transcript.Lines[0].Text.Should().Be("You may rise.");
         transcript.Lines[0].IsNarration.Should().BeFalse();
      }

      // The invariant that belongs to the transcript rather than to each caller. A blank line is not merely
      // useless: it would take a slot in every prompt rebuilt afterwards, and an empty assistant turn teaches
      // the model that saying nothing is a valid reply. A narration-only turn legitimately produces an empty
      // spoken line, so this path is reached in normal play, not only on a malformed response.
      [TestCase("")]
      [TestCase("   ")]
      [TestCase(null)]
      public void GIVEN_nothing_was_actually_said_WHEN_recording_THEN_no_line_is_kept(string text)
      {
         var transcript = new ConversationTranscript();

         transcript.Record(false, "Derthert", "lord_derthert", text);

         transcript.Count.Should().Be(0);
      }

      // A narration entry must stay distinguishable from a spoken one, because that flag is what decides
      // whether the speaker's run is replayed rebuilt in the tagged [DIALOGUE]/[NARRATION] shape (audit C4).
      [Test]
      public void GIVEN_a_narration_line_WHEN_recording_THEN_it_is_marked_as_narration()
      {
         var transcript = new ConversationTranscript();

         transcript.Record(false, "Derthert", "lord_derthert", "He circles the prisoner slowly.", true);

         transcript.Lines[0].IsNarration.Should().BeTrue();
      }

      // What the memory summarizer is handed. Every line is attributed by name so the summary it writes can
      // say who did what; an unattributed dump would make the NPC remember someone else's words as their own.
      [Test]
      public void GIVEN_an_exchange_WHEN_rendering_to_text_THEN_every_line_is_attributed_by_name()
      {
         string nl = Environment.NewLine;

         Exchange().ToPlainText("Gabriel").Should()
                   .Be($"Gabriel: Are you with me?{nl}Derthert: I am.{nl}Olaf: And I.{nl}");
      }

      // The player's label is a PARAMETER because the same rendering serves a captive scene ("Prisoner") and
      // an ordinary talk (their own name). Hard-coding either one would have the summarizer write the wrong
      // register into a permanent memory.
      [Test]
      public void GIVEN_a_captive_scene_label_WHEN_rendering_to_text_THEN_the_players_lines_carry_it()
      {
         Exchange().ToPlainText("Prisoner").Should().Contain("Prisoner: Are you with me?");
      }

      // Fail safe: a conversation closed before anyone spoke still asks for its summary. Empty text is the
      // signal the summarizer needs; a stray newline would read as a transcript with one blank exchange.
      [Test]
      public void GIVEN_nothing_was_said_WHEN_rendering_to_text_THEN_the_result_is_empty()
      {
         new ConversationTranscript().ToPlainText("Gabriel").Should().BeEmpty();
      }

      // The transcript answers each participant with THEIR view of the conversation: their own lines as their
      // assistant turns, everyone else's attributed. Pinned here at the seam the controller actually calls,
      // so the two halves cannot drift apart (the composer's own rules are pinned in its fixture).
      [Test]
      public void GIVEN_an_exchange_WHEN_composing_for_one_speaker_THEN_they_receive_their_own_view_of_it()
      {
         var turns = Exchange().ComposeSessionFor("lord_derthert");

         turns.Should().HaveCount(3);
         turns[0].Role.Should().Be(SessionTurnRole.Player);
         turns[1].Role.Should().Be(SessionTurnRole.Speaker); // his own line
         turns[2].Role.Should().Be(SessionTurnRole.Witness); // Olaf's, attributed
      }

      // With nothing folded, the transcript behaves exactly as before, every line replayed raw, so the running
      // compression is inert for the short conversations that are the overwhelming majority.
      [Test]
      public void GIVEN_no_summary_WHEN_composing_THEN_the_whole_raw_history_is_returned_unchanged()
      {
         var turns = Exchange().ComposeSessionFor("lord_derthert");

         turns.Should().HaveCount(3);
         turns.Should().NotContain(t => t.SpeakerName == ConversationTranscript.SummaryLabel);
      }

      // STAKE: the coherence guarantee. Once the older half of a long talk is folded, a turn must receive the
      // compressed recap FIRST (as attributed context) and then ONLY the recent raw lines, so it stays coherent
      // with the whole conversation without replaying it. The folded lines must not reappear raw.
      [Test]
      public void GIVEN_a_folded_summary_WHEN_composing_THEN_the_recap_leads_and_only_recent_lines_follow_raw()
      {
         var t = new ConversationTranscript();
         t.Record(true, "You", "player", "one");
         t.Record(false, "Derthert", "lord_derthert", "two");
         t.Record(false, "Derthert", "lord_derthert", "three");
         t.Record(true, "You", "player", "four");
         t.Record(false, "Derthert", "lord_derthert", "five");

         t.ApplySummary("They spoke of the war.", 3); // lines [0,3) folded away

         var turns = t.ComposeSessionFor("lord_derthert");

         turns.Should().HaveCount(3);
         turns[0].Role.Should().Be(SessionTurnRole.Witness);
         turns[0].SpeakerName.Should().Be(ConversationTranscript.SummaryLabel);
         turns[0].Text.Should().Be("They spoke of the war.");
         turns.Should().NotContain(x => x.Text == "two" || x.Text == "three"); // folded, never raw again
      }

      // STAKE: the fold marker must never go backwards or past the recorded lines, or a line could fall into the
      // gap between what the summary covered and where the raw replay resumes, and be silently lost, which is
      // the exact incoherence the feature exists to prevent.
      [Test]
      public void GIVEN_a_backward_or_oversized_marker_WHEN_applying_a_summary_THEN_it_is_clamped()
      {
         var t = Exchange(); // 3 lines

         t.ApplySummary("recap", 2);
         t.SummarizedThrough.Should().Be(2);

         t.ApplySummary("recap-back", 1); // backward: ignored, never uncovers a folded line
         t.SummarizedThrough.Should().Be(2);

         t.ApplySummary("recap-over", 99); // past the end: clamped to the line count
         t.SummarizedThrough.Should().Be(3);
      }

      // STAKE: a duel interrupts a conversation and the post-duel scene opens a FRESH chat; if seeding did not
      // carry every prior line across, the NPC would resume with no memory of what was just said and the whole
      // "resume where the blades cut in" promise would break. Lines must arrive oldest-first, attribution intact.
      [Test]
      public void GIVEN_a_prior_conversation_WHEN_seeding_a_fresh_transcript_THEN_all_its_lines_carry_across_in_order()
      {
         var resumed = new ConversationTranscript();

         resumed.SeedFrom(Exchange());

         resumed.Count.Should().Be(3);
         resumed.Lines[0].Text.Should().Be("Are you with me?");
         resumed.Lines[0].IsPlayer.Should().BeTrue();
         resumed.Lines[1].SpeakerName.Should().Be("Derthert");
         resumed.Lines[2].SpeakerName.Should().Be("Olaf");
      }

      // STAKE: a long conversation folds its older half into a running recap; if seeding dropped that recap, a
      // resumed chat would lose everything before the fold, which is exactly the coherence the fold exists to
      // keep. The recap and its marker must survive the carry.
      [Test]
      public void GIVEN_a_prior_conversation_that_had_folded_WHEN_seeding_THEN_the_recap_and_its_marker_carry_across()
      {
         var prior = Exchange();
         prior.ApplySummary("They spoke of the war.", 2);

         var resumed = new ConversationTranscript();
         resumed.SeedFrom(prior);

         resumed.RunningSummary.Should().Be("They spoke of the war.");
         resumed.SummarizedThrough.Should().Be(2);
      }

      // STAKE: the resumed transcript must serve the NPC their own view of the WHOLE prior exchange on the very
      // next call, or the post-duel opener would be spoken by someone with no idea what led to the fight. Pinned
      // at the compose seam the controller actually calls.
      [Test]
      public void GIVEN_a_seeded_transcript_WHEN_composing_for_a_participant_THEN_they_receive_the_prior_exchange()
      {
         var resumed = new ConversationTranscript();
         resumed.SeedFrom(Exchange());

         var turns = resumed.ComposeSessionFor("lord_derthert");

         turns.Should().HaveCount(3);
         turns[1].Role.Should().Be(SessionTurnRole.Speaker); // Derthert's own line, from the prior conversation
      }

      // A missing source must never throw: a duel armed from a path that had no transcript (a console-forced
      // duel) still opens the post-duel scene, just with nothing to resume.
      [Test]
      public void GIVEN_no_prior_conversation_WHEN_seeding_from_null_THEN_it_is_a_no_op()
      {
         var resumed = new ConversationTranscript();

         resumed.SeedFrom(null);

         resumed.Count.Should().Be(0);
      }

      #region private

      private static ConversationTranscript Exchange()
      {
         var transcript = new ConversationTranscript();
         transcript.Record(true, "You", "player", "Are you with me?");
         transcript.Record(false, "Derthert", "lord_derthert", "I am.");
         transcript.Record(false, "Olaf", "lord_olaf", "And I.");

         return transcript;
      }

      #endregion
   }
}
