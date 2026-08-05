// Code written by Gabriel Mailhot, 04/08/2026.
// end_mercenary fix (player report, 2026-08-04): "The King agreed to free me from my mercenary contract.
// Nothing happens." Verifies the ONE guard the bridge consults before dissolving a mercenary contract, so
// the fix cannot regress into the same class of bug it closes: a promised deed that silently does nothing.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MercenaryReleasePolicyTests
    {
        // The ordinary happy path this whole feature exists for: a King (or the player) agrees to end a
        // REAL contract, and the bridge must be allowed to actually dissolve it. Without this, the fix would
        // have no reachable success case at all.
        [Test]
        public void GIVEN_the_player_is_under_mercenary_service_WHEN_resolving_end_mercenary_THEN_the_release_is_allowed()
        {
            MercenaryReleasePolicy.CanEnd(isUnderMercenaryService: true).Should().BeTrue();
        }

        // STAKE: the player report was "nothing happens", an empty promise. The fix must not trade one
        // silent no-op for another: when the clan is NOT actually under mercenary service, the bridge must
        // REFUSE (ActionOutcome.Refused, an in-character note the model sees), never swallow the action as
        // if nothing was asked. This test pins the fact this refusal branches on.
        [Test]
        public void GIVEN_the_player_is_not_under_mercenary_service_WHEN_resolving_end_mercenary_THEN_the_release_is_refused()
        {
            MercenaryReleasePolicy.CanEnd(isUnderMercenaryService: false).Should().BeFalse();
        }
    }
}
