// Code written by Gabriel Mailhot, 06/08/2026.
// join_as_mercenary war-check (v1.34.2 governance hardening, action-system audit): the join_as_mercenary executor
// enrolled the player's clan under a kingdom's banner with no check on whether the player was AT WAR with that very
// kingdom, so a player actively fighting a realm could sign on as its hired swords - a state the fiction can never
// justify. This verifies the two facts both the prompt teaching (ResolveMercenaryOfferKingdom) and the bridge's
// execution guard (ExecuteJoinAsMercenary) consult, so a refused enrolment is surfaced, never a silent no-op.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MercenaryEnlistmentPolicyTests
    {
        // The ordinary happy path: a clan free of any kingdom obligation, not at war with the hiring realm, may
        // take service. Without this the fix would have no reachable success case and the verb would never fire.
        [Test]
        public void GIVEN_the_player_is_free_and_not_at_war_WHEN_resolving_join_as_mercenary_THEN_service_is_allowed()
        {
            MercenaryEnlistmentPolicy.CanJoin(playerFreeOfKingdom: true, playerAtWarWithKingdom: false).Should().BeTrue();
        }

        // STAKE (finding 4): the audit found a player could enrol as mercenary of a kingdom they were actively
        // fighting. A realm you are at war with does not take you into its pay; even a clan otherwise free must be
        // refused while the war stands, or the executor would sign a contract with the very enemy it is fighting.
        [Test]
        public void GIVEN_the_player_is_at_war_with_the_kingdom_WHEN_resolving_join_as_mercenary_THEN_service_is_refused()
        {
            MercenaryEnlistmentPolicy.CanJoin(playerFreeOfKingdom: true, playerAtWarWithKingdom: true).Should().BeFalse();
        }

        // STAKE: a clan already sworn or already under contract settles that obligation first (the bridge's own
        // "already sworn" refusal depends on this fact being false). Pins that a non-free clan is refused even
        // when there is no war, so the free-of-kingdom guard is not dead code.
        [Test]
        public void GIVEN_the_player_is_not_free_of_a_kingdom_WHEN_resolving_join_as_mercenary_THEN_service_is_refused()
        {
            MercenaryEnlistmentPolicy.CanJoin(playerFreeOfKingdom: false, playerAtWarWithKingdom: false).Should().BeFalse();
        }
    }
}
