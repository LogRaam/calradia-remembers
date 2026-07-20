// Code written by Gabriel Mailhot, 20/07/2026.
// Courier robustness batch (ROADMAP 2026-07-20): the pure rules for calling a LIVE courier "stuck". A jammed
// courier (terrain fault, a pursuit gone wrong) used to burn the whole delivery grace window and then LOSE the
// letter; detecting the jam early hands the letter back to date-based delivery instead, so it still arrives.
// The mod records each courier's position once per daily tick and asks these two questions.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides when a live courier's lack of movement stops being patience and becomes a jam.</summary>
    public static class CourierStuckPolicy
    {
        /// <summary>
        ///   TUNE: days a courier may sit on the same spot before its letter reverts to date-based delivery.
        ///   Two, not one: a single bad pathing day (a detour, a blocked ford) must not cost the visible ride.
        /// </summary>
        public const int StuckDaysBeforeFallback = 2;

        /// <summary>
        ///   TUNE: map-unit drift under which a day of "movement" counts as not moving at all, so engine jitter
        ///   around a jammed spot cannot masquerade as progress and reset the clock forever.
        /// </summary>
        public const float StuckEpsilon = 0.5f;

        /// <summary>True when the courier genuinely progressed since the last daily record (drift is not progress).</summary>
        public static bool HasMoved(float distanceSinceLastRecord) => distanceSinceLastRecord > StuckEpsilon;

        /// <summary>True when the courier has now sat still long enough to give its letter back to the fallback.</summary>
        public static bool IsStuck(int today, int lastMovementDay) => today - lastMovementDay >= StuckDaysBeforeFallback;
    }
}
