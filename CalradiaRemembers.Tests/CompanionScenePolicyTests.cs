// Code written by Gabriel Mailhot, 30/06/2026.
// Documents when a captive encounter targets a held companion (only when one is held, a roll inside the chance)
// and how the variant is drawn (coerce the player / the men assault / the captor torments).
//
// WHY IT MATTERS: this is the "stance with teeth" companion-victim scene (Hardcore only), the revenge driver
// that seeds the player's hatred of a captor for the rest of the campaign. ShouldTargetCompanion decides
// whether a scene turns onto a companion at all, and its answer feeds directly into CompanionAbuseConsequencePolicy
// (bond damage, revenge grudge, world-event). PickKind decides WHICH variant plays, and IsPlayerCoerced in
// particular decides whether the player is the forced perpetrator, which the consequence policy treats as a
// deeper betrayal than merely watching. Get the gate or the variant split wrong and either a companion is put
// through an ordeal with no held companion to justify it, or the wrong consequence magnitude lands.

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
        // The hard prerequisite: with zero companions held, even a winning roll must never target one, or the
        // host would try to open a companion-victim scene with no victim to pick, a null/invalid target.
        [Test]
        public void GIVEN_no_companion_is_held_WHEN_deciding_THEN_the_scene_never_targets_a_companion()
        {
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 0, rollPercent: 0).Should().BeFalse();
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 0, rollPercent: 10).Should().BeFalse();
        }

        // Pins the inclusive lower edge of CompanionSceneChancePercent (45): the scene must actually fire
        // inside the tuned chance, and this is what lets the revenge-driver scene exist at all.
        [Test]
        public void GIVEN_a_companion_is_held_WHEN_the_roll_lands_THEN_the_scene_targets_the_companion()
        {
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 1, rollPercent: 0).Should().BeTrue();
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 2, rollPercent: CompanionScenePolicy.CompanionSceneChancePercent - 1).Should().BeTrue();
        }

        // The far side of the same boundary: a roll landing ON or above the chance must leave the PLAYER as
        // the victim instead. Wrong here and the tuned 45% companion-scene rate silently drifts, unbalancing
        // how often a held companion is put through an ordeal versus the player.
        [Test]
        public void GIVEN_a_companion_is_held_WHEN_the_roll_misses_THEN_the_player_remains_the_victim()
        {
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 1, rollPercent: CompanionScenePolicy.CompanionSceneChancePercent).Should().BeFalse();
            CompanionScenePolicy.ShouldTargetCompanion(heldCompanions: 1, rollPercent: 99).Should().BeFalse();
        }

        // PlayerCoerced is the variant CompanionAbuseConsequencePolicy treats as the DEEPEST bond wound (the
        // player's own hands forced to the deed, not merely watching): pins the roll band that selects it, and
        // that IsPlayerCoerced/IsCollective classify it correctly for that downstream magnitude to apply.
        [Test]
        public void GIVEN_a_low_roll_WHEN_picking_the_variant_THEN_the_player_is_coerced_to_act()
        {
            CompanionSceneKind kind = CompanionScenePolicy.PickKind(0);
            kind.Should().Be(CompanionSceneKind.PlayerCoerced);
            CompanionScenePolicy.IsPlayerCoerced(kind).Should().BeTrue();
            CompanionScenePolicy.IsCollective(kind).Should().BeFalse();
        }

        // SoldiersAssault is the COLLECTIVE variant (the captor's men, not the captor alone); pins the middle
        // roll band and that IsCollective correctly flags it, since the collective flag drives how the scene's
        // witnesses are built (per CaptiveEncounterService's BuildSoldierWitnesses call).
        [Test]
        public void GIVEN_a_mid_roll_WHEN_picking_the_variant_THEN_the_men_assault_collectively()
        {
            CompanionSceneKind kind = CompanionScenePolicy.PickKind(50);
            kind.Should().Be(CompanionSceneKind.SoldiersAssault);
            CompanionScenePolicy.IsCollective(kind).Should().BeTrue();
            CompanionScenePolicy.IsPlayerCoerced(kind).Should().BeFalse();
        }

        // The top roll band and the third distinct variant: the captor acts in person while the player merely
        // watches, neither coerced nor collective. Together the three variant tests fix the exact roll bands
        // (0..34 / 35..69 / 70..99) so a retune can't silently collapse one variant's share to near-zero.
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
