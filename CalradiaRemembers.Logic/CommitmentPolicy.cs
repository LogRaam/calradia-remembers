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

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure math and tuning for a TrackedCommitment's deadline and its fulfilled/breached consequences.</summary>
    public static class CommitmentPolicy
    {
        /// <summary>Tuning: how many in-game days a pledge has to be honored — roughly one season.</summary>
        public const int DeadlineDays = 21;

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
    }

    /// <summary>What the daily overdue sweep should do once a commitment's deadline lapses. See <see cref="CommitmentPolicy.OnOverdue" />.</summary>
    public enum OverdueOutcome
    {
        /// <summary>An oath broken: the deadline passed with the required positive act never verified.</summary>
        Breach,

        /// <summary>An oath honored by its own silence: the deadline passed with the forbidden act never happening.</summary>
        Fulfill
    }
}
