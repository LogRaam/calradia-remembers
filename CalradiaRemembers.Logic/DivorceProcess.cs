// Code written by Gabriel Mailhot, 03/07/2026.
// DIVORCE, Phase 1 (NPC case): the plain state carrier for an ESTRANGEMENT PROCESS, the same shape as
// TrackedCommitment. A marriage is an alliance, so ending one is never "talk once and it is done": this
// record tracks a multi-day process the spouse may resist, delay, or defeat outright. The decision logic
// (deadline math, the resistance roll, consequence magnitudes) lives in DivorcePolicy; the host
// (CalradiaRemembers.Marriage.DivorceProcessBehavior) persists this and advances it on the daily tick.

namespace CalradiaRemembers.Logic
{
    /// <summary>The lifecycle of a divorce process. Active is the only non-terminal state.</summary>
    public enum DivorceProcessState
    {
        /// <summary>Still under way, its end day not yet reached (or reached but not yet resolved this tick).</summary>
        Active,

        /// <summary>The marriage was genuinely dissolved.</summary>
        Fulfilled,

        /// <summary>The spouse's resistance won out, or a guard failed at the end day: the estrangement is off.</summary>
        Aborted
    }

    /// <summary>
    ///   One NPC's ongoing estrangement from their own spouse, begun at the player's urging: who,
    ///   concerning which spouse, started when, due when, how many days of spousal resistance have
    ///   already pushed the end day back, and where it stands. A plain state carrier: <see cref="DivorcePolicy" />
    ///   drives the deadline math, the resistance roll, and the consequence magnitudes; the host persists it.
    /// </summary>
    public sealed class DivorceProcess
    {
        /// <summary>Stable unique id.</summary>
        public string Id { get; set; }

        /// <summary>Hero StringId of the NPC the player persuaded to seek the estrangement.</summary>
        public string NpcId { get; set; }

        /// <summary>Hero StringId of the NPC's own spouse, whose marriage is being dissolved.</summary>
        public string SpouseId { get; set; }

        /// <summary>In-game day the process began.</summary>
        public int StartDay { get; set; }

        /// <summary>In-game day the process resolves, absent further delay (pushed back by a Delayed resistance roll).</summary>
        public int EndDay { get; set; }

        /// <summary>Total days already added by spousal resistance, capped by <see cref="DivorcePolicy.MaxCumulativeDelayDays" />.</summary>
        public int CumulativeDelayDays { get; set; }

        /// <summary>Where the process currently stands.</summary>
        public DivorceProcessState State { get; set; } = DivorceProcessState.Active;
    }
}
