// Code written by Gabriel Mailhot, 11/07/2026.
// The refusal countdown must start only once the divorce-demand letter has actually been DELIVERED (the
// player has seen it): a demand not yet delivered never times out into a refusal.

namespace CalradiaRemembers.Logic.Marriage
{
    /// <summary>
    ///   Pure gate deciding when a pending divorce demand's grace period has run out, unread refusal (the
    ///   player never explicitly refused, but the deadline passed). The clock is anchored on DELIVERY day,
    ///   never on the day the demand was raised, so a letter held back (see
    ///   <see cref="LetterDeliveryPolicy" />) never silently times out before the player could even see it.
    /// </summary>
    public static class DivorceDemandTimeoutPolicy
    {
        /// <summary>
        ///   Sentinel stored while the demand letter has NOT yet been delivered: the clock is not running,
        ///   so no amount of elapsed days can ever count as a timeout.
        /// </summary>
        public const int ClockNotStarted = int.MinValue;

        /// <summary>
        ///   True when the demand clock has actually been started, that is, the letter has been delivered
        ///   and a real start day was recorded (anything other than <see cref="ClockNotStarted" />).
        /// </summary>
        public static bool ClockStarted(int demandClockStartDay) => demandClockStartDay != ClockNotStarted;

        /// <summary>
        ///   The clock on a pending divorce demand. THREE INTS in a row, two days and a span, and the first is
        ///   also the flag: an unstarted clock is never due however large today grows, which is a rule a
        ///   swapped pair silently breaks.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class DemandClockFacts
        {
           /// <summary>The day the letter reached the player and the clock began. Unstarted means never due.</summary>
           public int DemandClockStartDay { get; set; }

           /// <summary>The current campaign day.</summary>
           public int Today { get; set; }

           /// <summary>How long the player is given before silence reads as refusal.</summary>
           public int GracePeriodDays { get; set; }
        }

        /// <summary>
        ///   True when a pending divorce demand should now lapse into a silent refusal: the clock has
        ///   actually started (the letter reached the player) AND at least <c>GracePeriodDays</c>
        ///   have elapsed since that delivery day. Never true while the clock has not started, however large
        ///   <c>Today</c> grows.
        /// </summary>
        public static bool IsRefusalDue(DemandClockFacts facts)
        {
           if (facts == null) return false;

           int demandClockStartDay = facts.DemandClockStartDay;
           int today = facts.Today;
           int gracePeriodDays = facts.GracePeriodDays;

            if (!ClockStarted(demandClockStartDay)) return false;

            return today - demandClockStartDay >= gracePeriodDays;
        }
    }
}
