// Code written by Gabriel Mailhot, 13/09/2026.
// The case these were written from is real and is reproduced verbatim below: Gabriel briefed Nabb to charm
// Ira, Nabb ended his beat by turning to her and asking "Which ones do you remember?", and the floor came
// back to the player with the question hanging. He had to spend a turn typing "*I watch and wait to see what
// happens*" to make Ira answer.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class EnsembleFloorPolicyTests
   {
      private const string Ira = "lord_1_37";
      private const string Nabb = "companion_nabb";
      private const string Third = "companion_coll";

      private static TranscriptLine Said(string heroId, string name, string text) =>
         new TranscriptLine {IsPlayer = false, IsNarration = false, SpeakerHeroId = heroId, SpeakerName = name, Text = text};

      // ── the reported case, verbatim ──

      // Nabb's actual closing line. Note it never says "Ira": the address is carried by the narration and by
      // the fact that only one other person has a voice. A rule that required the name would miss this.
      private const string NabbsQuestion =
         "*He looks back to Ira, and something shifts in his bearing.* \"Bold men die young, you said. Useful "
       + "men survive.\" *His head tilts, studying her the way she's been studying him.* \"I'd wager you've "
       + "killed men of both sorts. Which ones do you remember?\" *The question hangs - not a challenge, not a "
       + "deflection. An invitation.*";

      [Test]
      public void GIVEN_the_beat_Gabriel_played_WHEN_it_closes_THEN_Ira_is_given_the_floor_to_answer()
      {
         var beat = new List<TranscriptLine> {
            new TranscriptLine {IsPlayer = true, SpeakerName = "You", Text = "Nabb, speak for yourself."},
            Said(Ira, "Ira", "\"Your Master of Arms has a bold mouth, Thais. Which one is he?\""),
            Said(Nabb, "Nabb the Bloody Handed", NabbsQuestion)
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb}, Ira, 0).Should().Be(Ira);
      }

      // THE FAULT ITSELF: the beat's LAST line is a stage direction closing on a full stop. Testing the raw
      // text for "ends with ?" would read that and answer no, which is why stage directions are stripped
      // first. This assertion is the difference between the feature working and never firing at all.
      [Test]
      public void GIVEN_a_question_followed_by_closing_narration_WHEN_read_THEN_it_still_counts_as_asked()
      {
         EnsembleFloorPolicy.EndsOnAQuestion(NabbsQuestion).Should().BeTrue();
      }

      // ── what must NOT reopen the floor ──

      // A speaker holding the floor: a rhetorical question, then their own answer to it. Nothing was handed
      // over, and answering it would be an NPC talking over another NPC's finished thought.
      [Test]
      public void GIVEN_a_rhetorical_question_answered_by_the_same_speaker_WHEN_read_THEN_nothing_was_asked()
      {
         EnsembleFloorPolicy
            .EndsOnAQuestion("\"You think I would refuse? I would not. The matter is settled.\"")
            .Should()
            .BeFalse();
      }

      [Test]
      public void GIVEN_a_beat_that_ends_on_a_statement_WHEN_deciding_THEN_the_floor_returns_to_the_player()
      {
         var beat = new List<TranscriptLine> {
            Said(Ira, "Ira", "\"Then we are agreed.\""),
            Said(Nabb, "Nabb the Bloody Handed", "*He inclines his head.* \"As you say, my lady.\"")
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb}, Ira, 0).Should().BeNull();
      }

      // A question with nobody else in the room is a question for the PLAYER, and the player already has the
      // floor. Answering it would have the NPC hold both sides of the conversation.
      [Test]
      public void GIVEN_only_one_voice_in_the_room_WHEN_it_asks_THEN_the_question_is_the_players_to_answer()
      {
         var beat = new List<TranscriptLine> {Said(Ira, "Ira", "\"And what would you have me do?\"")};

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira}, Ira, 0).Should().BeNull();
      }

      // A speaker cannot be handed the floor to answer their own question, whatever else is true.
      [Test]
      public void GIVEN_the_asker_is_in_the_cast_WHEN_choosing_THEN_they_are_never_chosen_to_answer_themselves()
      {
         var beat = new List<TranscriptLine> {Said(Nabb, "Nabb the Bloody Handed", "\"Which ones do you remember?\"")};

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb}, Ira, 0).Should().NotBe(Nabb);
      }

      // THE BOUND. Without it two NPCs can hold a conversation the player only watches - which is a worse
      // failure than the silence this fixes, because the player cannot interrupt it.
      [Test]
      public void GIVEN_this_beat_has_already_answered_once_WHEN_asked_again_THEN_the_floor_goes_back_to_the_player()
      {
         var beat = new List<TranscriptLine> {Said(Nabb, "Nabb the Bloody Handed", NabbsQuestion)};

         EnsembleFloorPolicy
            .WhoAnswers(beat, new[] {Ira, Nabb}, Ira, EnsembleFloorPolicy.RepliesPerBeat)
            .Should()
            .BeNull();
      }

      [Test]
      public void GIVEN_the_cap_WHEN_read_THEN_one_beat_can_never_spend_more_than_a_single_answer()
      {
         EnsembleFloorPolicy.RepliesPerBeat.Should().Be(1);
      }

      // A closing question inside NARRATION is the narrator's, asked of nobody. Answering it would have a
      // character reply to a stage direction.
      [Test]
      public void GIVEN_the_last_line_is_narration_that_asks_WHEN_deciding_THEN_no_one_is_made_to_answer_it()
      {
         var beat = new List<TranscriptLine> {
            Said(Ira, "Ira", "\"Then we are agreed.\""),
            new TranscriptLine {IsNarration = true, SpeakerHeroId = Ira, SpeakerName = "Ira", Text = "*And who could say otherwise?*"}
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb}, Ira, 0).Should().BeNull();
      }

      // ── aiming it in a crowded room ──

      // With three voices the question has to be aimed, and the name the asker actually used wins over the
      // anchor's standing claim on the room.
      [Test]
      public void GIVEN_three_voices_and_a_name_spoken_WHEN_choosing_THEN_the_one_named_answers()
      {
         var beat = new List<TranscriptLine> {
            Said(Ira, "Ira", "\"Speak, then.\""),
            Said(Third, "Coll the Unbroken", "\"I have said my piece.\""),
            Said(Nabb, "Nabb the Bloody Handed", "\"Coll, you were there. What did you see?\"")
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb, Third}, Ira, 0).Should().Be(Third);
      }

      // Nobody named: an unaimed question in the anchor's own meeting falls to the anchor.
      [Test]
      public void GIVEN_three_voices_and_no_name_spoken_WHEN_choosing_THEN_the_anchor_answers()
      {
         var beat = new List<TranscriptLine> {
            Said(Third, "Coll the Unbroken", "\"I have said my piece.\""),
            Said(Nabb, "Nabb the Bloody Handed", "\"And who here would say otherwise?\"")
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb, Third}, Ira, 0).Should().Be(Ira);
      }

      // The LAST name mentioned wins: a line that recalls one person before turning to another is turning to
      // the second.
      [Test]
      public void GIVEN_two_names_in_one_line_WHEN_choosing_THEN_the_one_turned_to_last_answers()
      {
         var beat = new List<TranscriptLine> {
            Said(Ira, "Ira", "\"Speak.\""),
            Said(Third, "Coll the Unbroken", "\"As you wish.\""),
            Said(Nabb, "Nabb the Bloody Handed", "\"Coll said as much. Ira, which ones do you remember?\"")
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb, Third}, Ira, 0).Should().Be(Ira);
      }

      // ── it must never throw on the shapes a live beat really produces ──

      [Test]
      public void GIVEN_nothing_or_junk_WHEN_deciding_THEN_it_declines_quietly_rather_than_throwing()
      {
         EnsembleFloorPolicy.WhoAnswers(null, new[] {Ira}, Ira, 0).Should().BeNull();
         EnsembleFloorPolicy.WhoAnswers(new List<TranscriptLine>(), null, Ira, 0).Should().BeNull();
         EnsembleFloorPolicy.WhoAnswers(new List<TranscriptLine>(), new[] {Ira}, Ira, 0).Should().BeNull();
         EnsembleFloorPolicy.WhoAnswers(new List<TranscriptLine> {null}, new[] {Ira}, Ira, 0).Should().BeNull();
         EnsembleFloorPolicy.EndsOnAQuestion(null).Should().BeFalse();
         EnsembleFloorPolicy.EndsOnAQuestion("   ").Should().BeFalse();
         EnsembleFloorPolicy.EndsOnAQuestion("*shrugs*").Should().BeFalse();
      }

      // A player line is never the thing a character is made to answer here: the player's own turn already
      // drives the ordered pass, and answering it again would double every beat.
      [Test]
      public void GIVEN_the_player_asked_the_last_question_WHEN_deciding_THEN_no_extra_turn_is_spent_on_it()
      {
         var beat = new List<TranscriptLine> {
            Said(Ira, "Ira", "\"Then we are agreed.\""),
            new TranscriptLine {IsPlayer = true, SpeakerName = "You", Text = "Are we?"}
         };

         EnsembleFloorPolicy.WhoAnswers(beat, new[] {Ira, Nabb}, Ira, 0).Should().BeNull();
      }
   }
}
