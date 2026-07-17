// Code written by Gabriel Mailhot, 17/07/2026.
// DIVORCE, announce phase (romance audit C1/C2). A spouse's divorce demand is announced by an LLM-written letter
// delivered by a courier, and the refusal clock only starts once that letter reaches the player. Two ways that
// signal betrayed the arc: the letter could NEVER arrive (LLM generation failed, or the courier was intercepted
// with no resend), freezing the whole demand at "clock not started" forever, no refusal, no escalation, no
// re-announce, a silent soft-lock that broke the CHANGELOG promise "you are never blindsided"; and a STALE letter
// from a previous demand cycle, still marked delivered, could falsely start the clock so a refusal was stamped
// for a demand the player never saw. This pure state machine drives a bounded announce phase: wait for the
// current letter, start the clock when it truly lands, re-announce a lapsed one, and after enough lapses force a
// guaranteed in-person notice so the arc always completes and the player always learns of it.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What the host should do this tick for a demand whose refusal clock has not yet started.</summary>
    public enum DivorceAnnounceStep
    {
        /// <summary>The current announcing letter is still within its delivery window: keep waiting, do nothing.</summary>
        Wait,

        /// <summary>The current letter reached the player: start the refusal clock now.</summary>
        StartClock,

        /// <summary>The window lapsed with no delivery: queue a fresh announcing letter and open a new window.</summary>
        Reannounce,

        /// <summary>
        ///   Too many windows have lapsed undelivered: stop trusting the letter path, give the player a
        ///   guaranteed in-person notice, and start the clock so the arc can never dead-lock.
        /// </summary>
        ForceNotice
    }

    /// <summary>
    ///   Drives the bounded announce phase of a spouse's divorce demand (romance audit C1/C2), so the refusal
    ///   clock starts on a letter the player genuinely saw THIS cycle, an undelivered letter re-announces
    ///   instead of freezing, and a persistently-undeliverable one still resolves via a forced notice.
    /// </summary>
    public static class DivorceAnnouncePolicy
    {
        /// <summary>Days a queued announcing letter has to reach the player before the host acts on its silence. Tuning.</summary>
        public const int AnnounceWindowDays = 8;

        /// <summary>Lapsed windows tolerated before the host stops trusting the letter and forces a notice. Tuning.</summary>
        public const int MaxReannounces = 2;

        /// <summary>
        ///   Given the current announce state, the single step the host should take. Delivery of the CURRENT
        ///   cycle's letter always wins (start the clock); otherwise wait out the window, then re-announce, then,
        ///   once <see cref="MaxReannounces" /> windows have lapsed, force a guaranteed notice.
        /// </summary>
        public static DivorceAnnounceStep Resolve(bool currentLetterDelivered,
                                                  int today,
                                                  int cycleBeginDay,
                                                  int reannounceCount)
        {
            if (currentLetterDelivered) return DivorceAnnounceStep.StartClock;

            if (today - cycleBeginDay < AnnounceWindowDays) return DivorceAnnounceStep.Wait;

            if (reannounceCount >= MaxReannounces) return DivorceAnnounceStep.ForceNotice;

            return DivorceAnnounceStep.Reannounce;
        }
    }
}
