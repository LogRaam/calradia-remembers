// Code written by Gabriel Mailhot, 20/07/2026.
// The transaction model of an assembly motion. Gabriel's rule: decisions are PROVISIONAL during the session and
// only touch the world when the assembly is LIFTED. So a motion can be proposed, agreed, and still WITHDRAWN
// before the lift with nothing to undo (no gold to refund, no mission to cancel). This pure state machine
// documents which moves are legal and which status actually commits, so the "cancel a provisional resolution"
// path is proven solid rather than trusted. No engine dependency.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The legal transitions of a <see cref="ResolutionStatus" /> and the commit gate at the lift.</summary>
    public static class ResolutionLifecycle
    {
        /// <summary>
        ///   Still on the table and editable this session: it can be agreed, or withdrawn, and it has NOT touched
        ///   the world yet. Both <see cref="ResolutionStatus.Proposed" /> and <see cref="ResolutionStatus.Agreed" />
        ///   are provisional; the distinction only matters at the lift.
        /// </summary>
        public static bool IsProvisional(ResolutionStatus status)
            => status == ResolutionStatus.Proposed || status == ResolutionStatus.Agreed;

        /// <summary>
        ///   A provisional motion the player may still WITHDRAW (cancel) before the lift. This is the whole point
        ///   of the transaction model: cancelling costs nothing because nothing has been applied.
        /// </summary>
        public static bool CanReverse(ResolutionStatus status) => IsProvisional(status);

        /// <summary>
        ///   The only status that commits when the assembly is lifted. A motion merely PROPOSED (floated but never
        ///   consented to) does NOT commit, and a REVERSED one never commits: the world sees agreed motions only.
        /// </summary>
        public static bool CommitsAtLift(ResolutionStatus status) => status == ResolutionStatus.Agreed;

        /// <summary>Terminal: nothing further can happen to it. Reversed is terminal within the session; Applied
        /// and Failed are the two post-commit outcomes.</summary>
        public static bool IsTerminal(ResolutionStatus status)
            => status == ResolutionStatus.Reversed
               || status == ResolutionStatus.Applied
               || status == ResolutionStatus.Failed;

        /// <summary>
        ///   Whether a status change is legal. During the session only Propose/Agree/Reverse moves occur; the
        ///   Agreed -> Applied/Failed moves happen once, at the lift, when the bridge runs the executor.
        /// </summary>
        public static bool CanTransition(ResolutionStatus from, ResolutionStatus to)
        {
            switch (from)
            {
                case ResolutionStatus.Proposed:
                    // Consent it, or withdraw it. It cannot jump straight to committed: nothing commits mid-session.
                    return to == ResolutionStatus.Agreed || to == ResolutionStatus.Reversed;

                case ResolutionStatus.Agreed:
                    // Still withdrawable before the lift; otherwise it commits to exactly one outcome at the lift.
                    return to == ResolutionStatus.Reversed
                           || to == ResolutionStatus.Applied
                           || to == ResolutionStatus.Failed;

                default:
                    // Reversed, Applied, Failed are terminal.
                    return false;
            }
        }
    }
}
