// Code written by Gabriel Mailhot, 04/08/2026.
// follow_me's start gate: EncounterContextBuilder.Build and FollowMeBehavior.TryStart both call
// EscortEligibilityPolicy.CanEscort with the SAME four facts, so the prompt teaching and the bridge's own
// re-check can never quietly diverge ("the prompt is advice, the bridge is law"). These tests pin every guard
// combination: a lord who does not lead his own party (a companion already riding IN the player's party) has
// nothing further for follow_me to do; a lord at war with the player is not about to ride alongside the very
// army he may soon fight; a lord bound to an army or a siege would have his party reclaimed by his own AI the
// moment the escort began, making it an unkeepable promise; and a lord already escorting cannot be double-booked.

#region

using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Escort
{
    [TestFixture]
    public sealed class EscortEligibilityPolicyTests
    {
        // The baseline every other test perturbs one guard away from: with every fact clean, the escort must
        // be allowed, or a player who did everything right could never get the promise honoured.
        [Test]
        public void GIVEN_every_guard_clear_WHEN_checked_THEN_the_escort_is_allowed()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: true, atWar: false, inArmy: false, alreadyEscorting: false)
                .Should().BeTrue();
        }

        // A companion already riding IN the player's own party (or a wanderer with no party at all) does not
        // "lead" one - follow_me has nothing to add for them, so this must refuse rather than silently no-op.
        [Test]
        public void GIVEN_the_npc_does_not_lead_their_own_party_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: false, atWar: false, inArmy: false, alreadyEscorting: false)
                .Should().BeFalse();
        }

        // An enemy lord pledging to ride alongside the very army he may soon fight would read as an
        // impossible fiction - war overrides every other fact.
        [Test]
        public void GIVEN_the_npc_is_at_war_with_the_player_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: true, atWar: true, inArmy: false, alreadyEscorting: false)
                .Should().BeFalse();
        }

        // A lord bound to an army or besieging a settlement has his party's movement claimed by that duty; his
        // own AI would reclaim it the instant the escort started, so the promise could never actually hold.
        [Test]
        public void GIVEN_the_npc_is_bound_to_an_army_or_a_siege_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: true, atWar: false, inArmy: true, alreadyEscorting: false)
                .Should().BeFalse();
        }

        // One escort at a time per lord: a second follow_me on an already-escorting lord must not stack or
        // restart the term unnoticed.
        [Test]
        public void GIVEN_the_npc_is_already_escorting_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: true, atWar: false, inArmy: false, alreadyEscorting: true)
                .Should().BeFalse();
        }
    }
}
