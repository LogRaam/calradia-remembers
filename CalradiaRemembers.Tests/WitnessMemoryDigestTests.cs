// Code written by Gabriel Mailhot, 23/08/2026.
// What this pins: how much of a witness's own memory a group scene shows. The mod hands each witness's event
// summaries (oldest-first, already compression-folded) to WitnessMemoryDigest.Compose, and the result is the
// "X remembers: ..." line the main speaker's single call reads. If this selection is wrong, a witness either
// forgets salient past (player report: a companion answered as if a prior agreement never happened) or the group
// prompt bloats with every witness's full history. The stake on each test is which of those two it guards.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public class WitnessMemoryDigestTests
    {
        // Under the cap, the witness recalls EVERYTHING, in order: a short history must not be trimmed at all,
        // or a witness with only a few memories would still seem to forget one.
        [Test]
        public void GIVEN_fewer_than_the_cap_WHEN_composing_THEN_all_summaries_are_kept_in_order()
        {
            var result = WitnessMemoryDigest.Compose(new List<string> {"met at Pravend", "agreed to the signal word"}, 8);

            result.Should().Be("met at Pravend; agreed to the signal word");
        }

        // Over the cap, the OLDEST kept line (the compression-folded summary or the first meeting) must survive
        // alongside the most recent: this is the whole point of a compression-aware cap, so the durable past is
        // never the thing that gets dropped when a witness has a long history.
        [Test]
        public void GIVEN_more_than_the_cap_WHEN_composing_THEN_the_oldest_and_the_most_recent_are_kept_and_a_middle_one_is_dropped()
        {
            var summaries = new List<string> {"OLDEST folded summary", "b", "c", "d", "NEWEST"};

            string result = WitnessMemoryDigest.Compose(summaries, 3);

            result.Should().Contain("OLDEST folded summary"); // the folded past is never dropped
            result.Should().Contain("NEWEST");                 // the recent is kept
            result.Should().NotContain("; c;");                // a middle memory is what gives way
            result.Should().Be("OLDEST folded summary; d; NEWEST");
        }

        // Blank or whitespace summaries are extraction noise; letting them through would waste cap slots and
        // print empty "; ;" gaps in the recall line.
        [Test]
        public void GIVEN_blank_summaries_WHEN_composing_THEN_they_are_ignored()
        {
            var result = WitnessMemoryDigest.Compose(new List<string> {" ", "real memory", ""}, 8);

            result.Should().Be("real memory");
        }

        // No content means no recall line at all: the prompt must not mint an empty "X remembers:" (it would read
        // as invented history), so the composer returns null for null, empty, or all-blank input.
        [Test]
        public void GIVEN_no_real_content_WHEN_composing_THEN_it_returns_null()
        {
            WitnessMemoryDigest.Compose(null, 8).Should().BeNull();
            WitnessMemoryDigest.Compose(new List<string>(), 8).Should().BeNull();
            WitnessMemoryDigest.Compose(new List<string> {" ", ""}, 8).Should().BeNull();
        }

        // A non-positive cap is a caller error (or a mis-set tuning value); it must never throw mid-turn nor
        // return a partial line, it simply yields no recall.
        [Test]
        public void GIVEN_a_non_positive_cap_WHEN_composing_THEN_it_returns_null()
        {
            WitnessMemoryDigest.Compose(new List<string> {"a", "b"}, 0).Should().BeNull();
        }
    }
}
