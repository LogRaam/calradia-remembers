// Code written by Gabriel Mailhot, 18/11/2026.
// SceneTranscriptWindow chooses the "scene so far" a tale's narrator reads each turn. Schmidt (Discord, 18/11/2026): with
// only the last 24 lines, the narrator forgot by turn 109 the spear Gunjadrid carried and the girl hidden in the log. If
// this is wrong, a long scene forgets what happened in it, or the prompt grows without end.

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
   public sealed class SceneTranscriptWindowTests
   {
      private static List<string> Scene(int turns, int narrationChars)
      {
         var lines = new List<string>();
         for (var i = 0; i < turns; i++)
         {
            lines.Add($"You: turn {i}, I do something.");
            lines.Add("(narrator) " + new string('n', narrationChars) + $" end of turn {i}");
         }

         return lines;
      }

      // STAKE (Schmidt): a scene that fits is read whole: twenty-five turns back is still remembered.
      [Test]
      public void GIVEN_a_scene_within_the_budget_WHEN_composed_THEN_every_line_is_kept()
      {
         List<string> lines = Scene(30, 1200);

         SceneTranscriptWindow.Compose(lines).Should().Equal(lines);
      }

      // STAKE: a scene past the budget never swells the prompt past it, and the newest lines come whole.
      [Test]
      public void GIVEN_a_scene_past_the_budget_WHEN_composed_THEN_the_newest_lines_come_whole_within_it()
      {
         List<string> lines = Scene(200, 1500);

         IReadOnlyList<string> shown = SceneTranscriptWindow.Compose(lines);

         shown.Sum(l => l.Length + 1).Should().BeLessThanOrEqualTo(SceneTranscriptWindow.DefaultBudgetChars + SceneTranscriptWindow.OlderPlayerLinesChars + 200);
         shown.Last().Should().Be(lines.Last());
      }

      // STAKE: past the budget, what the player did earlier stays (their own words), and the model is told the narration of it was left out.
      [Test]
      public void GIVEN_a_scene_past_the_budget_WHEN_composed_THEN_the_players_older_lines_stay_and_older_narration_goes()
      {
         List<string> lines = Scene(200, 1500);

         IReadOnlyList<string> shown = SceneTranscriptWindow.Compose(lines);

         shown.Should().Contain("You: turn 150, I do something.");
         shown.Should().NotContain(l => l.EndsWith("end of turn 150"));
         shown[0].Should().Contain("narration of it is left out");
      }

      // STAKE: one enormous last narration is still shown: the narrator must always read the turn it answers.
      [Test]
      public void GIVEN_a_last_line_larger_than_the_budget_WHEN_composed_THEN_it_is_still_shown()
      {
         var lines = new List<string> {"You: hello", new string('x', 100)};

         SceneTranscriptWindow.Compose(lines, 50).Last().Should().Be(lines.Last());
      }
   }
}
