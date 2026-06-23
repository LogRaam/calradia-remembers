// Code written by Gabriel Mailhot, 22/06/2026.
// "Talk to a prisoner — bargain for their freedom" (the inverse of the DeliverPrisoner deed). Pure so the
// branching rule stays tested: a lord you HOLD is freed on the spot, a lord ANOTHER holds becomes a rescue
// deed, and only a real lord can strike the bargain at all — a wrong branch would release the wrong way or
// let a commoner "bargain" their way out.

namespace CalradiaRemembers.Logic
{
    /// <summary>How a freedom bargain is honoured, decided by who holds the prisoner.</summary>
    public enum ReleaseBargainMode
    {
        /// <summary>Not a prisoner — no bargain.</summary>
        None,

        /// <summary>The player holds them: release on the spot (a free_prisoner action this turn).</summary>
        ImmediateRelease,

        /// <summary>Another lord holds them: a rescue deed, verified when they are actually freed.</summary>
        RescueDeed
    }

    /// <summary>The rules for a prisoner bargaining their way out.</summary>
    public static class PrisonerReleasePolicy
    {
        /// <summary>Only a captive LORD (a hero of standing) can offer to be ransomed into freedom.</summary>
        public static bool CanBargainForFreedom(bool isLord, bool isPrisoner)
            => isLord && isPrisoner;

        /// <summary>
        ///   Which way a freedom bargain is honoured: the player's own captive is released immediately
        ///   (case A); a captive another lord holds is a rescue deed (case B); a non-prisoner, neither.
        /// </summary>
        public static ReleaseBargainMode Classify(bool isPrisoner, bool heldByPlayer)
        {
            if (!isPrisoner) return ReleaseBargainMode.None;
            return heldByPlayer ? ReleaseBargainMode.ImmediateRelease : ReleaseBargainMode.RescueDeed;
        }
    }
}
