// Code written by Gabriel Mailhot, 29/07/2026.
// This gate decides whether an intimacy-tagged scene leaves a fondly-remembered KindnessInScene appreciation,
// which is what the NPC's encyclopedia surfaces under "Kindnesses". It exists because of a real player report
// (Nexus): after a VIOLENT encounter that set the relation to -20, the encyclopedia still read "carries a small
// kindness remembered towards you for a tender moment we shared" because the mod planted the kindness on any
// turn the model tagged as intimacy, with no tone check. If this gate is wrong, either a hostile/coerced scene
// stamps a fake tenderness on the encyclopedia (the reported bug), or a genuinely tender moment leaves no mark
// at all (the feature failing to deliver). Every test pins one side of that line.

#region

using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AppreciationTonePolicyTests
    {
        // STAKE: the exact reported bug. A scene the model tagged intimacy but that LOWERED the relation is not a
        // kindness; planting one made the encyclopedia contradict the -20 relation and the violent memory.
        [Test]
        public void GIVEN_the_same_turn_lowered_the_relation_WHEN_deciding_THEN_no_kindness_is_left()
        {
            AppreciationTonePolicy.LeavesSceneKindness(relationLoweredThisTurn: true, coercedScene: false)
                .Should().BeFalse();
        }

        // STAKE: a captor/captive scene is coerced by definition; a prisoner "intimacy" is not a fond kindness
        // owed to the player, so it must never surface as one even when no relation change accompanies it.
        [Test]
        public void GIVEN_a_captive_or_coerced_scene_WHEN_deciding_THEN_no_kindness_is_left()
        {
            AppreciationTonePolicy.LeavesSceneKindness(relationLoweredThisTurn: false, coercedScene: true)
                .Should().BeFalse();
        }

        // STAKE: the feature must still deliver. A consensual tenderness that neither lowered the relation nor
        // was coerced IS a fond kindness; the "a tender moment we shared" wording is then honest.
        [Test]
        public void GIVEN_a_consensual_tenderness_that_did_not_lower_the_relation_WHEN_deciding_THEN_a_kindness_is_left()
        {
            AppreciationTonePolicy.LeavesSceneKindness(relationLoweredThisTurn: false, coercedScene: false)
                .Should().BeTrue();
        }

        // STAKE: both faults together must still refuse. A coerced scene that ALSO lowered the relation is the
        // clearest non-kindness; neither guard alone should be relied on to catch it.
        [Test]
        public void GIVEN_a_coerced_scene_that_also_lowered_the_relation_WHEN_deciding_THEN_no_kindness_is_left()
        {
            AppreciationTonePolicy.LeavesSceneKindness(relationLoweredThisTurn: true, coercedScene: true)
                .Should().BeFalse();
        }
    }
}
