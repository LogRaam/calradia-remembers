// Code written by Gabriel Mailhot, 21/09/2026.
// What these pin: that the words of a conversation the player walked away from can be put back exactly as they
// were, and that a snapshot which cannot be read never costs anyone their save.
//
// The report, fkasad on Nexus, 20/09/2026, verbatim: "I've been playing for a couple of hours, negociating
// something with a npc... And we are close to a concensus. But I need a break, thus, I quit the chat, save and
// close the game and live my life. Then come back and reload. For the game, no time elapsed... I go back to the
// npc I was talking to before and reopen the chat box. He remembers talking to me but most of what we said is
// gone. I need to restart the negociation from the begining."
//
// He is describing the one thing CR deliberately never kept. The transcript is a process object ("a conversation
// never spans a save", ConversationTranscript's own doc), and everything durable about a conversation is the
// SUMMARY written when it closed. That is right for a conversation that ended. It is wrong for one that was
// merely interrupted, and the player pays for the difference by negotiating the same bargain twice.
//
// What makes this cheap is that the transcript is also the prompt: every participant's session is recomposed
// from it before every call (ConversationTranscript.ComposeSessionFor). Put the lines back and the NPC's own
// continuity comes back with them, with no second telling of the story to keep in step. So these tests are about
// the snapshot being FAITHFUL: an exchange that comes back altered would be worse than one that did not come
// back at all, because the player can read it.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class ConversationResumeSnapshotTests
   {
      private const string Lord = "lord_1_37";

      private static readonly ConversationResumeIdentity WithThatLordToday =
         new ConversationResumeIdentity {HeroId = Lord, GameDay = 412, AnchorId = "anchor-1"};

      // The negotiation fkasad describes, shortened: the point is that it is unfinished.
      private static ConversationTranscript TheNegotiation()
      {
         var transcript = new ConversationTranscript();
         transcript.Record(Player("You have held Varnovapol three winters and never once raised its walls."));
         transcript.Record(Lords("Rhagaea", "\"Walls are raised with silver I do not have.\""));
         transcript.Record(Player("Then take mine, and hold the place for my banner."));
         transcript.Record(Lords("Rhagaea", "\"You ask me to trade a fief for a purse. Say the figure.\""));

         return transcript;
      }

      private static TranscriptLine Player(string text)
         => new TranscriptLine {IsPlayer = true, SpeakerName = "You", Text = text};

      private static TranscriptLine Lords(string name, string text)
         => new TranscriptLine {IsPlayer = false, SpeakerHeroId = Lord, SpeakerName = name, Text = text};

      // THE WHOLE REQUEST, IN ONE ASSERTION. "I need to restart the negociation from the begining" is the fault;
      // coming back to the same four lines, in the same order, attributed to the same mouths, is the fix. Order
      // and attribution are not decoration: the transcript is rebuilt per speaker BY ID before every LLM call, so
      // a line that comes back under the wrong name hands the negotiation to the wrong person.
      [Test]
      public void GIVEN_the_negotiation_fkasad_walked_away_from_WHEN_it_is_captured_and_restored_THEN_every_word_comes_back_in_order()
      {
         ConversationResumeSnapshot kept = ConversationResumeSnapshot.Capture(TheNegotiation(), WithThatLordToday);

         ConversationTranscript resumed = kept.ToTranscript();

         resumed.Lines.Select(l => l.Text).Should().Equal(TheNegotiation().Lines.Select(l => l.Text));
         resumed.Lines.Select(l => l.IsPlayer).Should().Equal(true, false, true, false);
         resumed.Lines.Last().SpeakerHeroId.Should().Be(Lord);
      }

      // A LONG CONVERSATION IS ALREADY COMPRESSED, and the compression must travel with it. Once a conversation
      // grows, its older half is folded into a running summary and no longer replayed raw (Gabriel, 30/07/2026).
      // A snapshot that kept the lines but lost the fold would silently re-inflate a two-hour negotiation into
      // the prompt on the very turn the player resumes it, which is the most expensive turn to get wrong.
      [Test]
      public void GIVEN_a_conversation_already_folded_into_a_recap_WHEN_it_is_restored_THEN_the_recap_comes_back_with_it()
      {
         ConversationTranscript transcript = TheNegotiation();
         transcript.ApplySummary("They have been haggling over the price of Varnovapol.", 2);

         ConversationTranscript resumed = ConversationResumeSnapshot.Capture(transcript, WithThatLordToday).ToTranscript();

         resumed.RunningSummary.Should().Be("They have been haggling over the price of Varnovapol.");
         resumed.SummarizedThrough.Should().Be(2);
      }

      // A COUPLE OF HOURS OF ROLEPLAY IS WHAT HE SAID, so the cap is reached in real use, not in theory. What it
      // must never do is keep the OLDEST words: the player resumes where he left off, and the last thing said is
      // the thing he is answering. Dropping from the front also keeps the save small, which matters because this
      // blob rides in every campaign file.
      [Test]
      public void GIVEN_a_conversation_longer_than_the_cap_WHEN_it_is_captured_THEN_it_is_the_most_recent_words_that_are_kept()
      {
         var marathon = new ConversationTranscript();
         for (var i = 0; i < ConversationResumeSnapshot.MaxLinesKept + 40; i++) marathon.Record(Player($"line {i}"));

         ConversationResumeSnapshot kept = ConversationResumeSnapshot.Capture(marathon, WithThatLordToday);

         kept.Lines.Should().HaveCount(ConversationResumeSnapshot.MaxLinesKept);
         kept.Lines.Last().Text.Should().Be($"line {ConversationResumeSnapshot.MaxLinesKept + 39}");
         kept.Lines.First().Text.Should().Be("line 40");
      }

      // AND THE FOLD MARKER MOVES WITH THE LINES IT COUNTS. SummarizedThrough is an INDEX into the lines, so
      // dropping the oldest without shifting it would mark still-raw lines as already summarized: the player
      // would resume a conversation whose most recent exchange the NPC is told it has already been given a recap
      // of, and those words would reach the model neither raw nor folded.
      [Test]
      public void GIVEN_folded_lines_are_dropped_by_the_cap_WHEN_it_is_restored_THEN_the_fold_marker_still_points_at_the_right_line()
      {
         var marathon = new ConversationTranscript();
         for (var i = 0; i < ConversationResumeSnapshot.MaxLinesKept + 10; i++) marathon.Record(Player($"line {i}"));
         marathon.ApplySummary("The early haggling.", ConversationResumeSnapshot.MaxLinesKept);

         ConversationTranscript resumed = ConversationResumeSnapshot.Capture(marathon, WithThatLordToday).ToTranscript();

         resumed.SummarizedThrough.Should().Be(ConversationResumeSnapshot.MaxLinesKept - 10,
            "ten lines were dropped from the front, so everything after the fold marker shifts by ten");
         resumed.Lines[resumed.SummarizedThrough].Text.Should().Be($"line {ConversationResumeSnapshot.MaxLinesKept}",
            "the first line NOT covered by the recap must still be the first line the model is shown raw");
      }

      // WHO IT BELONGS TO HAS TO SURVIVE THE SAVE, not just the words. The hero id is what stops the exchange
      // being handed to the next person the player talks to, the day is what stops yesterday's words being
      // recited as though the player were still standing there, and the anchor is the memory event this
      // conversation ALREADY wrote when it first closed: without it the resumed sitting writes a second one and
      // the NPC remembers one afternoon as two.
      [Test]
      public void GIVEN_a_captured_conversation_WHEN_it_is_written_to_the_save_and_read_back_THEN_it_still_knows_whose_it_is()
      {
         string json = ConversationResumeSnapshot.Capture(TheNegotiation(), WithThatLordToday).Serialize();

         ConversationResumeSnapshot read = ConversationResumeSnapshot.Deserialize(json);

         read.HeroId.Should().Be(Lord);
         read.GameDay.Should().Be(412);
         read.AnchorId.Should().Be("anchor-1");
         read.Lines.Select(l => l.Text).Should().Equal(TheNegotiation().Lines.Select(l => l.Text));
      }

      // A BLOB THAT CANNOT BE READ MUST COST NOTHING. This rides in the campaign file, so it will one day be read
      // by a build that wrote it differently, or half-written by a crash. The save-migration rule is absolute
      // here: a conversation that fails to resume is a small disappointment, and a load that throws is the
      // player's campaign. Every unreadable shape degrades to "there is nothing to resume".
      [TestCase(null, TestName = "GIVEN_a_save_written_before_this_feature_WHEN_it_is_read_THEN_there_is_simply_nothing_to_resume")]
      [TestCase("", TestName = "GIVEN_an_empty_blob_WHEN_it_is_read_THEN_there_is_simply_nothing_to_resume")]
      [TestCase("{ this is not json", TestName = "GIVEN_a_blob_truncated_by_a_crash_WHEN_it_is_read_THEN_there_is_simply_nothing_to_resume")]
      [TestCase("[1,2,3]", TestName = "GIVEN_a_blob_of_the_wrong_shape_WHEN_it_is_read_THEN_there_is_simply_nothing_to_resume")]
      public void GIVEN_an_unreadable_capture_WHEN_it_is_read_THEN_nothing_is_resumed_and_nothing_throws(string blob)
      {
         ConversationResumeSnapshot read = null;

         FluentActions.Invoking(() => read = ConversationResumeSnapshot.Deserialize(blob)).Should().NotThrow();
         read.Should().BeNull();
      }

      // NOTHING WAS SAID, SO THERE IS NOTHING TO PUT BACK. A chat opened and closed without a word still runs the
      // capture, and an empty snapshot in the save would invite the next open to "resume" a blank exchange,
      // replaying zero bubbles while claiming continuity the player never had.
      [Test]
      public void GIVEN_a_chat_that_was_opened_and_closed_without_a_word_WHEN_it_is_captured_THEN_there_is_nothing_to_keep()
      {
         ConversationResumeSnapshot.Capture(new ConversationTranscript(), WithThatLordToday).Should().BeNull();
         ConversationResumeSnapshot.Capture(null, WithThatLordToday).Should().BeNull();
      }

      // AND A CAPTURE WITH NOBODY'S NAME ON IT IS WORSE THAN NONE: it would match the next chat that asked, and
      // hand one lord's words to another. The identity is the guard, so an incomplete one refuses outright.
      [Test]
      public void GIVEN_a_capture_with_no_hero_named_WHEN_it_is_taken_THEN_it_is_refused_rather_than_kept_for_anyone()
      {
         var nameless = new ConversationResumeIdentity {HeroId = "", GameDay = 412, AnchorId = "anchor-1"};

         ConversationResumeSnapshot.Capture(TheNegotiation(), nameless).Should().BeNull();
      }

      // THE NARRATION AND THE THIRD VOICES ARE PART OF THE SCENE. A witness who spoke, and the *stage directions*
      // the model writes, are both recorded in the transcript and both shown as their own kind of bubble. Keeping
      // only the two main speakers would bring the player back to a room that had emptied while he was away.
      [Test]
      public void GIVEN_a_scene_with_a_witness_and_narration_WHEN_it_is_restored_THEN_the_room_comes_back_whole()
      {
         var scene = new ConversationTranscript();
         scene.Record(Player("Speak plainly, both of you."));
         scene.Record(new TranscriptLine {SpeakerHeroId = "companion_nabb", SpeakerName = "Nabb", Text = "\"Plainly, then.\""});
         scene.Record(new TranscriptLine {IsNarration = true, SpeakerName = "Rhagaea", Text = "*She sets down the cup.*"});

         ConversationTranscript resumed = ConversationResumeSnapshot.Capture(scene, WithThatLordToday).ToTranscript();

         resumed.Lines.Should().HaveCount(3);
         resumed.Lines[1].SpeakerHeroId.Should().Be("companion_nabb", "a witness is not the main speaker and must not become one");
         resumed.Lines[2].IsNarration.Should().BeTrue("narration is rendered as narration, not as somebody saying those words aloud");
      }
   }
}
