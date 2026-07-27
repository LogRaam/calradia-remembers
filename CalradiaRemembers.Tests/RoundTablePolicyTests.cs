// Code written by Gabriel Mailhot, 10/07/2026.
// Documents the pure round-robin arithmetic a round-table burst runs on: which participant indices get a turn
// this burst (SpeakerOrder), and where the next burst should resume (NextStartIndex) so that, over repeated
// triggers, everyone in a group larger than the per-burst cap eventually speaks instead of the same few voices
// answering every time. Consumed by the mod's RoundTableController.RunAsync, which spawns one LLM turn per
// returned index. Get the wrap/advance math wrong and either a turn indexes past the participant list, or the
// rotation stalls and never reaches participants beyond the cap.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RoundTablePolicyTests
    {
        // Baseline: cap matches the group size exactly, so a straight 0..N-1 order gives every participant one turn.
        [Test]
        public void GIVEN_three_participants_from_the_start_WHEN_ordering_a_full_cap_THEN_each_speaks_once_in_turn()
        {
            RoundTablePolicy.SpeakerOrder(3, 0, 3).Should().Equal(0, 1, 2);
        }

        // Per the method's own doc comment: when participantCount is less than cap the index wraps, and this is
        // INTENDED (produces back-and-forth cross-talk), not a bug to guard against.
        [Test]
        public void GIVEN_two_participants_and_a_larger_cap_WHEN_ordering_THEN_it_wraps_into_cross_talk()
        {
            RoundTablePolicy.SpeakerOrder(2, 0, 3).Should().Equal(0, 1, 0);
        }

        // DefaultCap (3) caps every burst even when more NPCs are present: a fourth participant here is
        // deliberately left out this round, picked up on a later trigger via NextStartIndex.
        [Test]
        public void GIVEN_four_participants_from_the_start_WHEN_ordering_a_cap_of_three_THEN_only_the_first_three_speak()
        {
            RoundTablePolicy.SpeakerOrder(4, 0, 3).Should().Equal(0, 1, 2);
        }

        // The rotation must wrap at the participant count, not run off the end: starting near the tail of a
        // 4-person group and asking for 3 turns has to land back at index 0 rather than index out of range.
        [Test]
        public void GIVEN_four_participants_from_the_third_WHEN_ordering_THEN_it_round_robins_and_wraps_to_zero()
        {
            RoundTablePolicy.SpeakerOrder(4, 2, 3).Should().Equal(2, 3, 0);
        }

        // Defensive: no participants present must not throw or divide by zero (Normalize takes a modulo of count).
        [Test]
        public void GIVEN_no_participants_WHEN_ordering_THEN_the_order_is_empty()
        {
            RoundTablePolicy.SpeakerOrder(0, 0, 3).Should().BeEmpty();
        }

        // Defensive: a zero or negative cap must produce no turns rather than an unbounded or malformed lap.
        [Test]
        public void GIVEN_a_non_positive_cap_WHEN_ordering_THEN_the_order_is_empty()
        {
            RoundTablePolicy.SpeakerOrder(4, 0, 0).Should().BeEmpty();
        }

        // This is the fairness guarantee the method's doc comment promises: the next burst resumes exactly where
        // this one left off, so a group bigger than the cap is not left with the same few voices heard every
        // single trigger. Includes the wrap case (start+cap lands past the roster) and the empty-roster guard.
        [TestCase(4, 0, 3, 3)]
        [TestCase(4, 2, 3, 1)]
        [TestCase(3, 0, 3, 0)]
        [TestCase(0, 0, 3, 0)]
        public void GIVEN_a_burst_WHEN_computing_the_next_start_THEN_it_advances_so_everyone_eventually_speaks(
            int count, int start, int cap, int expected)
        {
            RoundTablePolicy.NextStartIndex(count, start, cap).Should().Be(expected);
        }

        // Council audit M12: the per-member council used to give the whole roster a turn EVERY message (one full
        // LLM call each), so a large clan council cost a dozen calls per line. A small/medium council still speaks
        // in full (nothing changes there); only a large one is capped, and the round-robin start advances between
        // turns so everyone is still heard over successive turns. Boundary at exactly the cap stays uncapped.
        [TestCase(3, 3)]   // small council: whole table speaks
        [TestCase(6, 6)]   // exactly at the cap: still whole table
        [TestCase(10, 6)]  // large council: bounded to the cap
        [TestCase(0, 0)]   // degenerate: nothing to seat
        public void GIVEN_a_council_roster_WHEN_capping_the_lap_THEN_small_tables_speak_in_full_and_large_ones_are_bounded(
            int rosterCount, int expected)
        {
            RoundTablePolicy.CouncilLapCap(rosterCount).Should().Be(expected);
        }
    }
}
