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
    ///   Picks the scene-image BASENAME (no extension, no path) for a council's thinking placeholder, from the
    ///   player's location and the council's own scope. Pure so the mapping (outside beats scope; gender variants
    ///   only for Family/War; the "Fermale" typo in the shipped asset) is unit-tested rather than eyeballed in-game.
    /// </summary>
    public static class CouncilSceneImagePolicy
    {
        /// <summary>
        ///   Resolves the image basename. <paramref name="outside" /> (not in a settlement) wins over everything
        ///   else, including a Family council held in the wilderness. Inside a settlement: Family scope first,
        ///   then whether the settlement belongs to the player's own clan (War), else the neutral Tavern image.
        ///   Gender variants exist only for Family and War; Tavern and every Outside image are gender-neutral.
        /// </summary>
        public static string Resolve(bool outside, bool scopeIsFamily, bool ownedByPlayer, CouncilSceneTerrain terrain, bool playerIsFemale)
        {
            if (outside)
                return terrain switch {
                    CouncilSceneTerrain.Desert => "OutsideCounselDesert",
                    CouncilSceneTerrain.Steppe => "OutsideCounselSteppes",
                    _ => "OutsideCounselForestMountains"
                };

            if (scopeIsFamily)
                // NOTE: "Fermale" is a typo in the shipped asset filename, matched exactly on purpose.
                return playerIsFemale ? "FamilyCounselFermale" : "FamilyCounselMale";

            if (ownedByPlayer)
                return playerIsFemale ? "WarCounselFemale" : "WarCounselMale";

            return "TavernCounsel";
        }
    }
}
