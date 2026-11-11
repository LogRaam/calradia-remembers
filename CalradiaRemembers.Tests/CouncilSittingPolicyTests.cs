// Code written by Gabriel Mailhot, 10/11/2026.
// What CouncilSittingPolicy decides: what a war council still has of its own sitting, the player's words included. Gabriel
// (10/11/2026): "on fait ... la memoire du conseil". If it is wrong, a table forgets what the player said a few turns ago
// (it showed the last 24 lines and dropped the rest, the player's lines never among them), or a recap call is spent every
// turn, or a failed recap lets the sitting grow without bound in every prompt.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CouncilSittingPolicyTests
    {
        // STAKE: the report: the table must read what the player said, under "You", beside the seats and the scene.
        [Test]
        public void GIVEN_a_sitting_WHEN_its_lines_are_shown_THEN_the_players_words_seats_and_scene_are_named()
        {
            var t = new ConversationTranscript();
            t.Record(new TranscriptLine {IsPlayer = true, Text = "We march on Hvalvik at dawn."});
            t.Record(new TranscriptLine {SpeakerName = "Ira", Text = "The men are tired."});
            t.Record(new TranscriptLine {IsNarration = true, Text = "A gust shakes the tent."});

            CouncilSittingPolicy.ShownLines(t).Should().Equal("You: We march on Hvalvik at dawn.", "Ira: The men are tired.", "(scene): A gust shakes the tent.");
        }

        // STAKE: once folded, the recap carries the older part and only the lines after it are shown whole, never both.
        [Test]
        public void GIVEN_a_folded_sitting_WHEN_its_lines_are_shown_THEN_only_the_lines_after_the_recap_are_whole()
        {
            var t = Sitting(30);
            t.ApplySummary("The player vowed to take Hvalvik.", 20);

            CouncilSittingPolicy.Recap(t).Should().Be("The player vowed to take Hvalvik.");
            CouncilSittingPolicy.ShownLines(t).Should().HaveCount(10).And.StartWith("Ira: line 20");
        }

        // STAKE: a recap call that failed must not let the sitting grow in every prompt: the window stays what it was.
        [Test]
        public void GIVEN_a_long_sitting_never_folded_WHEN_its_lines_are_shown_THEN_at_most_the_old_window_is()
        {
            var t = Sitting(60);

            CouncilSittingPolicy.Recap(t).Should().BeNull();
            CouncilSittingPolicy.ShownLines(t).Should().HaveCount(CouncilSittingPolicy.MaxShown).And.EndWith("Ira: line 59");
        }

        // STAKE: a short sitting costs no recap call; past the old window it folds, keeping about two turns whole.
        [Test]
        public void GIVEN_a_sitting_WHEN_the_fold_is_planned_THEN_it_waits_for_the_window_and_keeps_the_last_turns_whole()
        {
            CouncilSittingPolicy.Plan(CouncilSittingPolicy.FoldThreshold, 0).ShouldFold.Should().BeFalse();

            ConversationFoldPlan plan = CouncilSittingPolicy.Plan(CouncilSittingPolicy.FoldThreshold + 1, 0);

            plan.ShouldFold.Should().BeTrue();
            plan.FoldFrom.Should().Be(0);
            plan.FoldTo.Should().Be(CouncilSittingPolicy.FoldThreshold + 1 - CouncilSittingPolicy.RecentKept);
        }

        private static ConversationTranscript Sitting(int lines)
        {
            var t = new ConversationTranscript();
            foreach (int i in Enumerable.Range(0, lines)) t.Record(new TranscriptLine {SpeakerName = "Ira", Text = $"line {i}"});

            return t;
        }
    }
}
