// Code written by Gabriel Mailhot, 11/08/2026.
// RetainerSpikeBehavior calls RetainerLeavePolicy.MustLeave from BOTH its WarDeclared hook and its daily safety
// check to decide whether a foreign lord riding in the player's party must break off and return to their own
// clan. These tests pin the STAKE the spike itself was set: the author's flagship rule is that a WAR between the
// retainer's faction and the player forces them to leave, and a dead retainer must never linger as a stale
// tracked id. If this gate is wrong, an enemy lord keeps riding inside the very army they now field against, or
// a corpse stays "in the party" forever - the two exact failures the rule exists to prevent.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RetainerLeavePolicyTests
    {
        // The happy path: a tracked retainer, alive and at peace with the player, stays. Without this the gate
        // could over-fire and dismiss every retainer on the first check, making the whole arrangement useless.
        [Test]
        public void GIVEN_a_tracked_retainer_alive_and_at_peace_WHEN_checked_THEN_they_stay()
        {
            RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = false, IsAlive = true, StillTracked = true})
                .Should().BeFalse();
        }

        // The author's flagship rule and the reason the whole spike exists: the retainer's faction going to war
        // with the player MUST force them out, so an enemy lord can never keep travelling inside the player's army.
        [Test]
        public void GIVEN_the_retainers_faction_is_now_at_war_with_the_player_WHEN_checked_THEN_they_must_leave()
        {
            RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = true, IsAlive = true, StillTracked = true})
                .Should().BeTrue();
        }

        // A dead retainer must be dropped from tracking, never left as a stale id that a later tick keeps trying
        // to observe or release.
        [Test]
        public void GIVEN_a_tracked_retainer_has_died_WHEN_checked_THEN_they_must_leave_even_at_peace()
        {
            RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = false, IsAlive = false, StillTracked = true})
                .Should().BeTrue();
        }

        // Nothing tracked means nothing to end: the answer must be a plain, idempotent false so a stray call on
        // an untracked hero never triggers a spurious release.
        [Test]
        public void GIVEN_the_hero_is_not_tracked_as_a_retainer_WHEN_checked_THEN_nothing_must_happen()
        {
            RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = true, IsAlive = true, StillTracked = false})
                .Should().BeFalse();
        }

        // Even a dead, untracked hero must not report "must leave": the not-tracked short-circuit outranks the
        // death rule, so the policy never asks the behavior to act on someone it is not holding.
        [Test]
        public void GIVEN_a_dead_hero_who_is_not_tracked_WHEN_checked_THEN_still_nothing_must_happen()
        {
            RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = false, IsAlive = false, StillTracked = false})
                .Should().BeFalse();
        }
    }
}
