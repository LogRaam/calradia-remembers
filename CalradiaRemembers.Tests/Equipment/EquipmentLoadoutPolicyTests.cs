// Code written by Gabriel Mailhot, 02/09/2026.
// EquipmentLoadoutPolicy decides whether the notable-gear clause reads a hero's civilian or battle loadout, from
// the scene. It exists because a player reported their finest battle gear being remarked on in a tavern/bedchamber
// scene where they had narrated being out of armour. Get this wrong and either the fix does nothing (a settlement
// scene still reads battle plate) or it over-corrects (a battlefield reads a court gown). These tests pin the
// exact settlement set that flips to civilian, and that every field/hostile/unknown scene stays on battle.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Equipment
{
    [TestFixture]
    public sealed class EquipmentLoadoutPolicyTests
    {
        // THE PLAYER REPORT: every in-settlement scene must read the CIVILIAN outfit, so a legendary battle sword
        // stays in the saddlebags and only the town clothes (usually unremarkable) are what an onlooker sees.
        [TestCase(SceneType.Settlement)]
        [TestCase(SceneType.Keep)]
        [TestCase(SceneType.Tavern)]
        public void GIVEN_an_in_settlement_scene_WHEN_choosing_the_loadout_THEN_it_prefers_civilian(SceneType scene)
        {
            EquipmentLoadoutPolicy.PreferCivilianLoadout(scene).Should().BeTrue();
        }

        // The guard against over-correcting: on the road, in a dungeon, on a battlefield, or with no scene
        // resolved, a hero is in their war harness, so the clause must read the BATTLE loadout, never the gown.
        [TestCase(SceneType.Outdoor)]
        [TestCase(SceneType.Dungeon)]
        [TestCase(SceneType.Battlefield)]
        [TestCase(SceneType.Unknown)]
        public void GIVEN_a_field_or_hostile_or_unknown_scene_WHEN_choosing_the_loadout_THEN_it_prefers_battle(SceneType scene)
        {
            EquipmentLoadoutPolicy.PreferCivilianLoadout(scene).Should().BeFalse();
        }
    }
}
