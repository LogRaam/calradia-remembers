// Code written by Gabriel Mailhot, 05/08/2026.
// join_as_vassal fix (player report, 2026-08-05): "asked the legitimate ruler to become her vassal, the LLM
// narrated acceptance and threw 6000 denars, but the game only ever registered the player as a mercenary" -
// join_as_mercenary (the paid contract) had a real action wired to it; a permanent oath of vassalage did not,
// so the narrated acceptance was an empty promise. This pure gate is the one guard both the prompt teaching
// (EncounterContext.VassalOfferKingdom) and the bridge's execution guard must agree on before a "sworn as
// vassal" outcome is real: only a kingdom's actual ruler can grant vassalage, and a clan already sworn to a
// kingdom cannot be re-sworn by a passing conversation.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate for the <c>join_as_vassal</c> action: whether the speaking NPC truly rules a kingdom, and
    ///   the player's clan is not already a full vassal of one, the two facts both the prompt teaching and
    ///   the bridge's execution guard must agree on before the oath is real.
    /// </summary>
    public static class VassalageOfferPolicy
    {
        /// <summary>
        ///   True only when the NPC is the kingdom's actual ruler (a kingdom-decision/council-vote path is out
        ///   of scope; the ruler's own word suffices here) AND the player's clan is not already a full vassal
        ///   of some kingdom. A clan currently under mercenary service is NOT "already a vassal" (paid service,
        ///   not an oath), so it may still swear real fealty. The bridge must refuse (never silently ignore)
        ///   when this is false, so a lord who is not the ruler, or a clan already sworn elsewhere, gets an
        ///   honest in-character refusal instead of a game state that quietly does not match the fiction.
        /// </summary>
        public static bool CanJoin(bool npcIsKingdomRuler, bool playerAlreadyVassal) =>
            npcIsKingdomRuler && !playerAlreadyVassal;
    }
}
