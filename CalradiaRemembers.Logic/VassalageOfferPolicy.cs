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
        ///   What an oath of fealty needs. Three booleans in a row, one requirement then two disqualifiers, and
        ///   the bridge must REFUSE rather than silently ignore when this is false, so a lord who is not the
        ///   ruler gives an honest in-character refusal instead of a game state that does not match the fiction.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class VassalageFacts
        {
           /// <summary>
           ///   The NPC is the kingdom's actual ruler. A kingdom-decision or council-vote path is out of scope;
           ///   the ruler's own word suffices.
           /// </summary>
           public bool NpcIsKingdomRuler { get; set; }

           /// <summary>
           ///   The player's clan is already a full vassal somewhere. Mercenary service does NOT count: that is
           ///   paid service, not an oath, so such a clan may still swear real fealty.
           /// </summary>
           public bool PlayerAlreadyVassal { get; set; }

           /// <summary>
           ///   The player is actively fighting this kingdom. A clan signing allegiance to an enemy it is at war
           ///   with is a state the fiction can never justify.
           /// </summary>
           public bool PlayerAtWarWithKingdom { get; set; }
        }

        /// <summary>
        ///   True only when the NPC is the kingdom's actual ruler (a kingdom-decision/council-vote path is out
        ///   of scope; the ruler's own word suffices here) AND the player's clan is not already a full vassal
        ///   of some kingdom. A clan currently under mercenary service is NOT "already a vassal" (paid service,
        ///   not an oath), so it may still swear real fealty. The bridge must refuse (never silently ignore)
        ///   when this is false, so a lord who is not the ruler, or a clan already sworn elsewhere, gets an
        ///   honest in-character refusal instead of a game state that quietly does not match the fiction.
        ///   <para>
        ///   v1.34.2 governance hardening: <c>PlayerAtWarWithKingdom</c> forbids swearing fealty to
        ///   a kingdom the player is actively fighting (player report class: a clan signing allegiance to an
        ///   enemy it is at war with, a state the fiction can never justify). Re-checked at commit in
        ///   ExecuteJoinAsVassal against FactionManager.IsAtWarAgainstFaction, the bridge being the law.
        ///   </para>
        /// </summary>
        public static bool CanJoin(VassalageFacts facts)
        {
           if (facts == null) return false;

           bool npcIsKingdomRuler = facts.NpcIsKingdomRuler;
           bool playerAlreadyVassal = facts.PlayerAlreadyVassal;
           bool playerAtWarWithKingdom = facts.PlayerAtWarWithKingdom;

           return npcIsKingdomRuler && !playerAlreadyVassal && !playerAtWarWithKingdom;
        }
    }
}
