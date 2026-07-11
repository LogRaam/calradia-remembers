// Code written by Gabriel Mailhot, 11/07/2026.
// The refusal countdown must start only once the divorce-demand letter has actually been DELIVERED (the
// player has seen it): a demand not yet delivered never times out into a refusal.

namespace CalradiaRemembers.Logic
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
        ///   True when a pending divorce demand should now lapse into a silent refusal: the clock has
        ///   actually started (the letter reached the player) AND at least <paramref name="gracePeriodDays" />
        ///   have elapsed since that delivery day. Never true while the clock has not started, however large
        ///   <paramref name="today" /> grows.
        /// </summary>
        public static bool IsRefusalDue(int demandClockStartDay, int today, int gracePeriodDays)
        {
            if (!ClockStarted(demandClockStartDay)) return false;

            return today - demandClockStartDay >= gracePeriodDays;
        }
    }
}
