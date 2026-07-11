// Code written by Gabriel Mailhot, 10/07/2026.

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
        [Test]
        public void GIVEN_three_participants_from_the_start_WHEN_ordering_a_full_cap_THEN_each_speaks_once_in_turn()
        {
            RoundTablePolicy.SpeakerOrder(3, 0, 3).Should().Equal(0, 1, 2);
        }

        [Test]
        public void GIVEN_two_participants_and_a_larger_cap_WHEN_ordering_THEN_it_wraps_into_cross_talk()
        {
            RoundTablePolicy.SpeakerOrder(2, 0, 3).Should().Equal(0, 1, 0);
        }

        [Test]
        public void GIVEN_four_participants_from_the_start_WHEN_ordering_a_cap_of_three_THEN_only_the_first_three_speak()
        {
            RoundTablePolicy.SpeakerOrder(4, 0, 3).Should().Equal(0, 1, 2);
        }

        [Test]
        public void GIVEN_four_participants_from_the_third_WHEN_ordering_THEN_it_round_robins_and_wraps_to_zero()
        {
            RoundTablePolicy.SpeakerOrder(4, 2, 3).Should().Equal(2, 3, 0);
        }

        [Test]
        public void GIVEN_no_participants_WHEN_ordering_THEN_the_order_is_empty()
        {
            RoundTablePolicy.SpeakerOrder(0, 0, 3).Should().BeEmpty();
        }

        [Test]
        public void GIVEN_a_non_positive_cap_WHEN_ordering_THEN_the_order_is_empty()
        {
            RoundTablePolicy.SpeakerOrder(4, 0, 0).Should().BeEmpty();
        }

        [TestCase(4, 0, 3, 3)]
        [TestCase(4, 2, 3, 1)]
        [TestCase(3, 0, 3, 0)]
        [TestCase(0, 0, 3, 0)]
        public void GIVEN_a_burst_WHEN_computing_the_next_start_THEN_it_advances_so_everyone_eventually_speaks(
            int count, int start, int cap, int expected)
        {
            RoundTablePolicy.NextStartIndex(count, start, cap).Should().Be(expected);
        }
    }
}
