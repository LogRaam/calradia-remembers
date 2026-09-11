// Code written by Gabriel Mailhot, 26/07/2026.
// Documents when CR pays for the SDK's LLM-driven memory compressor (LlmMemoryCompressor). Compression is
// worth an extra LLM call only once an NPC's event history has grown past the threshold, and even then, a
// fruitless pass (the model kept every event) must not immediately re-trigger, or CR would pay for a fresh
// call after every single new memory. RetryGrowth caps that cost at one call per handful of new events.
// 14/08/2026: also pins the isAlive gate added for the player report "memory compression is still being
// carried out for deceased characters" (a dead character is never spoken to again, so compressing its
// memory buys nothing).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MemoryCompressionPolicyTests
    {
        // STAKE: an extra LLM call per conversation for a character with a handful of memories would cost
        // players money for nothing.
        [Test]
        public void GIVEN_event_count_below_threshold_WHEN_never_attempted_THEN_compression_does_not_run()
        {
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = 10, LastAttemptedAtCount = 0, Threshold = MemoryCompressionPolicy.DefaultThreshold}).Should().BeFalse();
        }

        // STAKE: pins the tunable threshold so a later change is deliberate. The boundary is inclusive: the
        // threshold itself already qualifies.
        [Test]
        public void GIVEN_event_count_at_the_threshold_WHEN_never_attempted_THEN_compression_runs()
        {
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = MemoryCompressionPolicy.DefaultThreshold, LastAttemptedAtCount = 0, Threshold = MemoryCompressionPolicy.DefaultThreshold}).Should().BeTrue();
        }

        // STAKE: the reported failure mode this rule exists for, paying for a fresh call after every new
        // memory when the model kept everything and the count did not move.
        [Test]
        public void GIVEN_a_fruitless_attempt_at_the_same_count_WHEN_checking_again_THEN_compression_does_not_re_run()
        {
            int threshold = MemoryCompressionPolicy.DefaultThreshold;

            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = threshold, LastAttemptedAtCount = threshold, Threshold = threshold}).Should().BeFalse();
        }

        // STAKE: memory must keep being contained as the history grows, not give up after one unproductive
        // pass: once enough new events have piled up since the fruitless attempt, retry.
        [Test]
        public void GIVEN_retry_growth_more_events_since_the_fruitless_attempt_WHEN_checking_again_THEN_compression_runs()
        {
            int threshold = MemoryCompressionPolicy.DefaultThreshold;
            int grownCount = threshold + MemoryCompressionPolicy.RetryGrowth;

            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = grownCount, LastAttemptedAtCount = threshold, Threshold = threshold}).Should().BeTrue();
        }

        // What stood here compared the two-argument overload with the three-argument one, "they must not
        // drift" - a test of a delegation rather than of a behaviour. Both overloads are gone (2026-09-10):
        // the threshold is a default on the facts object, so there is no second form left to disagree, and
        // the rule the test was really about is pinned by the threshold cases above.

        // STAKE: a misconfigured tuning value must not turn compression into an every-message LLM call.
        [Test]
        public void GIVEN_a_zero_or_negative_threshold_WHEN_checking_THEN_the_default_threshold_is_used_instead()
        {
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = MemoryCompressionPolicy.DefaultThreshold - 1, LastAttemptedAtCount = 0, Threshold = 0}).Should().BeFalse();
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = MemoryCompressionPolicy.DefaultThreshold - 1, LastAttemptedAtCount = 0, Threshold = -5}).Should().BeFalse();
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = MemoryCompressionPolicy.DefaultThreshold, LastAttemptedAtCount = 0, Threshold = 0}).Should().BeTrue();
        }

        // STAKE: the reported bug itself, an LLM call burned once a day on a hero the player can never
        // talk to again. A dead character's history can be arbitrarily long and never attempted before
        // (the two conditions that would otherwise force compression); isAlive:false must still win.
        [Test]
        public void GIVEN_a_dead_character_WHEN_checking_regardless_of_history_THEN_compression_never_runs()
        {
            int threshold = MemoryCompressionPolicy.DefaultThreshold;

            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = threshold, LastAttemptedAtCount = 0, Threshold = threshold, IsAlive = false}).Should().BeFalse();
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = threshold * 10, LastAttemptedAtCount = 0, Threshold = threshold, IsAlive = false}).Should().BeFalse();
            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts {EventCount = threshold + MemoryCompressionPolicy.RetryGrowth, LastAttemptedAtCount = threshold, Threshold = threshold, IsAlive = false}).Should().BeFalse();
        }

        // STAKE: proves the gate is the ONLY new behaviour: an alive character at the exact same counts
        // that the dead-character test above refuses must still compress exactly as it did before this
        // fix, so the isAlive gate cannot be hiding a change to the existing threshold/retry rules.
        [Test]
        public void GIVEN_an_alive_character_WHEN_checking_at_the_same_counts_THEN_compression_runs_unchanged()
        {
            int threshold = MemoryCompressionPolicy.DefaultThreshold;

            MemoryCompressionPolicy.ShouldCompress(new MemoryCompressionPolicy.MemoryFacts
                {EventCount = threshold, Threshold = threshold, IsAlive = true}).Should().BeTrue();
        }
    }
}
