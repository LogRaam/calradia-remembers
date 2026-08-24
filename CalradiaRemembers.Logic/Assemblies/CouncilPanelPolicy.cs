// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 1: the Engagements panel and the end-of-council memory seal are now BOTH driven from the real
// resolution ledger (AssemblyLedger), not from a separate LLM extraction that could disagree with what actually
// executes. This pure policy decides, for one resolution entry, whether it shows in the panel and under what
// status word, and whether closing the council should seal a memory-of-decision for it. Kept pure so the mapping
// (a withdrawn motion is hidden; a committed motion is remembered, except the two kinds that already write their
// own memory at the lift) is unit-tested rather than eyeballed in game.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Panel display and memory-seal decisions for one council resolution, from its kind and status.</summary>
    public static class CouncilPanelPolicy
    {
        /// <summary>
        ///   Whether a resolution belongs in the Engagements panel. Everything the table has raised is shown
        ///   (Proposed, Agreed, and after the lift Applied or Failed) EXCEPT a motion withdrawn during the
        ///   session, which was taken back and should leave no trace on the panel.
        /// </summary>
        public static bool ShouldShow(ResolutionStatus status) => status != ResolutionStatus.Reversed;

        /// <summary>The panel's short standing word for a resolution's status.</summary>
        public static string StatusLabel(ResolutionStatus status)
        {
            switch (status)
            {
                case ResolutionStatus.Proposed: return "Discussed";
                case ResolutionStatus.Agreed:   return "Agreed";
                case ResolutionStatus.Applied:  return "Done";
                case ResolutionStatus.Failed:   return "Failed";
                default:                        return "Withdrawn"; // Reversed, never actually shown (ShouldShow gates it)
            }
        }

        /// <summary>
        ///   Whether closing the council should seal a first-person memory-of-decision onto the acting member for
        ///   this resolution. Only a motion that actually COMMITTED (Applied) is sealed, and only when the kind's
        ///   own lift executor does NOT already record the member a memory: <see cref="ResolutionKind.SwearOath" />
        ///   (CommitmentBehavior.Create) and <see cref="ResolutionKind.PledgeAgainst" /> (LaunchPledge) each write
        ///   their own, so sealing again would double the memory. A Failed motion is skipped here too: the lift
        ///   already records its own broken-pledge memory (CouncilLift.RememberPledge).
        /// </summary>
        public static bool ShouldSeal(string kind, ResolutionStatus status)
        {
            if (status != ResolutionStatus.Applied) return false;
            if (string.Equals(kind, ResolutionCatalog.KeyOf(ResolutionKind.SwearOath), StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(kind, ResolutionCatalog.KeyOf(ResolutionKind.PledgeAgainst), StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }
    }
}
