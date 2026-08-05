// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 2, Phase 1 (the mechanical spine): the symmetric of InformalQuest — instead
// of the player owing the NPC a task, the NPC owes the PLAYER something. The flagship kind is a lord
// pledging to move against a rival, powered by the existing (CR-owned, deterministic) schemes system;
// this file only carries the plain state, never the decision logic (see CommitmentPolicy).
// 04/08/2026: R7-light (COUNCIL_ACTIONS.md's swear_oath, Partie 8) generalizes this to a small whitelist of
// AUTO-VERIFIABLE oath kinds exposed at the council: PayGold, KeepPeace, Protect, appended after MoveAgainst so
// every ordinal already persisted in a save stays put (this enum serializes as its plain int ordinal, Newtonsoft's
// default, since no [JsonConverter] is attached). PromisedAmount and WitnessIds are both NEW, ADDITIVE fields: an
// older save missing them deserializes with null (Json.NET's default for a missing member), which every reader
// below already treats as "no amount owed"/"no witnesses", so no SaveKey version bump is needed.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The flavour of a tracked commitment. Phase 1 shipped only the flagship kind (MoveAgainst); the R7-light
    ///   swear_oath build (04/08/2026) appends three more, chosen because each has a cheap, real event to verify
    ///   it by (never the LLM's own word, COUNCIL_ACTIONS.md's R7.2): a kind considered but DROPPED for lacking
    ///   one (e.g. "plead for peace with the king", whose outcome is not the pledger's own to control) is never
    ///   added here.
    /// </summary>
    public enum CommitmentKind
    {
        /// <summary>The promisor pledged to move against a named rival, launching a real political scheme.</summary>
        MoveAgainst,

        /// <summary>
        ///   swear_oath: the promisor owes the player <see cref="TrackedCommitment.PromisedAmount" /> denars by
        ///   the deadline. Verified by <c>CommitmentBehavior.OnGoldGivenToPlayer</c>, called from BOTH gold-giving
        ///   paths (the 1:1 give_gold action and the council's own give_gold resolution) so a partial or full
        ///   payment through EITHER channel counts; <see cref="TrackedCommitment.TargetId" /> is unused (the
        ///   beneficiary is always the player).
        /// </summary>
        PayGold,

        /// <summary>
        ///   swear_oath: the promisor's OWN faction (they must lead it, see the council's own offering fact) does
        ///   not declare war on the faction named in <see cref="TrackedCommitment.TargetId" /> (a faction
        ///   StringId here, not a hero, the one kind that repurposes the field) before the deadline. Verified the
        ///   OPPOSITE way from every other kind: silence is success. A war declared between the two factions
        ///   before the deadline is an IMMEDIATE breach (<c>CommitmentBehavior.OnWarDeclared</c>); reaching the
        ///   deadline with no such war is a fulfillment, not a breach, see <see cref="CommitmentPolicy.OnOverdue" />.
        /// </summary>
        KeepPeace,

        /// <summary>
        ///   swear_oath: the promisor fights on the PLAYER's own side in a real battle before the deadline.
        ///   Verified by <c>CommitmentBehavior</c>'s existing <c>OnMapEventEnded</c> hook (extended, not
        ///   duplicated): fulfilled the moment a MapEvent ends with the promisor and the player's own party on the
        ///   SAME side. <see cref="TrackedCommitment.TargetId" /> is unused (the beneficiary is always the
        ///   player, exactly like PayGold).
        /// </summary>
        Protect
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

        /// <summary>
        ///   Hero StringId of the rival the promise concerns (MoveAgainst, Protect implicitly targets the
        ///   player and leaves this unused). For <see cref="CommitmentKind.KeepPeace" /> ONLY, this instead
        ///   carries a FACTION StringId (COUNCIL_ACTIONS.md's swear_oath: a peace oath names a faction, not a
        ///   hero), the one field this kind repurposes rather than growing a second target column.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>The commitment's flavour.</summary>
        public CommitmentKind Kind { get; set; }

        /// <summary>In-game day the promise was made.</summary>
        public int IssuedDay { get; set; }

        /// <summary>In-game day by which the promise must be honored, else it is breached.</summary>
        public int DeadlineDay { get; set; }

        /// <summary>Where the promise currently stands.</summary>
        public CommitmentState State { get; set; } = CommitmentState.Active;

        /// <summary>
        ///   ADDITIVE field (04/08/2026, swear_oath): for <see cref="CommitmentKind.PayGold" /> ONLY, the denars
        ///   still owed. Set at swear time from the amount named at the council table (clamped by
        ///   <see cref="CommitmentPolicy.ClampPromisedAmount" />), then REDUCED as real gold arrives (any
        ///   give_gold, 1:1 or council) until it reaches zero, which is the fulfillment signal (see
        ///   <c>CommitmentBehavior.OnGoldGivenToPlayer</c>). Null for every other kind, and for a save written
        ///   before this field existed (Json.NET leaves a missing member null on deserialize, never zero, so an
        ///   old MoveAgainst pledge is never mistaken for an already-paid PayGold oath).
        /// </summary>
        public int? PromisedAmount { get; set; }

        /// <summary>
        ///   ADDITIVE field (04/08/2026, swear_oath): StringIds of the OTHER seated members present when this
        ///   oath was sworn (mirrors <c>AssemblyResolutionEntry.WitnessIds</c>, audit M6's own witness list,
        ///   carried down onto the commitment itself so the LATER breach sweep, which no longer has the council
        ///   session to consult, still knows who heard the vow). Null/empty for a private oath (nothing sworn
        ///   at a public table, e.g. the 1:1 pledge_against path never populates this) or a save written before
        ///   this field existed: both read as "no public cost", never invent witnesses that were never recorded.
        /// </summary>
        public List<string> WitnessIds { get; set; }
    }
}
