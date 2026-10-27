// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales: the numbered list of a scene's ways out (Gabriel, 04/10/2026: "like an LLM does in the CLI: Choice
// 1, Choice 2, Chat about that"). This decides what the chat lists under the narration, and what a typed number
// means; its answer feeds the list in both chat skins and the line sent to the narrator. If it is wrong, the player is
// offered a way that does not exist for them, a typed "1" takes a path they never saw listed first, a number meant as
// words is swallowed, or a click moves the story without the narrator ever telling it.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SceneChoiceMenuTests
   {
      private static SceneChoiceMenuFacts Fresh(params string[] ways) => new() {OpenWays = ways, NarrationIsLast = true};

      /// <summary>The list as the chat builds it: the open ways of the scene the engine reports, names filled.</summary>
      private static SceneChoiceMenuFacts FromTheWood(AuthoredQuestRun run, AuthoredQuest q)
         => new() {OpenWays = AuthoredQuestEngine.SceneChoices(run, q).Select(c => AuthoredQuestEngine.Fill(run, c.Label)).ToList(), NarrationIsLast = true};

      private static (AuthoredQuest, AuthoredQuestRun) InTheWood()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredQuestRun run = AuthoredTales.Start(q);
         run.StageId = "the_wood";

         return (q, run);
      }

      // Gabriel's menu: the open ways, numbered in order, and a last line to write freely.
      [Test]
      public void GIVEN_open_ways_under_a_fresh_narration_WHEN_listed_THEN_they_are_numbered_in_order_and_end_with_something_else()
      {
         IReadOnlyList<SceneChoiceMenuItem> items = SceneChoiceMenu.Items(Fresh("Take the left path", "Take the right path"));

         items.Select(i => i.Text).Should().Equal("1. Take the left path", "2. Take the right path", "3. Something else...");
         items[0].Sends.Should().Be("Take the left path");
         items[2].WriteFreely.Should().BeTrue();
      }

      // The list disappears once the player writes, stays away while the narrator writes, and comes back under the next
      // narration; once the scene is over there is nothing left to choose.
      [Test]
      public void GIVEN_the_player_answered_the_narrator_is_writing_or_the_scene_is_over_WHEN_listed_THEN_nothing_is_shown()
      {
         string[] ways = {"Take the left path"};

         SceneChoiceMenu.Items(new SceneChoiceMenuFacts {OpenWays = ways, NarrationIsLast = false}).Should().BeEmpty();
         SceneChoiceMenu.Items(new SceneChoiceMenuFacts {OpenWays = ways, NarrationIsLast = true, Waiting = true}).Should().BeEmpty();
         SceneChoiceMenu.Items(new SceneChoiceMenuFacts {OpenWays = ways, NarrationIsLast = true, Over = true}).Should().BeEmpty();
         SceneChoiceMenu.Items(Fresh(ways)).Should().HaveCount(2);
      }

      // A scene with no way out (every scene written before branches) lists nothing at all, not even "Something else".
      [Test]
      public void GIVEN_a_scene_with_no_open_way_WHEN_listed_THEN_nothing_is_shown()
      {
         SceneChoiceMenu.Items(Fresh()).Should().BeEmpty();
      }

      // A closed way is never listed: before the light is seen, the left path is not offered at all.
      [Test]
      public void GIVEN_a_way_closed_by_its_flag_WHEN_listed_THEN_it_is_not_offered()
      {
         (AuthoredQuest q, AuthoredQuestRun run) = InTheWood();

         SceneChoiceMenu.Items(FromTheWood(run, q)).Select(i => i.Text)
                        .Should().Equal("1. Take the right path down to the river", "2. Turn back", "3. Something else...");
      }

      // A typed "1" is the first OPEN way, the one listed first, not the first the file declares (the closed left path).
      [Test]
      public void GIVEN_the_first_declared_way_closed_WHEN_1_is_typed_THEN_it_sends_the_first_open_way()
      {
         (AuthoredQuest q, AuthoredQuestRun run) = InTheWood();

         SceneChoiceMenu.Read("1", FromTheWood(run, q)).Should().Be("Take the right path down to the river");
         SceneChoiceMenu.Read(" 2. ", FromTheWood(run, q)).Should().Be("Turn back");
      }

      // A number with no line behind it is free text, as is any number among words.
      [Test]
      public void GIVEN_a_number_with_no_way_behind_it_or_among_words_WHEN_typed_THEN_it_goes_as_typed()
      {
         SceneChoiceMenuFacts f = Fresh("Take the left path", "Take the right path");

         SceneChoiceMenu.Read("7", f).Should().Be("7");
         SceneChoiceMenu.Read("0", f).Should().Be("0");
         SceneChoiceMenu.Read("1 and then I run", f).Should().Be("1 and then I run");
         SceneChoiceMenu.Read("I go left", f).Should().Be("I go left");
      }

      // STAKE: Gabriel (26/10/2026) typed 3 for "3. Something else..." and the narrator read "3" as what he did ("You say
      // nothing"); the number of "Something else..." must send nothing and leave the input to him, as a click does.
      [Test]
      public void GIVEN_the_list_shown_WHEN_the_number_of_something_else_is_typed_THEN_nothing_is_sent()
      {
         SceneChoiceMenuFacts f = Fresh("Take the left path", "Take the right path");

         SceneChoiceMenu.Read("3", f).Should().BeNull();
         SceneChoiceMenu.Read(" 3. ", f).Should().BeNull();
      }

      // With no list shown (the player already answered), a typed number is words like any other.
      [Test]
      public void GIVEN_no_list_shown_WHEN_a_number_is_typed_THEN_it_goes_as_typed()
      {
         SceneChoiceMenu.Read("1", new SceneChoiceMenuFacts {OpenWays = new[] {"Take the left path"}, NarrationIsLast = false}).Should().Be("1");
      }

      // The game decides, not the click: picking a way only sends its words. The story moves when the narrator writes the
      // marker; a narration without one leaves the scene where it was.
      [Test]
      public void GIVEN_a_way_clicked_WHEN_its_words_are_sent_THEN_the_story_moves_only_on_the_narrators_marker()
      {
         (AuthoredQuest q, AuthoredQuestRun run) = InTheWood();
         string sent = SceneChoiceMenu.Items(FromTheWood(run, q))[0].Sends;

         SceneMarkers silent = SceneMarkers.Extract("You weigh the river path, but stay where you are. What do you do?");
         foreach (string c in silent.Choices) AuthoredQuestEngine.ChooseInScene(run, q, c);
         run.SceneChoiceId.Should().BeNull();

         SceneMarkers told = SceneMarkers.Extract("You take the path down toward the river. [[choice:right]]");
         foreach (string c in told.Choices) AuthoredQuestEngine.ChooseInScene(run, q, c);

         sent.Should().Be("Take the right path down to the river");
         told.Text.Should().Be("You take the path down toward the river.");
         run.SceneChoiceId.Should().Be("right");
      }
   }
}
