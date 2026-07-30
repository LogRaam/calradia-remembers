// Code written by Gabriel Mailhot, 30/07/2026.
// This policy decides WHEN a running conversation's older lines get folded into a compressed summary, and WHICH
// lines. It exists because a long exchange (a council of several members, or any talk that runs on) must keep
// every turn coherent with the whole conversation without replaying an ever-growing RAW transcript, which
// inflates every prompt and can overflow a small or reasoning model (Gabriel 2026-07-30). If the fold triggers
// too eagerly it burns LLM calls on short talks; too late and the transcript is already overflowing. If the
// "recent kept" boundary is wrong, either a just-said line vanishes into a lossy summary (incoherence, the exact
// thing this feature prevents) or nothing is ever folded. Each test pins one of those boundaries.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConversationCompressionPolicyTests
    {
        // STAKE: a short conversation must stay whole and cost nothing extra. Folding a handful of lines would
        // spend an LLM call and lose verbatim nuance for no benefit, so below the threshold it must decline.
        [Test]
        public void GIVEN_an_unfolded_count_at_or_below_the_threshold_WHEN_planning_THEN_nothing_is_folded()
        {
            ConversationCompressionPolicy.Plan(totalLines: 10, summarizedThrough: 0, threshold: 14, recentKept: 4)
                .ShouldFold.Should().BeFalse();
        }

        // STAKE: once the raw history has piled up past the threshold, fold everything except the most recent
        // lines. Without this the transcript grows unbounded and eventually overflows the model, which is the
        // whole reason the feature exists.
        [Test]
        public void GIVEN_the_unfolded_count_exceeds_the_threshold_WHEN_planning_THEN_the_older_range_is_folded()
        {
            ConversationFoldPlan plan =
                ConversationCompressionPolicy.Plan(totalLines: 20, summarizedThrough: 0, threshold: 14, recentKept: 4);

            plan.ShouldFold.Should().BeTrue();
            plan.FoldFrom.Should().Be(0);
            plan.FoldTo.Should().Be(16); // 20 total minus the 4 most recent kept verbatim
        }

        // STAKE: the immediate context must stay verbatim. The last few lines (a just-asked question, a fresh
        // reply) are never folded into the lossy summary, so the next speaker answers the ACTUAL recent words,
        // not a paraphrase. This is the coherence guarantee.
        [Test]
        public void GIVEN_a_fold_WHEN_planning_THEN_exactly_the_recent_kept_lines_stay_out_of_the_fold()
        {
            ConversationFoldPlan plan =
                ConversationCompressionPolicy.Plan(totalLines: 30, summarizedThrough: 0, threshold: 14, recentKept: 4);

            (plan.FoldTo).Should().Be(26);
            (30 - plan.FoldTo).Should().Be(4); // the 4 newest lines are replayed raw, not summarized
        }

        // STAKE: a second fold must resume from where the first left off, not re-fold what is already summarized
        // (which would double-count it and waste the call). The fold starts at the prior SummarizedThrough.
        [Test]
        public void GIVEN_some_lines_already_folded_WHEN_planning_THEN_the_fold_resumes_from_that_point()
        {
            ConversationFoldPlan plan =
                ConversationCompressionPolicy.Plan(totalLines: 40, summarizedThrough: 16, threshold: 14, recentKept: 4);

            plan.ShouldFold.Should().BeTrue();
            plan.FoldFrom.Should().Be(16); // resume, do not re-fold [0,16)
            plan.FoldTo.Should().Be(36);
        }

        // STAKE: if everything past the last summary is within the "recent kept" window, there is nothing new to
        // fold, so it must decline even when the total is large. Otherwise it would try to fold an empty range.
        [Test]
        public void GIVEN_all_lines_beyond_the_summary_are_within_the_recent_window_WHEN_planning_THEN_nothing_is_folded()
        {
            // 18 total, 15 already folded, keep 4 recent: only lines [15,18) remain, all inside the recent window.
            ConversationCompressionPolicy.Plan(totalLines: 18, summarizedThrough: 15, threshold: 2, recentKept: 4)
                .ShouldFold.Should().BeFalse();
        }
    }
}
