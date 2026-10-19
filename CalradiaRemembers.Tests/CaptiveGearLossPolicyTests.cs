// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: pins the ONE rule the owner is still weighing (2026-07-21), whether
// equipment loss ties to FLEEING or to the moment of CAPTURE. Today's ratified answer is FLED only. These
// three tests are the seam: when the owner decides, this file (and CaptiveGearLossPolicy alone) is what
// changes, not the holder ledger, the best-per-slot policy, or the recovery routes.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveGearLossPolicyTests
    {
        // The one case that DOES take gear today: fleeing is the only outcome the brief ties the loss to.
        [Test]
        public void GIVEN_the_player_fled_WHEN_deciding_whether_gear_is_taken_THEN_it_is_taken()
        {
            CaptiveGearLossPolicy.TakesGearOnResolution(CaptiveEscapeResolution.Fled).Should().BeTrue();
        }

        // Beating every aggressor down means the player is standing over the men who took their gear:
        // nothing was ever surrendered, so stripping here would contradict the fiction itself.
        [Test]
        public void GIVEN_the_player_fought_free_WHEN_deciding_whether_gear_is_taken_THEN_it_is_not_taken()
        {
            CaptiveGearLossPolicy.TakesGearOnResolution(CaptiveEscapeResolution.FoughtFree).Should().BeFalse();
        }

        // The brief is explicit: "the loss is attached to ESCAPING, never to being captured, so a
        // knocked-out player is not punished twice." Overpowered must never trigger the transfer.
        [Test]
        public void GIVEN_the_player_was_overpowered_WHEN_deciding_whether_gear_is_taken_THEN_it_is_not_taken()
        {
            CaptiveGearLossPolicy.TakesGearOnResolution(CaptiveEscapeResolution.Overpowered).Should().BeFalse();
        }
    }
}
