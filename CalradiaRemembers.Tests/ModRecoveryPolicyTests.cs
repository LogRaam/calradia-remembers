// Code written by Gabriel Mailhot, 02/09/2026.
// ModRecoveryPolicy backs the cr.reset recovery command, offered to a player whose mod stopped working after a
// council scene soft-locked and they force-quit. The command must stand down exactly the stuck subsystems and
// report them honestly, and must NEVER over-report (claiming it reset something that was fine) or under-report
// (leaving the player thinking nothing happened when it did). These tests pin the plan for each flag combination
// and the wording, including the "nothing was stuck" case which must still reassure and point at the log.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public class ModRecoveryPolicyTests
    {
        // STAKE: the plague case the command exists for, both subsystems stuck at once (a council overlay AND a
        // still-active assembly). Both must be stood down, overlay first so the report is deterministic.
        [Test]
        public void GIVEN_both_a_chat_overlay_and_an_active_assembly_WHEN_planning_THEN_both_are_stood_down_overlay_first()
        {
            ModRecoveryPolicy.Plan(chatOverlayOpen: true, assemblyActive: true)
                .Should().Equal(CrRecoveryAction.CloseChatOverlay, CrRecoveryAction.CancelAssembly);
        }

        // STAKE: only the overlay is stuck (the common case: a chat window that never closed). The command must
        // not also "cancel" an assembly that was never convened, or it would report a lie.
        [Test]
        public void GIVEN_only_a_chat_overlay_WHEN_planning_THEN_only_the_overlay_is_closed()
        {
            ModRecoveryPolicy.Plan(chatOverlayOpen: true, assemblyActive: false)
                .Should().Equal(CrRecoveryAction.CloseChatOverlay);
        }

        // STAKE: only an assembly is active (the player ran the command from the map, no chat open). Only the
        // assembly is stood down.
        [Test]
        public void GIVEN_only_an_active_assembly_WHEN_planning_THEN_only_the_assembly_is_cancelled()
        {
            ModRecoveryPolicy.Plan(chatOverlayOpen: false, assemblyActive: true)
                .Should().Equal(CrRecoveryAction.CancelAssembly);
        }

        // STAKE: nothing is stuck. The command must do nothing (never wipe or close a healthy state) and return an
        // empty plan, which Describe then turns into the reassuring "nothing was stuck" message below.
        [Test]
        public void GIVEN_nothing_stuck_WHEN_planning_THEN_the_plan_is_empty()
        {
            ModRecoveryPolicy.Plan(chatOverlayOpen: false, assemblyActive: false).Should().BeEmpty();
        }

        // STAKE: an empty plan must NOT read as an error or a no-op mystery: the player ran a recovery command and
        // deserves to know it looked and found nothing, plus where to look next (the log).
        [Test]
        public void GIVEN_an_empty_plan_WHEN_describing_THEN_it_reassures_and_points_at_the_log()
        {
            string message = ModRecoveryPolicy.Describe(new List<CrRecoveryAction>());

            message.Should().Contain("nothing was stuck");
            message.Should().Contain("CalradiaRemembers.log");
        }

        // STAKE: a real reset must name what it cleared so the player can trust it acted (and so a bug report can
        // say which subsystem was stuck). Both cleared subsystems appear in the one line.
        [Test]
        public void GIVEN_a_full_plan_WHEN_describing_THEN_it_names_each_cleared_subsystem()
        {
            IReadOnlyList<CrRecoveryAction> plan =
                ModRecoveryPolicy.Plan(chatOverlayOpen: true, assemblyActive: true);

            string message = ModRecoveryPolicy.Describe(plan);

            message.Should().Contain("chat window");
            message.Should().Contain("assembly");
        }

        // STAKE: a null plan (a defensive caller passing null) must not throw inside a recovery path, of all
        // places, the one code the player runs precisely because things are already broken.
        [Test]
        public void GIVEN_a_null_plan_WHEN_describing_THEN_it_does_not_throw_and_reassures()
        {
            ModRecoveryPolicy.Describe(null).Should().Contain("nothing was stuck");
        }
    }
}
