// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BirthLetterPolicyTests
    {
        [Test]
        public void GIVEN_the_mother_rides_with_the_player_WHEN_a_child_is_born_THEN_no_announcement_letter_is_sent()
        {
            BirthLetterPolicy.ShouldSendAnnouncement(true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_mother_is_far_from_the_player_WHEN_a_child_is_born_THEN_a_fond_announcement_is_sent()
        {
            BirthLetterPolicy.ShouldSendAnnouncement(false).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_players_own_spouse_gives_birth_apart_WHEN_deciding_support_THEN_she_never_dun_him()
        {
            BirthLetterPolicy.ShouldRequestChildSupport(false, true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_child_born_outside_marriage_and_the_mother_apart_WHEN_deciding_support_THEN_she_may_ask()
        {
            BirthLetterPolicy.ShouldRequestChildSupport(false, false).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_mother_rides_with_the_player_WHEN_deciding_support_THEN_she_never_writes_for_it()
        {
            // Beside him, whether the child is legitimate or not, no support demand goes out by courier.
            BirthLetterPolicy.ShouldRequestChildSupport(true, false).Should().BeFalse();
            BirthLetterPolicy.ShouldRequestChildSupport(true, true).Should().BeFalse();
        }
    }
}
