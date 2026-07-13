// Code written by Gabriel Mailhot, 22/06/2026.
// Guards the freedom-bargain branching: a held lord is freed on the spot, another's captive is a rescue
// deed, a non-prisoner gets nothing, and only a lord may bargain at all.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerReleasePolicyTests
    {
        // Only a captive of real standing can offer terms for their own release; the gate needs BOTH facts
        // true at once, not either one alone.
        [Test]
        public void GIVEN_a_captive_lord_WHEN_asked_THEN_they_may_bargain_for_freedom()
        {
            PrisonerReleasePolicy.CanBargainForFreedom(isLord: true, isPrisoner: true).Should().BeTrue();
        }

        // Neither half of the gate is enough alone: a commoner has no standing to bargain even when held, and
        // a lord who is not actually a prisoner has nothing to bargain out of.
        [Test]
        public void GIVEN_a_commoner_or_a_free_lord_WHEN_asked_THEN_they_may_not()
        {
            PrisonerReleasePolicy.CanBargainForFreedom(isLord: false, isPrisoner: true).Should().BeFalse();
            PrisonerReleasePolicy.CanBargainForFreedom(isLord: true, isPrisoner: false).Should().BeFalse();
        }

        // The player's own prisoner is freed THIS turn (the free_prisoner action): it must never be routed
        // through a rescue deed, which would send the player to go "free" someone already in their dungeon.
        [Test]
        public void GIVEN_the_players_own_captive_WHEN_classifying_THEN_it_is_an_immediate_release()
        {
            PrisonerReleasePolicy.Classify(isPrisoner: true, heldByPlayer: true)
                .Should().Be(ReleaseBargainMode.ImmediateRelease);
        }

        // A captive held by SOMEONE ELSE cannot be freed on the spot; per the CHANGELOG this must become a
        // deed paid only on a real field release, never on a mere promise made in conversation.
        [Test]
        public void GIVEN_a_captive_held_by_another_lord_WHEN_classifying_THEN_it_is_a_rescue_deed()
        {
            PrisonerReleasePolicy.Classify(isPrisoner: true, heldByPlayer: false)
                .Should().Be(ReleaseBargainMode.RescueDeed);
        }

        // A free person must read as None regardless of the (meaningless, contradictory) heldByPlayer flag:
        // the isPrisoner check has to dominate, not be overridden by custody bookkeeping.
        [Test]
        public void GIVEN_someone_who_is_not_a_prisoner_WHEN_classifying_THEN_there_is_no_bargain()
        {
            PrisonerReleasePolicy.Classify(isPrisoner: false, heldByPlayer: false)
                .Should().Be(ReleaseBargainMode.None);
            PrisonerReleasePolicy.Classify(isPrisoner: false, heldByPlayer: true)
                .Should().Be(ReleaseBargainMode.None);
        }
    }
}
