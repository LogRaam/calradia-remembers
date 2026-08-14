// Code written by Gabriel Mailhot, 14/08/2026.
// Player report: "the monarch notified me per letter of a successful siege after a few days when I was part
// of the force that took it." The realm-tidings feature already filters WorldEvent.InvolvesPlayer so the
// player is never told their own deeds, but WorldEventFeeders.OnSettlementOwnerChanged only ever recognised
// the player as involved when they were the officially credited CAPTURER (an army's own leader), a mere
// member of the besieging force was never flagged, so the conquest boomeranged back as unbidden "news".
// WorldEventFeeders.OnMapEventEnded stamps a short-lived marker (settlement id + day) when the player fought
// on the winning attacking side of a siege assault; this pure helper decides whether that marker still
// matches the settlement whose ownership is changing, so the match logic itself is unit-tested without
// needing a live MapEvent.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a buffered "the player just stormed this settlement" marker still applies to a
    ///   settlement whose ownership is changing. Deliberately narrow (same settlement, recent day) so a later
    ///   unrelated recapture of the same place can never false-positive against a stale marker.
    /// </summary>
    public static class SiegeParticipationMarker
    {
        /// <summary>
        ///   How many days old a marker may be and still match, tolerating the ownership-change event landing
        ///   a tick after the assault that caused it (same-day or the very next day).
        /// </summary>
        public const int MaxDayAge = 1;

        /// <summary>
        ///   True when the marker recorded for <paramref name="markerSettlementId" /> on
        ///   <paramref name="markerDay" /> still applies to <paramref name="settlementId" /> as of
        ///   <paramref name="today" />: the settlement must match exactly, and the marker must be neither in
        ///   the future nor older than <see cref="MaxDayAge" /> days.
        /// </summary>
        public static bool Matches(string markerSettlementId, int markerDay, string settlementId, int today)
        {
            if (string.IsNullOrEmpty(markerSettlementId) || string.IsNullOrEmpty(settlementId)) return false;
            if (markerSettlementId != settlementId) return false;

            int age = today - markerDay;

            return age >= 0 && age <= MaxDayAge;
        }
    }
}
