// Code written by Gabriel Mailhot, 31/07/2026.
// Phase 1 of hosting the council in a real settlement scene instead of a map overlay (which smears/risks VRAM):
// the council is now a 3D scene, so it needs a real place to stand in. This pure policy picks Hall vs Tavern by
// settlement ownership, with graceful fallback to whichever interior actually exists, so the choice is
// unit-tested and the mod just supplies the three booleans (owns the settlement, has a lordshall, has a tavern).

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Where a settlement-hosted council is staged.</summary>
    public enum CouncilVenue
    {
        /// <summary>No usable interior; the caller should keep the map overlay instead.</summary>
        None,

        /// <summary>The lordshall: the owning clan presides over its own house.</summary>
        Hall,

        /// <summary>The tavern: a guest gathers in the common room, having no hall to preside in.</summary>
        Tavern
    }

    /// <summary>Decides which interior hosts a settlement council, given ownership and what the place has.</summary>
    public static class CouncilVenuePolicy
    {
        /// <summary>
        ///   What the settlement offers, and whose it is. The first fact decides who PRESIDES and the other two
        ///   say what rooms exist, so a swap between them holds the council in the wrong room of the right town.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class VenueFacts
        {
           /// <summary>The player's clan holds this settlement, so the player presides rather than visits.</summary>
           public bool PlayerClanOwnsSettlement { get; set; }

           /// <summary>The settlement has a lord's hall to preside in.</summary>
           public bool HasLordshall { get; set; }

           /// <summary>The settlement has a tavern to gather in.</summary>
           public bool HasTavern { get; set; }
        }

        /// <summary>
        ///   The council's backdrop by WHERE it is held: the owner presides in his Hall, a guest gathers in the
        ///   Tavern; falls back to whichever interior the settlement actually has, else None (caller keeps the
        ///   overlay).
        /// </summary>
        public static CouncilVenue Choose(VenueFacts facts)
        {
           if (facts == null) return CouncilVenue.None;

           bool playerClanOwnsSettlement = facts.PlayerClanOwnsSettlement;
           bool hasLordshall = facts.HasLordshall;
           bool hasTavern = facts.HasTavern;

            if (playerClanOwnsSettlement && hasLordshall) return CouncilVenue.Hall;
            if (!playerClanOwnsSettlement && hasTavern) return CouncilVenue.Tavern;
            if (hasTavern) return CouncilVenue.Tavern;
            if (hasLordshall) return CouncilVenue.Hall;

            return CouncilVenue.None;
        }

        /// <summary>The engine Location id for a venue ("lordshall"/"tavern"), or null for None.</summary>
        public static string LocationId(CouncilVenue venue)
            => venue == CouncilVenue.Hall ? "lordshall" : venue == CouncilVenue.Tavern ? "tavern" : null;
    }
}
