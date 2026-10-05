// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md): what each character and the scene's narrator are told once a tale
// has choices and flags. Gabriel's standing rules: a character is told only what they know AT THIS STAGE and never what
// comes next, so a twist stays hidden until the player uncovers it; characters never lie; the model chooses the moment,
// the game decides whether it counts. These texts feed the character's prompt (EncounterContext.AuthoredQuestBlock)
// and the narrator's. If they are wrong, a character speaks of a path the player never took, the narrator offers a way
// out that does not exist for this player, or a line meant for one path leaks into another.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredQuestBranchPromptTests
   {
      private const string Giver = AuthoredTales.Giver;
      private const string Village = AuthoredTales.Village;

      private static AuthoredQuestRun At(AuthoredQuest q, string stage, params string[] flags)
      {
         AuthoredQuestRun run = AuthoredTales.Start(q);
         run.StageId = stage;
         run.Flags.AddRange(flags);

         return run;
      }

      // A line under a flag reaches the character only on the path where the flag was set: the lord hears of the
      // smugglers' lantern only if the player saw the light.
      [Test]
      public void GIVEN_a_knower_line_under_a_flag_WHEN_the_flag_holds_or_not_THEN_the_character_is_told_it_only_when_it_holds()
      {
         AuthoredQuest q = AuthoredTales.Load();

         string lit = AuthoredQuestPrompt.ForCharacter(At(q, "the_mill", "saw_light"), q, Giver, null, offering: false);
         string dark = AuthoredQuestPrompt.ForCharacter(At(q, "the_mill"), q, Giver, null, offering: false);

         lit.Should().Contain("smugglers keep a lantern").And.NotContain("Nobody has told you of any light");
         dark.Should().NotContain("smugglers").And.Contain("Nobody has told you of any light");
      }

      // The canvas works the same way: in the conversation that completes the stage, the lord asks about the light only
      // when there was one.
      [Test]
      public void GIVEN_a_canvas_line_under_a_flag_WHEN_the_lord_hears_the_report_THEN_he_asks_about_the_light_only_if_it_was_seen()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredQuestRun lit = At(q, "the_mill", "saw_light");
         AuthoredQuestRun dark = At(q, "the_mill");
         AuthoredQuestEngine.ConversationOpened(lit, q, Giver, null, 110);
         AuthoredQuestEngine.ConversationOpened(dark, q, Giver, null, 110);

         AuthoredQuestPrompt.ForCharacter(lit, q, Giver, null, offering: false).Should().Contain("relief is plain").And.Contain("light in the wood");
         AuthoredQuestPrompt.ForCharacter(dark, q, Giver, null, offering: false).Should().Contain("relief is plain").And.NotContain("light in the wood");
      }

      // Secrecy: at the road, the lord is told he waits for news, and nothing of the mill, the river, the light or the
      // ways the player might take.
      [Test]
      public void GIVEN_a_stage_with_choices_WHEN_the_lord_speaks_THEN_he_is_told_none_of_the_ways_ahead_nor_what_lies_down_them()
      {
         AuthoredQuest q = AuthoredTales.Load();

         string block = AuthoredQuestPrompt.ForCharacter(At(q, "the_road"), q, Giver, null, offering: false);

         block.Should().Contain("wait for news");
         foreach (string ahead in new[] {"Ride to", "not your affair", "mill", "river", "light", "smugglers", "cap"}) block.Should().NotContain(ahead);
      }

      // Secrecy: the villagers learn the boy is found only at the stage where he is; before, they are told nothing.
      [Test]
      public void GIVEN_a_line_of_a_later_stage_WHEN_a_villager_speaks_earlier_THEN_they_are_told_nothing_of_it()
      {
         AuthoredQuest q = AuthoredTales.Load();

         AuthoredQuestPrompt.ForCharacter(At(q, "the_road"), q, "notable_a", Village, offering: false).Should().BeNull();
         AuthoredQuestPrompt.ForCharacter(At(q, "the_mill"), q, "notable_a", Village, offering: false).Should().Contain("said to be found");
      }

      // The lord whose conversation took the "give up" path speaks from that path while it lasts: disappointed, and
      // knowing the player refused, not still waiting for news.
      [Test]
      public void GIVEN_the_conversation_that_took_a_choice_WHEN_the_lord_speaks_in_it_THEN_he_speaks_from_that_choice()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredQuestRun run = At(q, "the_road");
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 101).EndingId.Should().Be("abandoned");

         string block = AuthoredQuestPrompt.ForCharacter(run, q, Giver, null, offering: false);

         block.Should().Contain("disappointment").And.Contain("refused to look for the boy").And.NotContain("wait for news");
      }

      // When that conversation closes, the path is behind him: he is no longer told it.
      [Test]
      public void GIVEN_the_conversation_that_ended_the_tale_closed_WHEN_the_lord_speaks_again_THEN_the_tale_is_no_longer_his_to_speak()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredQuestRun run = At(q, "the_road");
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 101);
         AuthoredQuestEngine.ConversationClosed(run, Giver);

         AuthoredQuestPrompt.ForCharacter(run, q, Giver, null, offering: false).Should().BeNull();
      }

      // The narrator is told the ways out open now, each with its marker, and never a closed one: before the light is
      // seen, the left path does not exist; once it is, it does.
      [Test]
      public void GIVEN_a_scene_with_ways_out_WHEN_the_narrator_is_told_THEN_it_knows_only_the_open_ways_each_with_its_marker()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredQuestRun run = At(q, "the_wood");

         string before = AuthoredQuestPrompt.Narrator(run, q);
         AuthoredQuestEngine.Found(run, q, "lantern");
         string after = AuthoredQuestPrompt.Narrator(run, q);

         before.Should().Contain("[[choice:right]]").And.Contain("[[choice:turn_back]]").And.NotContain("[[choice:left]]").And.NotContain("Follow the light");
         after.Should().Contain("[[choice:left]]").And.Contain("Follow the light to the left");
      }

      // The narrator takes a way only on the player's words, and the scene ends with it: never chosen for the player.
      [Test]
      public void GIVEN_a_scene_with_ways_out_WHEN_the_narrator_is_told_THEN_it_takes_one_only_on_the_players_words_and_ends_there()
      {
         AuthoredQuest q = AuthoredTales.Load();

         string prompt = AuthoredQuestPrompt.Narrator(At(q, "the_wood"), q);

         prompt.Should().Contain("ONLY when the player's own words choose it, never for them").And.Contain("the scene closes");
      }

      // The narrator of the wood knows nothing of what waits down either path (the mill's smugglers, the river's cap).
      [Test]
      public void GIVEN_a_scene_before_its_paths_WHEN_the_narrator_is_told_THEN_nothing_of_the_stages_beyond_is_in_it()
      {
         AuthoredQuest q = AuthoredTales.Load();

         string prompt = AuthoredQuestPrompt.Narrator(At(q, "the_wood", "saw_light"), q);

         foreach (string beyond in new[] {"smugglers", "cap", "old mill", "relief"}) prompt.Should().NotContain(beyond);
      }

      private static AuthoredQuest WithWords(string extraIf = "")
         => AuthoredTales.Load(AuthoredTales.Fork.Replace("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            extraIf + "\"said\": { \"to\": \"{giver}\", \"meaning\": \"the player says the boy of {village} is not worth the trouble\" }"));

      // A choice by words reaches the interpreter of THIS conversation: its id, what the words must mean (names filled),
      // and the exact report the game reads back; the game parses that report into the choice it will check.
      [Test]
      public void GIVEN_a_choice_by_words_open_with_this_character_WHEN_the_interpreter_is_told_THEN_it_learns_the_id_the_meaning_and_the_report()
      {
         AuthoredQuest q = WithWords();

         string told = AuthoredQuestPrompt.ChoicesForInterpreter(At(q, "the_road"), q, Giver);
         var parsed = new NpcMemoryService.Core.Parsing.SectionResponseParser().Parse("[DIALOGUE]So be it.[/DIALOGUE]\n" + told.Substring(told.IndexOf("[ACTION]", System.StringComparison.Ordinal)).Replace("<the choice id>", "let_it_be"));

         told.Should().Contain("choice let_it_be: the player says the boy of Ortongard is not worth the trouble").And.NotContain("{village}");
         parsed.Actions.Should().ContainSingle(a => a.Type == AuthoredQuestPrompt.StoryChoiceVerb && a.Parameters["choice"] == "let_it_be");
      }

      // Taught only where it can be taken: not in a conversation with someone else, not while closed by its flag, and
      // never once the stage has moved on.
      [Test]
      public void GIVEN_another_character_a_closed_choice_or_a_later_stage_WHEN_the_interpreter_is_told_THEN_it_learns_nothing()
      {
         AuthoredQuest open = WithWords(), closed = WithWords("\"if\": \"saw_light\", ");

         AuthoredQuestPrompt.ChoicesForInterpreter(At(open, "the_road"), open, "lord_elsewhere").Should().BeNull();
         AuthoredQuestPrompt.ChoicesForInterpreter(At(closed, "the_road"), closed, Giver).Should().BeNull();
         AuthoredQuestPrompt.ChoicesForInterpreter(At(open, "the_wood"), open, Giver).Should().BeNull();
      }

      // A scene with no ways out is told exactly as before: no section about ways at all.
      [Test]
      public void GIVEN_a_scene_written_before_branches_WHEN_the_narrator_is_told_THEN_no_way_out_is_offered()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredQuestRun run = At(q, "the_wood");
         q.Stages.Single(s => s.Id == "the_wood").Choices.Clear();

         AuthoredQuestPrompt.Narrator(run, q).Should().NotContain("WAYS THE PLAYER MAY TAKE").And.NotContain("[[choice:");
      }
   }
}
