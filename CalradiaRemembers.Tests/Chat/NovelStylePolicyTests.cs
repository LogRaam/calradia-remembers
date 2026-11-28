// Code written by Gabriel Mailhot, 28/11/2026.
// NovelStylePolicy decides what the chat does with a reply in the novel style (stage 5; Gabriel, 28/11/2026: "oui ok
// j'aime ça" to speech in quotation marks and gestures as plain third-person prose, no asterisks). Wrong one way, a
// Roleplay player's chat changes, or a captive scene loses its own contract; wrong the other, a Novel player still sees
// *asterisks*, a first-person gesture reads as the player narrating, or a question ending in a gesture is lost and the
// bystanders re-ask what the player was meant to answer (Lordfadooboo, 20/10/2026).

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Chat
{
   [TestFixture]
   public class NovelStylePolicyTests
   {
      // STAKE: a player who kept the default Roleplay must see the chat exactly as before.
      [Test]
      public void GIVEN_roleplay_chosen_WHEN_asked_THEN_the_novel_style_does_not_apply()
         => NovelStylePolicy.Applies(new NovelStyleFacts {NovelChosen = false}).Should().BeFalse();

      // STAKE: a Novel player whose chat ignored the choice would still read asterisks.
      [Test]
      public void GIVEN_novel_chosen_WHEN_an_ordinary_conversation_THEN_the_novel_style_applies()
         => NovelStylePolicy.Applies(new NovelStyleFacts {NovelChosen = true}).Should().BeTrue();

      // STAKE: a captive scene keeps its own voice contract; the novel style must not reach it.
      [Test]
      public void GIVEN_novel_chosen_WHEN_a_captive_scene_THEN_the_novel_style_does_not_apply()
         => NovelStylePolicy.Applies(new NovelStyleFacts {NovelChosen = true, CaptiveScene = true}).Should().BeFalse();

      // STAKE: a model that still writes a starred gesture in the novel style would show the asterisks the player turned away from.
      [Test]
      public void GIVEN_a_starred_third_person_gesture_WHEN_shown_THEN_it_becomes_plain_prose()
         => NovelStylePolicy.ForDisplay("*She sets down the cup.* \"Sit.\"").Should().Be("She sets down the cup. \"Sit.\"");

      // STAKE: an unwrapped fragment without a capital or a full stop reads as broken prose.
      [Test]
      public void GIVEN_a_starred_fragment_after_speech_WHEN_shown_THEN_it_is_a_sentence()
         => NovelStylePolicy.ForDisplay("\"Sit.\" *gestures to the chair*").Should().Be("\"Sit.\" Gestures to the chair.");

      // STAKE: a gesture after a comma inside the speech continues the sentence; a capital there reads wrong.
      [Test]
      public void GIVEN_a_starred_gesture_after_a_comma_WHEN_shown_THEN_it_keeps_its_small_letter()
         => NovelStylePolicy.ForDisplay("\"Sit,\" *she says, pointing.* \"Now.\"").Should().Be("\"Sit,\" she says, pointing. \"Now.\"");

      // STAKE: bare first-person prose outside quotes reads as the PLAYER narrating; such a span keeps its asterisks.
      [Test]
      public void GIVEN_a_starred_first_person_gesture_WHEN_shown_THEN_it_is_left_as_written()
         => NovelStylePolicy.ForDisplay("*I narrow my eyes.* \"You came back.\"").Should().Be("*I narrow my eyes.* \"You came back.\"");

      // STAKE: a reply already in the novel style must reach the screen untouched.
      [Test]
      public void GIVEN_a_reply_without_asterisks_WHEN_shown_THEN_it_is_unchanged()
         => NovelStylePolicy.ForDisplay("She sets down the cup. \"Sit.\"").Should().Be("She sets down the cup. \"Sit.\"");

      // STAKE: markdown bold around a gesture would leave stray asterisks on screen.
      [Test]
      public void GIVEN_a_bold_gesture_WHEN_shown_THEN_no_asterisk_is_left()
         => NovelStylePolicy.ForDisplay("**She laughs.** \"Fine.\"").Should().Be("She laughs. \"Fine.\"");

      // STAKE: a question followed by a plain gesture is a question to the player; the turn-taking test must still see it.
      [Test]
      public void GIVEN_a_novel_line_ending_on_a_gesture_WHEN_its_spoken_part_is_read_THEN_the_question_is_found()
         => EnsembleFloorPolicy.EndsOnAQuestion(NovelStylePolicy.SpokenPart("\"Where were you?\" She sets down the cup.")).Should().BeTrue();

      // STAKE: a statement with no question must not hand the floor away because a gesture was stripped.
      [Test]
      public void GIVEN_a_novel_line_with_a_question_inside_then_a_statement_WHEN_read_THEN_it_does_not_end_on_a_question()
         => EnsembleFloorPolicy.EndsOnAQuestion(NovelStylePolicy.SpokenPart("\"Why? Because I said so.\" She turns away.")).Should().BeFalse();

      // STAKE: a model that wrote speech without quotes would have its whole line thrown away; such a line is read as it is.
      [Test]
      public void GIVEN_a_line_without_quotes_WHEN_its_spoken_part_is_read_THEN_it_is_the_whole_line()
         => NovelStylePolicy.SpokenPart("Where were you?").Should().Be("Where were you?");

      // STAKE: curly quotes are quotes; a model writing them must not lose its speech.
      [Test]
      public void GIVEN_curly_quotes_WHEN_the_spoken_part_is_read_THEN_the_speech_is_found()
         => NovelStylePolicy.SpokenPart("He leans in. “And you?” He waits.").Should().Be("“And you?”");

      // STAKE: in a room of several voices the floor goes to the one asked; a novel line's question must be seen there too.
      [Test]
      public void GIVEN_novel_speech_WHEN_the_last_line_asks_and_ends_on_a_gesture_THEN_the_other_voice_answers()
      {
         var lines = new[] {new TranscriptLine {SpeakerHeroId = "a", Text = "\"What say you?\" She folds her arms."}};

         EnsembleFloorPolicy.WhoAnswers(lines, new[] {"a", "b"}, "a", 0, novelSpeech: true).Should().Be("b");
         EnsembleFloorPolicy.WhoAnswers(lines, new[] {"a", "b"}, "a", 0).Should().BeNull();
      }

      // STAKE: the interpreter is told who "she" is, or a deed the NPC narrated of itself is read as nobody's.
      [Test]
      public void GIVEN_the_interpreter_note_WHEN_read_THEN_it_says_the_third_person_deeds_are_the_npcs()
         => NovelStylePolicy.InterpreterNote.Should().Contain("is the NPC's own act");
   }
}
