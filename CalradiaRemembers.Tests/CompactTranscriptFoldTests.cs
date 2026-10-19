// Code written by Gabriel Mailhot, 16/09/2026.
// What these pin: that a Compact conversation folds its raw transcript sooner than a Full one.
//
// DuskSymphony, on an 8,192-token local server (Nexus, 14/09/2026): "occasionally one will get through the
// first message but followups will still be too high."
//
// That sentence is the diagnosis. The system prompt is the same size on turn one and turn five; what grows
// between them is the raw transcript. It folded at fourteen unfolded lines, and that threshold knew nothing
// about Compact — so the one part of the prompt that GROWS was the one part Compact had never shrunk, and a
// player whose first message fit could not have a conversation.
//
// If these fail, a player on a small local model gets one exchange and then a wall.

#region

using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompactTranscriptFoldTests
    {
        // The dial itself. Six rather than four because folding costs an LLM call of its own, and on the small
        // servers this exists for, that call is not free either.
        [Test]
        public void GIVEN_compact_mode_WHEN_the_fold_threshold_is_asked_for_THEN_it_is_tighter_than_the_shipped_one()
        {
            ConversationCompressionPolicy.ThresholdFor(true)
                .Should().BeLessThan(ConversationCompressionPolicy.ThresholdFor(false));

            ConversationCompressionPolicy.ThresholdFor(false)
                .Should().Be(ConversationCompressionPolicy.UnfoldedLineThreshold, "Full keeps the shipped pacing exactly");
        }

        // THE REPORT, as a transcript: a conversation that has run long enough to break a small context folds
        // under Compact, and the same conversation is left raw under Full. Both answers are right for their
        // own budget, which is the whole point of having a dial.
        [Test]
        public void GIVEN_a_conversation_of_eight_lines_WHEN_planned_THEN_compact_folds_it_and_full_leaves_it_raw()
        {
            Plan(8, compact: true).ShouldFold.Should().BeTrue("eight raw lines is already too much for an 8k window");
            Plan(8, compact: false).ShouldFold.Should().BeFalse("a large model keeps the verbatim exchange, which reads better");
        }

        // The fold must not eat the immediate context: the newest lines stay verbatim at BOTH levels, or the
        // character answers a recap of what the player just said instead of the words themselves.
        [Test]
        public void GIVEN_compact_mode_WHEN_a_long_conversation_folds_THEN_the_newest_lines_are_still_left_raw()
        {
            ConversationFoldPlan plan = Plan(12, compact: true);

            plan.ShouldFold.Should().BeTrue();
            plan.FoldTo.Should().BeLessThan(12, "the most recent lines are never folded");
            (12 - plan.FoldTo).Should().BeGreaterThanOrEqualTo(ConversationCompressionPolicy.RecentLinesKeptVerbatim);
        }

        // A short exchange is left alone at every level: folding costs an LLM call, and paying it for four
        // lines would be a worse deal on a small server than the lines themselves.
        [Test]
        public void GIVEN_a_conversation_barely_started_WHEN_planned_THEN_neither_level_folds_it()
        {
            Plan(3, compact: true).ShouldFold.Should().BeFalse();
            Plan(3, compact: false).ShouldFold.Should().BeFalse();
        }

        #region private

        private static ConversationFoldPlan Plan(int lines, bool compact)
            => ConversationCompressionPolicy.Plan(new ConversationCompressionPolicy.TranscriptFold {
                TotalLines = lines,
                SummarizedThrough = 0,
                Threshold = ConversationCompressionPolicy.ThresholdFor(compact)
            });

        #endregion
    }
}
