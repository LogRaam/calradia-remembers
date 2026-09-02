// Code written by Gabriel Mailhot, 02/09/2026.
// Equipment-awareness pillar, war-vs-civil split (player report): the notable-gear clause read BOTH loadouts and
// kept the finest piece, so a warrior's legendary battle sword and plate surfaced even in a tavern or a bedchamber
// scene, contradicting a player who had narrated being out of armour. The game itself shows a hero's CIVILIAN
// outfit inside a settlement and their BATTLE harness in the field, so this policy picks the loadout that matches
// the scene, and the extractor reads only that one. Pure and engine-free so the mapping stays tested; the host
// resolves the live SceneType and reads the chosen Equipment set.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Chooses which equipment loadout (civilian or battle) the notable-gear clause should read for a scene.</summary>
    public static class EquipmentLoadoutPolicy
    {
        /// <summary>
        ///   True when the scene is an in-settlement setting where the game shows a hero's CIVILIAN attire (a town
        ///   or village, a keep, a tavern), so the gear clause should read the civilian outfit, not the battle
        ///   harness sitting in their saddlebags. Everything else (on the road, a dungeon, a battlefield, or an
        ///   unresolved scene) reads the BATTLE loadout, matching what the hero is actually wearing there.
        /// </summary>
        public static bool PreferCivilianLoadout(SceneType scene)
        {
            switch (scene)
            {
                case SceneType.Settlement:
                case SceneType.Keep:
                case SceneType.Tavern:
                    return true;
                default:
                    return false;
            }
        }
    }
}
