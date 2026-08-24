// Code written by Gabriel Mailhot, 02/08/2026.
// While the council is "thinking" (the placeholder turn before the narrator replies), the placeholder shows a
// situational SCENE image instead of the anchor NPC's portrait: no single seated member presides over a council,
// so their face on the waiting bubble reads wrong (the narrator model already dropped that framing, see
// ChatViewModel.CouncilPlaceholderText). Gabriel supplied one image per situation; this policy is the single
// source of truth for which one, so the mod-side resolution (Chat/ChatViewModel) and any future caller agree.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The terrain bucket a council scene image cares about: only Desert and Steppe get their own art (dunes,
    ///   open grass); every other engine TerrainType (Forest, Mountain, Plain, Snow, Water...) shares one
    ///   "forest/mountains" image, since Gabriel did not commission distinct art for each of those.
    /// </summary>
    public enum CouncilSceneTerrain
    {
        Desert,
        Steppe,
        Other
    }

    /// <summary>
    ///   The image family a council's TYPE calls for, independent of terrain. Family and War carry their own
    ///   sex-keyed art that primes over the location; Assembly reuses the tavern scene; every other scope
    ///   (companions, clan lords) is Generic and lets the LOCATION pick the image.
    /// </summary>
    public enum CouncilSceneKind
    {
        Family,
        War,
        Assembly,
        Generic
    }

    /// <summary>
    ///   Picks the scene-image BASENAME (no extension, no path) for a council's thinking placeholder, from the
    ///   council's own TYPE first and the player's location second. Pure so the mapping (type beats location for
    ///   Family/War; gender variants only for Family/War) is unit-tested rather than eyeballed in-game.
    /// </summary>
    public static class CouncilSceneImagePolicy
    {
        /// <summary>
        ///   Resolves the image basename (Gabriel 2026-08-23). The council's TYPE decides first: Family and War
        ///   each have their own sex-keyed art and it wins over the location, so a family council held in the
        ///   wilderness still shows the family scene, never the outside art. Assembly borrows the tavern scene.
        ///   A Generic scope (companions, clan lords) has no dedicated art, so the LOCATION picks it: the tavern
        ///   scene inside a settlement, else the outside scene for the terrain under the player. Gender variants
        ///   exist only for Family and War; Tavern and every Outside image are gender-neutral.
        /// </summary>
        public static string Resolve(bool outside, CouncilSceneKind kind, CouncilSceneTerrain terrain, bool playerIsFemale)
        {
            switch (kind)
            {
                case CouncilSceneKind.Family:
                    return playerIsFemale ? "FamilyCounselFemale" : "FamilyCounselMale";
                case CouncilSceneKind.War:
                    return playerIsFemale ? "WarCounselFemale" : "WarCounselMale";
                case CouncilSceneKind.Assembly:
                    return "TavernCounsel";
            }

            if (!outside) return "TavernCounsel";

            return terrain switch {
                CouncilSceneTerrain.Desert => "OutsideCounselDesert",
                CouncilSceneTerrain.Steppe => "OutsideCounselSteppes",
                _ => "OutsideCounselForestMountains"
            };
        }
    }
}
