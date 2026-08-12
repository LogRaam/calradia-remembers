// Code written by Gabriel Mailhot, 12/08/2026.
// Guards the "offered => completable" invariant for open-field bandit-clearing deeds. The viable-quest scanner
// used to offer bandit_clear whenever an anchor town existed, even when no bandit party roamed there, so a lord
// could send the player after bandits that were not present; the player found nothing, reported so, and the giver
// turned hostile (player report). ViableQuestScanner now counts bandit parties within QuestDeedTracker's own
// bandit-clear completion radius of the anchor and asks this policy whether to offer the deed. If this rule
// drifts, either the empty-hunt returns (a deed is offered with no bandits to find) or a legitimate offer is
// suppressed while bandits genuinely roam nearby.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BanditClearOfferPolicyTests
    {
        // The exact bug: zero bandit parties nearby must NOT offer the deed, or the player is sent on the
        // impossible hunt that ended in the giver's hostility.
        [Test]
        public void GIVEN_no_bandits_roam_nearby_WHEN_deciding_THEN_the_deed_is_not_offered()
        {
            BanditClearOfferPolicy.ShouldOffer(0).Should().BeFalse();
        }

        // The mirror case: a real bandit party in range means a real fight the deed can be satisfied by, so the
        // offer must stand. The boundary sits at exactly one party (a single roaming band is enough to clear).
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(9)]
        public void GIVEN_at_least_one_bandit_roams_nearby_WHEN_deciding_THEN_the_deed_is_offered(int count)
        {
            BanditClearOfferPolicy.ShouldOffer(count).Should().BeTrue();
        }

        // STAKE: a defensive miscount (a negative slipping through from a caller bug) must never be read as
        // "bandits present", or a corrupt count could resurrect the empty-hunt this whole rule exists to stop.
        [Test]
        public void GIVEN_a_defensive_negative_count_WHEN_deciding_THEN_the_deed_is_not_offered()
        {
            BanditClearOfferPolicy.ShouldOffer(-1).Should().BeFalse();
        }
    }
}
