// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 2, Phase 1 (the mechanical spine): the symmetric of InformalQuest — instead
// of the player owing the NPC a task, the NPC owes the PLAYER something. The flagship kind is a lord
// pledging to move against a rival, powered by the existing (CR-owned, deterministic) schemes system;
// this file only carries the plain state, never the decision logic (see CommitmentPolicy).

namespace CalradiaRemembers.Logic
{
    /// <summary>The flavour of a tracked commitment. Phase 1 ships only the flagship kind.</summary>
    public enum CommitmentKind
    {
        /// <summary>The promisor pledged to move against a named rival, launching a real political scheme.</summary>
        MoveAgainst
    }

    /// <summary>The lifecycle of a tracked commitment. Active is the only non-terminal state.</summary>
    public enum CommitmentState
    {
        /// <summary>Still outstanding, its deadline not yet reached.</summary>
        Active,

        /// <summary>Honored: verified by a real campaign event before the deadline lapsed.</summary>
        Fulfilled,

        /// <summary>The deadline passed with nothing done: an oath broken.</summary>
        Breached
    }

    /// <summary>
    ///   One NPC's promise TO THE PLAYER: who promised, concerning whom, of what kind, issued and due
    ///   when, and where it stands. A plain state carrier, mirroring <see cref="Scheme" /> in shape:
    ///   <see cref="CommitmentPolicy" /> drives the deadline math and consequence magnitudes; the host
    ///   (CalradiaRemembers.Commitments.CommitmentBehavior) persists it and turns a breach or
    ///   fulfillment into real standing.
    /// </summary>
    public sealed class TrackedCommitment
    {
        /// <summary>Stable unique id.</summary>
        public string Id { get; set; }

        /// <summary>Hero StringId of the NPC who made the promise.</summary>
        public string PromisorId { get; set; }

        /// <summary>Hero StringId of the rival the promise concerns.</summary>
        public string TargetId { get; set; }

        /// <summary>The commitment's flavour.</summary>
        public CommitmentKind Kind { get; set; }

        /// <summary>In-game day the promise was made.</summary>
        public int IssuedDay { get; set; }

        /// <summary>In-game day by which the promise must be honored, else it is breached.</summary>
        public int DeadlineDay { get; set; }

        /// <summary>Where the promise currently stands.</summary>
        public CommitmentState State { get; set; } = CommitmentState.Active;
    }
}
