// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 2, Phase 1: pure deadline math and consequence magnitudes for
// TrackedCommitment. The engine-coupled parts (creating the commitment, detecting fulfillment via a
// resolved scheme or a real battle, detecting a breach on the daily tick) live in
// CalradiaRemembers.Commitments.CommitmentBehavior; this class only reasons over plain numbers, so the
// tuning stays protected by tests.
// 04/08/2026: R7-light (swear_oath) adds OnOverdue (KeepPeace's "silence is success" inversion), the
// per-witness public-oath penalty (COUNCIL_ACTIONS.md's swear_oath section: a broken vow sworn before
// witnesses costs more than the same vow broken in private), the BrokenOath grudge magnitude, and the
// promised-amount sanity clamp for PayGold (reusing give_gold's own ceiling, Assemblies.GoldPledgePolicy,
// rather than inventing a second one: an oath's promised sum should never exceed what an instant pledge
// already allows).

#region

using System;
using CalradiaRemembers.Logic.Assemblies;

#endregion

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Logic
{
    /// <summary>Pure math and tuning for a TrackedCommitment's deadline and its fulfilled/breached consequences.</summary>
    public static class CommitmentPolicy
    {
        /// <summary>Tuning: how many in-game days a pledge has to be honored — roughly one season.</summary>
        public const int DeadlineDays = CalradianCalendar.DaysPerSeason;

        /// <summary>Tuning: relation/stance boost applied to the promisor toward the player on fulfillment.</summary>
        public const int FulfilledRelationBonus = 6;

        /// <summary>Tuning: relation/stance penalty applied to the promisor toward the player on breach (negative).</summary>
        public const int BreachedRelationPenalty = -12;

        /// <summary>
        ///   Tuning (swear_oath, COUNCIL_ACTIONS.md's "coup social des promesses publiques brisees"): the
        ///   relation cost inflicted on EACH living witness once a publicly-sworn oath breaks, on top of
        ///   <see cref="BreachedRelationPenalty" /> against the beneficiary. Negative, like every other penalty
        ///   here.
        /// </summary>
        public const int PerWitnessBreachPenalty = -3;

        /// <summary>
        ///   Tuning: the FLOOR the total per-witness cost can reach, however many witnesses sat at the table
        ///   (COUNCIL_ACTIONS.md's own "-2 relation avec chaque temoin, plafond -10" general rule; this build
        ///   uses -3/witness per the swear_oath section specifically, but keeps the SAME cap so a packed
        ///   WarCouncil of a dozen lords cannot turn one broken oath into an unbounded relation catastrophe).
        /// </summary>
        public const int MaxWitnessBreachPenalty = -10;

        /// <summary>
        ///   Tuning: magnitude planted for <see cref="Grudges.GrudgeSource.BrokenOath" /> (COUNCIL_ACTIONS.md's
        ///   own "~30"), through the real <c>GrudgeBehavior.TryPlant</c> gate like every other tier-2 source, so
        ///   the same anti-noise/rigidity rules apply rather than a bare relation hit.
        /// </summary>
        public const int BrokenOathGrudgeMagnitude = 30;

        /// <summary>
        ///   Tuning (PayGold's own sanity bound): the SAME ceiling give_gold's own council pledge already uses
        ///   (<see cref="GoldPledgePolicy.MaxPayable" />): an oath is a promise to pay LATER, not an instant
        ///   transfer, so nothing here checks the promisor's CURRENT purse (they may not hold it yet), but the
        ///   figure itself should never exceed what an instant pledge already allows.
        /// </summary>
        public static int ClampPromisedAmount(int proposed) => proposed < 1 ? 0 : Math.Min(proposed, GoldPledgePolicy.MaxPayable);

        /// <summary>
        ///   The total relation cost against EACH living witness once a public oath breaks: <paramref name="witnessCount" />
        ///   times <see cref="PerWitnessBreachPenalty" />, floored at <see cref="MaxWitnessBreachPenalty" /> (never
        ///   more negative). Zero witnesses (a private oath) costs nothing extra.
        /// </summary>
        public static int WitnessBreachPenalty(int witnessCount)
            => witnessCount <= 0 ? 0 : Math.Max(PerWitnessBreachPenalty * witnessCount, MaxWitnessBreachPenalty);

        /// <summary>
        ///   Tuning (v1.34.2 fault/no-fault hardening, audit Pattern 2): the grace window a SUSPENDED oath's
        ///   deadline is pushed forward to while its promisor (or, for Protect, the player) sits captive, so a
        ///   lord freed after a long captivity still has real days to make good and the sweep does not re-breach
        ///   him the instant he walks out of the cell. Re-applied each day the captivity persists; because the
        ///   promisor is already past the old deadline when this fires, the deadline only ever moves forward.
        /// </summary>
        public const int CaptivitySuspendGraceDays = 7;

        /// <summary>The deadline day for a commitment issued on <paramref name="issuedDay" />.</summary>
        public static int ComputeDeadline(int issuedDay) => issuedDay + DeadlineDays;

        /// <summary>True once <paramref name="currentDay" /> has reached or passed the deadline (inclusive).</summary>
        public static bool IsOverdue(int currentDay, int deadlineDay) => currentDay >= deadlineDay;

        /// <summary>
        ///   What the daily overdue sweep does with a kind once its deadline lapses with no other verifying
        ///   event having fired. Every kind but <see cref="CommitmentKind.KeepPeace" /> needs a POSITIVE act
        ///   before the deadline (move against the rival, pay the sum, fight at the player's side): reaching the
        ///   deadline with none is a broken oath, <see cref="OverdueOutcome.Breach" />. KeepPeace is the sole
        ///   inversion (COUNCIL_ACTIONS.md's own framing): the oath is "we will NOT go to war", so a war is what
        ///   breaches it (handled immediately, the moment it is declared, never waiting for this sweep) and
        ///   simply reaching the deadline having never gone to war IS the oath honored, <see cref="OverdueOutcome.Fulfill" />.
        /// </summary>
        public static OverdueOutcome OnOverdue(CommitmentKind kind)
            => kind == CommitmentKind.KeepPeace ? OverdueOutcome.Fulfill : OverdueOutcome.Breach;

        /// <summary>
        ///   The world as it stands when a commitment's deadline lapses. Three booleans in a row after the
        ///   kind, and each is a no-fault escape of a DIFFERENT shape: a target who is gone voids the pledge, a
        ///   promisor in a cell suspends it, and a PLAYER in a cell suspends only a Protect. Cross the two
        ///   prisoners and a lord is branded an oathbreaker for the player's own captivity.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class OverdueFacts
        {
           /// <summary>Which oath this is. KeepPeace honours itself by silence and short-circuits first.</summary>
           public CommitmentKind Kind { get; set; }

           /// <summary>
           ///   The lord who swore is held. He could do nothing to honour it, so the clock is suspended rather
           ///   than run out under him.
           /// </summary>
           public bool PromisorIsPrisoner { get; set; }

           /// <summary>The PLAYER is held. Suspends a Protect only: there is nobody to fight beside.</summary>
           public bool PlayerIsPrisoner { get; set; }

           /// <summary>
           ///   The rival a MoveAgainst named is dead or gone from the world. A lord cannot move against a corpse,
           ///   and the death is an act of the world, never his breach.
           /// </summary>
           public bool TargetGone { get; set; }
        }

        /// <summary>
        ///   The overdue-sweep outcome once the WORLD that moved between promise and deadline is taken into
        ///   account (v1.34.2 fault/no-fault hardening, audit Pattern 2): a commitment must never be branded a
        ///   breach for an event outside the promisor's control. KeepPeace still honors itself by its own silence
        ///   even from a cell (its baseline is never a breach, so it short-circuits first). For every breach-bound
        ///   kind: a MoveAgainst pledge whose rival is now dead or gone (<c>TargetGone</c>) is a clean
        ///   <see cref="OverdueOutcome.NeutralVoid" /> (a lord cannot move against a corpse, and the death is an act
        ///   of the world, never his breach); a promisor who is a PRISONER, or (for Protect) a PLAYER who is a
        ///   prisoner, could do nothing to honor the vow, so the clock is <see cref="OverdueOutcome.Suspend" />ed,
        ///   never run out under him, resuming when he is freed. Only when none of these no-fault escapes apply does
        ///   silence at the deadline read as a genuine <see cref="OverdueOutcome.Breach" />.
        /// </summary>
        public static OverdueOutcome OnOverdue(OverdueFacts facts)
        {
           if (facts == null) return OverdueOutcome.Breach;

           CommitmentKind kind = facts.Kind;
           bool promisorIsPrisoner = facts.PromisorIsPrisoner;
           bool playerIsPrisoner = facts.PlayerIsPrisoner;
           bool targetGone = facts.TargetGone;

            OverdueOutcome baseline = OnOverdue(kind);
            if (baseline != OverdueOutcome.Breach) return baseline; // KeepPeace: silence IS success, cell or no cell

            if (kind == CommitmentKind.MoveAgainst && targetGone) return OverdueOutcome.NeutralVoid;
            if (promisorIsPrisoner) return OverdueOutcome.Suspend;
            if (kind == CommitmentKind.Protect && playerIsPrisoner) return OverdueOutcome.Suspend;

            return OverdueOutcome.Breach;
        }
    }

    /// <summary>What the daily overdue sweep should do once a commitment's deadline lapses. See <see cref="CommitmentPolicy.OnOverdue" />.</summary>
    public enum OverdueOutcome
    {
        /// <summary>An oath broken: the deadline passed with the required positive act never verified.</summary>
        Breach,

        /// <summary>An oath honored by its own silence: the deadline passed with the forbidden act never happening.</summary>
        Fulfill,

        /// <summary>
        ///   The obligation ends CLEANLY, with no fault to answer for: the world moved between promise and deadline
        ///   (the rival the pledge named died or vanished), so there is nothing left to honor and no breach to
        ///   punish (v1.34.2 fault/no-fault hardening).
        /// </summary>
        NeutralVoid,

        /// <summary>
        ///   The clock is PAUSED, not run out: the promisor (or, for a Protect oath, the player) is a captive and
        ///   could do nothing to honor the vow. Extend the deadline and leave the pledge alive; it resumes its
        ///   normal course once they are freed (v1.34.2 fault/no-fault hardening).
        /// </summary>
        Suspend
    }
}
