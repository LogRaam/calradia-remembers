// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 1 (bringing the existing 1:1 resource verbs to the council): give_gold's
// pure policy, the SIXTH executable resolution. A seated lord (ClanLords or WarCouncil council) pledges to give
// the PLAYER gold from their OWN purse, the mirror image of grant_stipend's money direction (a companion's
// stipend is funded FROM the player; this is funded FROM the giver TO the player). Mirrors the existing 1:1
// give_gold bridge's own solvency clamp exactly (BannerlordGameStateBridge.ExecuteGiveGold): a giver can never be
// made to conjure coin they do not hold, so the amount the LLM proposes (AssemblyResolutionEntry.TargetHint,
// reusing grant_stipend's own TargetAmount field/parse rather than growing a new one) is re-clamped here against
// the giver's REAL gold at the lift (bridge is law), never trusted from proposal time.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for give_gold: the payable-amount clamp, mirroring the 1:1 give_gold action's own solvency rule.</summary>
    public static class GoldPledgePolicy
    {
        /// <summary>The same ceiling the 1:1 give_gold bridge already applies (BannerlordGameStateBridge.ExecuteGiveGold): no single pledge exceeds this, however large the LLM's proposal.</summary>
        public const int MaxPayable = GoldTransferLimit.MaxSingleTransfer;

        /// <summary>
        ///   The real amount the lift may hand over: the LOWEST of what was proposed, what the giver actually
        ///   holds, and the fixed ceiling. A non-positive proposal, or a giver with no gold, both yield 0 (the
        ///   lift's own signal to refuse rather than move nothing while reporting success): a pledge must never
        ///   conjure coin the giver does not hold, the exact stake this policy exists to pin.
        /// </summary>
        public static int ClampPayable(int proposed, int giverGold)
        {
            int payable = Math.Min(proposed, Math.Min(giverGold, MaxPayable));

            return payable < 0 ? 0 : payable;
        }
    }
}
