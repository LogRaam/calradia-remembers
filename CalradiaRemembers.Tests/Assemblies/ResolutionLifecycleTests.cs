// Code written by Gabriel Mailhot, 20/07/2026.
// Documents the assembly-motion transaction model: what is provisional, what can be cancelled, and what actually
// commits when the assembly is lifted. The cancellation path is the one that MUST be solid: a player who takes
// back a decision mid-council must leave no trace in the world, because nothing was applied before the lift.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class ResolutionLifecycleTests
    {
        // A freshly proposed motion is provisional: it has not touched the world, so it can still be withdrawn.
        // If this were false the roster panel would show a motion as binding before anyone consented to it.
        [Test]
        public void GIVEN_a_proposed_motion_WHEN_asked_THEN_it_is_provisional_and_can_be_reversed()
        {
            ResolutionLifecycle.IsProvisional(ResolutionStatus.Proposed).Should().BeTrue();
            ResolutionLifecycle.CanReverse(ResolutionStatus.Proposed).Should().BeTrue();
        }

        // The heart of the transaction model: a motion the council has AGREED to is still provisional until the
        // lift, so the player can change his mind and cancel it. If an agreed motion could not be reversed, the
        // only way to undo a decision would be to actually unwind a real world effect, which is exactly the
        // fragility commit-on-lift exists to avoid.
        [Test]
        public void GIVEN_an_agreed_motion_WHEN_asked_before_the_lift_THEN_it_is_still_provisional_and_reversible()
        {
            ResolutionLifecycle.IsProvisional(ResolutionStatus.Agreed).Should().BeTrue();
            ResolutionLifecycle.CanReverse(ResolutionStatus.Agreed).Should().BeTrue();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Agreed, ResolutionStatus.Reversed).Should().BeTrue();
        }

        // A cancelled motion is a dead end: it must never come back to life or slip through to commit at the lift.
        // Re-raising a withdrawn idea is a NEW motion, not a resurrection of this one.
        [Test]
        public void GIVEN_a_reversed_motion_WHEN_asked_THEN_it_is_terminal_and_cannot_transition_anywhere()
        {
            ResolutionLifecycle.IsTerminal(ResolutionStatus.Reversed).Should().BeTrue();
            ResolutionLifecycle.CanReverse(ResolutionStatus.Reversed).Should().BeFalse();
            ResolutionLifecycle.CommitsAtLift(ResolutionStatus.Reversed).Should().BeFalse();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Reversed, ResolutionStatus.Agreed).Should().BeFalse();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Reversed, ResolutionStatus.Applied).Should().BeFalse();
        }

        // The commit gate: only an AGREED motion commits when the assembly is lifted. A motion left merely proposed
        // (floated but never consented to) must NOT touch the world, or the player would suffer effects he never
        // signed off on. This, with the reversal rule, is what guarantees "nothing applies until the lift".
        [Test]
        public void GIVEN_the_lift_WHEN_deciding_what_commits_THEN_only_agreed_motions_do()
        {
            ResolutionLifecycle.CommitsAtLift(ResolutionStatus.Agreed).Should().BeTrue();
            ResolutionLifecycle.CommitsAtLift(ResolutionStatus.Proposed).Should().BeFalse();
            ResolutionLifecycle.CommitsAtLift(ResolutionStatus.Reversed).Should().BeFalse();
        }

        // Nothing commits mid-session: a proposed motion cannot jump straight to an applied world effect. The only
        // path to Applied/Failed is through Agreed, and that step is taken once, at the lift, by the bridge.
        [Test]
        public void GIVEN_a_proposed_motion_WHEN_it_tries_to_commit_directly_THEN_the_transition_is_illegal()
        {
            ResolutionLifecycle.CanTransition(ResolutionStatus.Proposed, ResolutionStatus.Applied).Should().BeFalse();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Proposed, ResolutionStatus.Failed).Should().BeFalse();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Proposed, ResolutionStatus.Agreed).Should().BeTrue();
        }

        // At the lift an agreed motion resolves to exactly one honest outcome: Applied when the bridge ran the
        // effect, Failed when it could not. Both are terminal so the verification panel shows a settled result,
        // never a status that keeps drifting.
        [Test]
        public void GIVEN_an_agreed_motion_WHEN_committed_at_the_lift_THEN_it_ends_as_applied_or_failed_and_stops()
        {
            ResolutionLifecycle.CanTransition(ResolutionStatus.Agreed, ResolutionStatus.Applied).Should().BeTrue();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Agreed, ResolutionStatus.Failed).Should().BeTrue();
            ResolutionLifecycle.IsTerminal(ResolutionStatus.Applied).Should().BeTrue();
            ResolutionLifecycle.IsTerminal(ResolutionStatus.Failed).Should().BeTrue();
            ResolutionLifecycle.CanTransition(ResolutionStatus.Applied, ResolutionStatus.Reversed).Should().BeFalse();
        }

        // A committed outcome is beyond recall: once the bridge applied a real effect at the lift, "cancel" is no
        // longer free, so the model refuses to reverse it. Undoing a real effect is a separate deed, not a council
        // withdrawal.
        [Test]
        public void GIVEN_an_applied_or_failed_motion_WHEN_asked_to_reverse_THEN_it_refuses()
        {
            ResolutionLifecycle.CanReverse(ResolutionStatus.Applied).Should().BeFalse();
            ResolutionLifecycle.CanReverse(ResolutionStatus.Failed).Should().BeFalse();
        }
    }
}
