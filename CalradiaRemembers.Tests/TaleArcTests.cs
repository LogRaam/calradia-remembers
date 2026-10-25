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
         for (var i = 0; i < exchanges && !closed; i++) closed = TaleArc.Exchanged(run, q, AuthoredTales.Giver, () => 0.99);

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

      // ---- Increment 2: signals and transitions (Gabriel, 25/10/2026: "si le NPC a eu un orgasme, le NPC a une conversation
      // avec le joueur de 5 beats puis on ferme le stage vers dawn. Sinon, le NPC reprend le beat ou elle prend
      // l'initiative"; "melange des deux, signaux observes avec tirage en filet"; "je garderais une intervention forte qui
      // retient"). ----

      private const string Looping = """
      {
        "id": "cr_test_loop", "title": "A Night That Turns",
        "binds": { "giver": "a lord" },
        "stages": [
          { "id": "night", "journal": "The night.",
            "objective": { "type": "talk_to", "hero": "{giver}", "when": "closed",
              "arc": {
                "signals": [
                  { "id": "npc_climaxed", "means": "{giver} reaches her climax in this reply", "net": { "after": 6, "chance": 0.5 } },
                  { "id": "refused", "means": "the player refuses or pushes {giver} away" } ],
                "on_signal": { "refused": "stung" },
                "max_exchanges": 30, "final": "after",
                "beats": [
                  { "id": "prelude", "exchanges": 3, "do": "Slow preliminaries." },
                  { "id": "she_leads", "exchanges": 3, "listens": "npc_climaxed", "do": "She takes the initiative." },
                  { "id": "yields", "exchanges": 3, "lead": "player", "listens": "npc_climaxed", "do": "She lets {player} lead.",
                    "next": [ { "if": "npc_climaxed", "to": "after" }, { "to": "she_leads" } ] },
                  { "id": "after", "exchanges": 5, "do": "Lying together, she asks for the secret." },
                  { "id": "stung", "exchanges": 2, "do": "Stung, she answers as an empress.", "next": [ { "to": "close" } ] } ] } },
            "on_complete": { "next": "end:dawn" } }
        ],
        "endings": { "dawn": { "memory": "A night.", "consequences": [ { "type": "memory", "who": "{giver}", "text": "A good night.", "if": "npc_climaxed" } ] } }
      }
      """;

      private static bool Step(AuthoredQuestRun run, AuthoredQuest q, double roll = 0.99, string signal = null)
      {
         if (signal != null) TaleArc.SignalSeen(run, q, AuthoredTales.Giver, signal);

         return TaleArc.Exchanged(run, q, AuthoredTales.Giver, () => roll);
      }

      private static string BeatOf(AuthoredQuestRun run, AuthoredQuest q) => TaleArc.Beat(run, q.Stages[0].Objective.Arc).Id;

      // STAKE: "si le NPC a eu un orgasme [...] 5 beats puis on ferme": seen while she leads, the night goes to its talk
      // after the player's part, and the flag is there for the ending's memory.
      [Test]
      public void GIVEN_her_climax_seen_while_she_leads_WHEN_the_players_part_ends_THEN_the_night_goes_to_the_talk_before_dawn()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         for (var i = 0; i < 3; i++) Step(run, q);
         Step(run, q, signal: "npc_climaxed");
         for (var i = 0; i < 5; i++) Step(run, q);

         BeatOf(run, q).Should().Be("after");
         run.Flags.Should().Contain("npc_climaxed");
      }

      // STAKE: "Sinon, le NPC reprend le beat ou elle prend l'initiative": nothing seen and the net not drawn, she leads again.
      [Test]
      public void GIVEN_no_climax_and_the_net_not_drawn_WHEN_the_players_part_ends_THEN_she_takes_the_initiative_again()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         for (var i = 0; i < 9; i++) Step(run, q, roll: 0.99);

         BeatOf(run, q).Should().Be("she_leads");
      }

      // STAKE: "tirage en filet": after six exchanges of listening the game may decide it, sets the flag, and tells her to
      // narrate it first in the next reply, before the talk that follows it.
      [Test]
      public void GIVEN_six_exchanges_listened_and_the_net_drawn_WHEN_the_players_part_ends_THEN_the_game_decides_it_and_she_narrates_it_first()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         for (var i = 0; i < 9; i++) Step(run, q, roll: 0.0);

         BeatOf(run, q).Should().Be("after");
         run.Flags.Should().Contain("npc_climaxed");
         string direction = TaleArc.Direction(run, q, AuthoredTales.Giver);
         direction.IndexOf("The game has decided", System.StringComparison.Ordinal)
                  .Should().BeInRange(0, direction.IndexOf("This part:", System.StringComparison.Ordinal));
      }

      // STAKE: the net waits its "after": three exchanges of listening are not enough, even on a roll that would succeed.
      [Test]
      public void GIVEN_fewer_exchanges_listened_than_the_net_asks_WHEN_her_part_ends_THEN_nothing_is_decided()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         for (var i = 0; i < 6; i++) Step(run, q, roll: 0.0);

         run.Flags.Should().NotContain("npc_climaxed");
      }

      // STAKE: the game decides what happens: a signal the current beat does not listen to is refused and sets nothing.
      [Test]
      public void GIVEN_the_preliminaries_WHEN_her_climax_is_reported_THEN_it_is_refused_and_sets_nothing()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");

         TaleArc.SignalSeen(run, q, AuthoredTales.Giver, "npc_climaxed").Should().BeFalse();
         run.Flags.Should().NotContain("npc_climaxed");
      }

      // STAKE: a refusal turns the night at once, whatever beat plays, to the beat the author wrote for it.
      [Test]
      public void GIVEN_a_signal_that_turns_the_arc_WHEN_it_is_seen_THEN_the_arc_jumps_to_its_beat_at_once()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         Step(run, q, signal: "refused");

         BeatOf(run, q).Should().Be("stung");
      }

      // STAKE: "une intervention forte qui retient": the player's strong turn holds the beat once and she answers it first;
      // a second in a row does not, so the player can never stall the night.
      [Test]
      public void GIVEN_the_player_turns_the_scene_WHEN_it_is_counted_THEN_the_beat_is_held_once_and_not_twice_in_a_row()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         Step(run, q);
         Step(run, q, signal: TaleArc.TurnedSignal);

         run.ArcBeatExchanges.Should().Be(1);
         TaleArc.Direction(run, q, AuthoredTales.Giver).Should().Contain("turned the scene");

         Step(run, q, signal: TaleArc.TurnedSignal);
         run.ArcBeatExchanges.Should().Be(2);
      }

      // STAKE: the loop she_leads -> yields -> she_leads always ends: max_exchanges sends it to the talk, then dawn.
      [Test]
      public void GIVEN_a_night_where_nothing_is_ever_seen_or_drawn_WHEN_exchanges_go_on_THEN_it_still_ends_within_its_cap_and_the_final_beat()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         var n = 0;
         var closed = false;
         while (!closed && n < 100)
         {
            closed = Step(run, q, roll: 0.99);
            n++;
         }

         closed.Should().BeTrue();
         n.Should().BeLessThanOrEqualTo(30 + 5);
      }

      // STAKE: on the last reply of the player's part the night may still go on; telling her it is the last would end a
      // scene that continues.
      [Test]
      public void GIVEN_the_last_reply_of_a_beat_whose_way_on_depends_on_a_signal_WHEN_she_is_told_the_arc_THEN_she_is_not_told_it_ends()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         for (var i = 0; i < 8; i++) Step(run, q);

         TaleArc.Direction(run, q, AuthoredTales.Giver).Should().NotContain("LAST reply");
      }

      // STAKE: whoever reads the turn's actions is taught exactly the signals the game would take now, and how to report them.
      [Test]
      public void GIVEN_her_part_WHEN_the_reader_is_taught_the_signals_THEN_it_learns_hers_the_refusal_and_the_players_turn()
      {
         AuthoredQuest q = Tale(Looping);
         AuthoredQuestRun run = AuthoredTales.At(q, "night");
         string prelude = TaleArc.SignalsForReader(run, q, AuthoredTales.Giver);
         for (var i = 0; i < 3; i++) Step(run, q);
         string herPart = TaleArc.SignalsForReader(run, q, AuthoredTales.Giver);

         prelude.Should().NotContain("npc_climaxed").And.Contain("- refused:").And.Contain("- turned:");
         herPart.Should().Contain("- npc_climaxed:").And.Contain("type: " + TaleArc.SignalVerb);
      }

      // STAKE: a signal's flag counts as set for the author's checks, or every arc tale would warn of a flag never set.
      [Test]
      public void GIVEN_a_transition_on_a_signals_flag_WHEN_the_tale_loads_THEN_the_author_is_not_warned_it_is_never_set()
      {
         AuthoredTales.Read(Looping).Problems.Should().BeEmpty();
      }

      // STAKE: an arc the game could not play, or could play forever, is refused when the tale loads.
      [TestCase("\"listens\": \"npc_climaxed\", \"do\": \"She takes", "\"listens\": \"moaned\", \"do\": \"She takes", "listens to 'moaned', which is not one of the arc's signals")]
      [TestCase("{ \"to\": \"she_leads\" }", "{ \"to\": \"bed\" }", "\"next\" goes to 'bed', which is neither one of its beats nor \"close\"")]
      [TestCase("\"max_exchanges\": 30,", "", "needs \"max_exchanges\" to be sure it ends")]
      [TestCase("{ \"id\": \"refused\",", "{ \"id\": \"turned\",", "is the game's own signal")]
      [TestCase("\"on_signal\": { \"refused\": \"stung\" }", "\"on_signal\": { \"slapped\": \"stung\" }", "names 'slapped', which is not one of its signals")]
      public void GIVEN_an_arc_with_signals_the_game_cannot_play_WHEN_the_tale_loads_THEN_the_author_is_told_why(string from, string to, string told)
      {
         AuthoredTales.Read(Looping.Replace(from, to)).Problems.Should().Contain(p => p.Contains(told));
      }
   }
}
