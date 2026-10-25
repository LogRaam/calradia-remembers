// Code written by Gabriel Mailhot, 25/10/2026.
// The arc that paces a tale's conversation (TaleArc; ROADMAP "an arc that paces a tale's conversation", increment 1, the
// straight arc). Gabriel (25/10/2026), on the night in Rhagaea's chamber: "comment le LLM dirige le beat pour amener le
// joueur vers le ending dawn? Car ca pourrait etre trop court ou trop long", then the shape: "les 3 premiers servent aux
// preliminaires, les 3 suivants le NPC prend l'initiative, les 3 suivants il laisse au joueur l'initiative [...] Ce plan
// ou cet arc devrait diriger l'experience que le LLM fait vivre au joueur."
//
// WHY IT MATTERS: without the arc the night lasts as long as the player types, and the model, which has no clock, guesses
// when "before the night ends" is. If the count is wrong the scene closes early or never; if the direction is wrong the
// model skips ahead, or is cut off mid-scene because nobody told it this reply was the last; if the arc survives a stage
// change, the next conversation starts half-way through someone else's night.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleArcTests
   {
      private const string Night = """
      {
        "id": "cr_test_night", "title": "A Night",
        "binds": { "giver": "a lord" },
        "stages": [
          { "id": "night", "journal": "The night.",
            "objective": { "type": "talk_to", "hero": "{giver}", "when": "closed",
              "arc": { "beats": [
                { "id": "prelude", "exchanges": 3, "lead": "npc", "do": "Slow preliminaries." },
                { "id": "she_leads", "exchanges": 3, "lead": "npc", "do": "She takes the initiative." },
                { "id": "yields", "exchanges": 3, "lead": "player", "do": "She lets {player} lead." },
                { "id": "after", "exchanges": 5, "lead": "npc", "do": "Lying together, she asks for the secret." } ] } },
            "canvas": "The chamber at night.",
            "on_complete": { "next": "end:dawn" } }
        ],
        "endings": { "dawn": { "memory": "A night." } }
      }
      """;

      private static AuthoredQuest Tale(string json = Night) => AuthoredTales.Load(json);

      private static AuthoredQuestRun Play(AuthoredQuest q, int exchanges, out bool closed)
      {
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         closed = false;
         for (var i = 0; i < exchanges && !closed; i++) closed = TaleArc.Exchanged(run, q, AuthoredTales.Giver);

         return run;
      }

      // STAKE: "trop court ou trop long": Gabriel's 3/3/3/5 night lasts fourteen exchanges, not one fewer, and then the
      // conversation closes rather than running on for as long as the player types.
      [Test]
      public void GIVEN_a_night_of_three_three_three_and_five_WHEN_exchanges_are_played_THEN_it_closes_on_the_fourteenth_and_not_before()
      {
         AuthoredQuest q = Tale();

         Play(q, 13, out bool closedEarly);
         Play(q, 14, out bool closedOnTime);

         closedEarly.Should().BeFalse();
         closedOnTime.Should().BeTrue();
      }

      // STAKE: the beats come in the order the author wrote: after three exchanges of preliminaries she takes the
      // initiative, and after three more she lets the player lead.
      [Test]
      public void GIVEN_the_preliminaries_played_out_WHEN_the_next_exchanges_come_THEN_she_takes_the_initiative_then_yields()
      {
         AuthoredQuest q = Tale();

         TaleArc.Beat(Play(q, 3, out _), q.Stages[0].Objective.Arc).Id.Should().Be("she_leads");
         TaleArc.Beat(Play(q, 6, out _), q.Stages[0].Objective.Arc).Id.Should().Be("yields");
      }

      // STAKE: the model has no clock; it is told which part it plays, so it neither rushes to the end nor ends the scene.
      [Test]
      public void GIVEN_the_first_reply_of_the_night_WHEN_the_character_is_told_the_arc_THEN_it_plays_the_preliminaries_and_is_told_not_to_hurry()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = AuthoredTales.At(q, "night");

         string direction = TaleArc.Direction(run, q, AuthoredTales.Giver);

         direction.Should().Contain("Slow preliminaries.").And.Contain("part 1 of 4").And.Contain("do not hurry ahead");
      }

      // STAKE: "les 3 suivants il laisse au joueur l'initiative": in a beat the player leads, she follows instead of pushing.
      [Test]
      public void GIVEN_a_beat_the_player_leads_WHEN_the_character_is_told_the_arc_THEN_it_is_told_to_follow()
      {
         AuthoredQuest q = Tale();

         TaleArc.Direction(Play(q, 6, out _), q, AuthoredTales.Giver).Should().Contain("The player leads this part");
      }

      // STAKE: the soft close comes after her last reply; if she is not told it is the last, she is cut off mid-scene and
      // never asks for the secret.
      [Test]
      public void GIVEN_the_last_reply_of_the_final_beat_WHEN_the_character_is_told_the_arc_THEN_it_knows_this_reply_ends_the_scene()
      {
         AuthoredQuest q = Tale();

         string last = TaleArc.Direction(Play(q, 13, out _), q, AuthoredTales.Giver);
         string before = TaleArc.Direction(Play(q, 12, out _), q, AuthoredTales.Giver);

         last.Should().Contain("LAST reply of the scene");
         before.Should().NotContain("LAST reply");
      }

      // STAKE: the cap is the arc's hard bound: reached, the arc goes to its final beat whatever else is playing, so the
      // night always ends with its farewell.
      [Test]
      public void GIVEN_max_exchanges_reached_WHEN_the_next_exchange_is_counted_THEN_the_arc_plays_its_final_beat()
      {
         AuthoredQuest q = Tale(Night.Replace("\"beats\": [", "\"max_exchanges\": 4, \"beats\": ["));

         AuthoredQuestRun run = Play(q, 4, out bool closed);

         closed.Should().BeFalse();
         TaleArc.Beat(run, q.Stages[0].Objective.Arc).Id.Should().Be("after");
      }

      // STAKE: the arc belongs to its stage: once the tale moves on, nothing of it is told to the character or counted.
      [Test]
      public void GIVEN_the_conversation_closed_by_the_player_WHEN_the_stage_completes_THEN_the_arc_is_cleared_with_it()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = Play(q, 5, out _);

         AuthoredQuestEngine.ConversationEnded(run, q, AuthoredTales.Giver, 101);

         run.Status.Should().Be(AuthoredQuestStatus.Completed);
         run.ArcBeatId.Should().BeNull();
         run.ArcExchanges.Should().Be(0);
      }

      // STAKE: a night interrupted by a save goes on from the same beat after the load.
      [Test]
      public void GIVEN_a_night_half_played_WHEN_the_game_is_saved_and_loaded_THEN_the_arc_resumes_where_it_was()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = Play(q, 7, out _);

         AuthoredQuestRun loaded = AuthoredQuestRun.Deserialize(run.Serialize());

         TaleArc.Beat(loaded, q.Stages[0].Objective.Arc).Id.Should().Be("yields");
         loaded.ArcBeatExchanges.Should().Be(1);
      }

      // STAKE: only the conversation the stage names is paced; another character's talk is not told the night.
      [Test]
      public void GIVEN_another_character_WHEN_they_are_told_the_arc_THEN_they_are_told_nothing()
      {
         AuthoredQuest q = Tale();

         TaleArc.Direction(AuthoredTales.At(q, "night"), q, "lord_someone_else").Should().BeNull();
      }

      // STAKE: the arc reaches the character inside the tale's own block, below the moment it belongs to.
      [Test]
      public void GIVEN_the_night_under_way_WHEN_the_givers_prompt_block_is_written_THEN_it_carries_the_beat_after_the_moment()
      {
         AuthoredQuest q = Tale();
         string block = AuthoredQuestPrompt.ForCharacter(AuthoredTales.At(q, "night"), q, AuthoredTales.Giver, null, offering: false);

         block.Should().Contain("Slow preliminaries.");
         block.IndexOf("Slow preliminaries.", System.StringComparison.Ordinal).Should().BeGreaterThan(block.IndexOf("This moment:", System.StringComparison.Ordinal));
      }

      // STAKE: an arc where the game could never play it is refused when the tale loads, not discovered in play.
      [TestCase("\"when\": \"closed\",", "", "needs \"when\": \"closed\"")]
      [TestCase("\"lead\": \"player\"", "\"lead\": \"her\"", "\"lead\" 'her' is neither npc nor player")]
      [TestCase("\"exchanges\": 5,", "\"exchanges\": 0,", "needs \"exchanges\" from 1 to 20")]
      [TestCase("\"do\": \"Slow preliminaries.\"", "\"do\": \"\"", "needs \"do\"")]
      [TestCase("\"beats\": [", "\"final\": \"dawn\", \"beats\": [", "the arc's \"final\" 'dawn' is not one of its beats")]
      [TestCase("\"id\": \"she_leads\"", "\"id\": \"prelude\"", "repeats the id 'prelude'")]
      public void GIVEN_an_arc_the_game_cannot_play_WHEN_the_tale_loads_THEN_the_author_is_told_why(string from, string to, string told)
      {
         AuthoredTales.Read(Night.Replace(from, to)).Problems.Should().Contain(p => p.Contains(told));
      }
   }
}
