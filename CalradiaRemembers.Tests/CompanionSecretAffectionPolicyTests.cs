// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionSecretAffectionPolicyTests
    {
        [Test]
        public void GIVEN_all_conditions_hold_WHEN_checking_eligibility_THEN_the_companion_could_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, true, true, true, false, 60).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_hero_is_not_a_companion_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(false, true, true, true, false, 60).Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_incompatible_orientation_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, false, true, true, false, 60).Should().BeFalse();
        }

        [Test]
        public void GIVEN_adult_content_is_disabled_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, true, false, true, false, 60).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_companion_is_not_content_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, true, true, false, false, 60).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_companion_is_already_attached_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, true, true, true, true, 60).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_bond_is_below_the_minimum_regard_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, true, true, true, false, 49).Should().BeFalse();
        }

        [TestCase(50, 5)]
        [TestCase(60, 25)]
        [TestCase(67, 39)]
        [TestCase(68, 40)]
        [TestCase(100, 40)]
        [TestCase(40, 5)]
        public void GIVEN_a_regard_WHEN_computing_the_weekly_chance_THEN_it_scales_and_clamps(int regard, int expected)
        {
            CompanionSecretAffectionPolicy.WeeklyChancePercent(regard).Should().Be(expected);
        }

        [TestCase(0, true)]
        [TestCase(24, true)]
        [TestCase(25, false)]
        [TestCase(99, false)]
        public void GIVEN_regard_sixty_with_a_chance_of_twenty_five_WHEN_rolling_THEN_only_the_bottom_quarter_confides(int roll, bool confides)
        {
            CompanionSecretAffectionPolicy.ShouldConfideThisWeek(60, roll).Should().Be(confides);
        }

        [Test]
        public void GIVEN_a_negative_roll_WHEN_deciding_THEN_the_companion_does_not_confide()
        {
            CompanionSecretAffectionPolicy.ShouldConfideThisWeek(60, -1).Should().BeFalse();
        }
    }
}
