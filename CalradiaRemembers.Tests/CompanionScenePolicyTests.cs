// Code written by Gabriel Mailhot, 30/06/2026.
// Documents when a captive encounter targets a held companion (only when one is held, a roll inside the chance)
// and how the variant is drawn (coerce the player / the men assault / the captor torments).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionScenePolicyTests
    {
        [Test]
        public void GIVEN_no_companion_is_held_WHEN_deciding_THEN_the_scene_never_targets_a_companion()
        {
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 0, rollPercent: 0).Should().BeFalse();
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 0, rollPercent: 10).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_companion_is_held_WHEN_the_roll_lands_THEN_the_scene_targets_the_companion()
        {
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 1, rollPercent: 0).Should().BeTrue();
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 2, rollPercent: CompanionScenePolicy.CompanionSceneChancePercent - 1).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_companion_is_held_WHEN_the_roll_misses_THEN_the_player_remains_the_victim()
        {
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 1, rollPercent: CompanionScenePolicy.CompanionSceneChancePercent).Should().BeFalse();
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 1, rollPercent: 99).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_low_roll_WHEN_picking_the_variant_THEN_the_player_is_coerced_to_act()
        {
            CompanionSceneKind kind = CompanionScenePolicy.PickKind(0);
            kind.Should().Be(CompanionSceneKind.PlayerCoerced);
            CompanionScenePolicy.IsPlayerCoerced(kind).Should().BeTrue();
            CompanionScenePolicy.IsCollective(kind).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_mid_roll_WHEN_picking_the_variant_THEN_the_men_assault_collectively()
        {
            CompanionSceneKind kind = CompanionScenePolicy.PickKind(50);
            kind.Should().Be(CompanionSceneKind.SoldiersAssault);
            CompanionScenePolicy.IsCollective(kind).Should().BeTrue();
            CompanionScenePolicy.IsPlayerCoerced(kind).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_high_roll_WHEN_picking_the_variant_THEN_the_captor_torments_in_person()
        {
            CompanionSceneKind kind = CompanionScenePolicy.PickKind(90);
            kind.Should().Be(CompanionSceneKind.CaptorTorments);
            CompanionScenePolicy.IsCollective(kind).Should().BeFalse();
            CompanionScenePolicy.IsPlayerCoerced(kind).Should().BeFalse();
        }
    }
}
