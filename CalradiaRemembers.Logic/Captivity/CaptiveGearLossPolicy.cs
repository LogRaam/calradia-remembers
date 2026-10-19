// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: ONE rule in the ratified brief is still under the owner's review
// (2026-07-21). Today, losing carried equipment is tied to FLEEING captivity, never to being captured or
// to being overpowered mid-escape (a knocked-out player is not punished twice). The owner is weighing an
// alternative where the loss instead happens at the moment of CAPTURE itself; that question is open, NOT
// decided. This file is the ONE seam the engine consults before it ever strips the player's equipment, so
// flipping the rule later is a one-line change HERE, not a rewrite of the holder ledger, the best-per-slot
// policy, or the recovery routes (defeat, capture-alive, post-battle loot): none of those depend on WHEN
// the loss triggers, only on the fact that it eventually does.

namespace CalradiaRemembers.Logic.Captivity
{
    /// <summary>How a captive-escape encounter resolved. Kept as this project's OWN copy of
    /// CalradiaRemembers.Duels.EscapeOutcome (the mod's real enum) so this pure policy carries no
    /// game-engine dependency; the engine maps one to the other at the call site.</summary>
    public enum CaptiveEscapeResolution
    {
        /// <summary>Every aggressor was knocked out: the player fought free.</summary>
        FoughtFree,

        /// <summary>The player reached the edge of the scene and broke away.</summary>
        Fled,

        /// <summary>The player was knocked out: still a prisoner.</summary>
        Overpowered
    }

    /// <summary>The single decision point for WHEN an escape encounter's outcome should transfer the
    /// player's equipment to their captor's holder. See file header: deliberately isolated so the
    /// still-open "loss happens at capture instead" question changes only this method.</summary>
    public static class CaptiveGearLossPolicy
    {
        /// <summary>
        ///   True only for a FLED escape (today's ratified rule). BeatThemAll/<see cref="CaptiveEscapeResolution.FoughtFree" />
        ///   keeps the gear on the player (they are standing over the men who took it, nothing left to give
        ///   up); Overpowered never touches it either (the loss is attached to ESCAPING, never to being
        ///   captured, so a knocked-out player is not punished twice: this matches vanilla, where being a
        ///   prisoner alone does not strip your inventory).
        /// </summary>
        public static bool TakesGearOnResolution(CaptiveEscapeResolution resolution)
            => resolution == CaptiveEscapeResolution.Fled;
    }
}
