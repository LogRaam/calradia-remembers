// Code written by Gabriel Mailhot, 13/07/2026.
// Documents what counts as being employed in the player's service. The flagship case is the reported one: a
// companion made GOVERNOR was still treated as idle and took the ennui penalty week after week.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionEngagementPolicyTests
    {
        [Test]
        public void GIVEN_a_companion_made_governor_WHEN_asked_THEN_they_are_NOT_idle()
        {
            // The reported case: a governorship is a charge, not idleness.
            CompanionEngagementPolicy.IsIdle(onErrand: false, isGovernor: true, leadsAParty: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_companion_leading_a_party_of_your_clan_WHEN_asked_THEN_they_are_NOT_idle()
        {
            CompanionEngagementPolicy.IsIdle(onErrand: false, isGovernor: false, leadsAParty: true)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_companion_away_on_an_errand_WHEN_asked_THEN_they_are_NOT_idle()
        {
            CompanionEngagementPolicy.IsIdle(onErrand: true, isGovernor: false, leadsAParty: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_companion_with_no_charge_at_all_WHEN_asked_THEN_they_are_idle()
        {
            CompanionEngagementPolicy.IsIdle(onErrand: false, isGovernor: false, leadsAParty: false)
                .Should().BeTrue();
        }
    }
}
