// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar: the deterministic denar-value-to-magnitude mapping the live GiftReceived/
// GoldReceived plant sites share (BannerlordGameStateBridge.ExecuteGiveItem / ExecuteTakeGold). Kept pure
// and separate from AppreciationPolicy (which reasons about an ALREADY-DECIDED magnitude, never about
// game-specific currency), so it can be tuned and tested independently.

namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>Maps a denar value (an item's worth, or a gold sum) onto an appreciation formation magnitude.</summary>
    public static class AppreciationValueScaling
    {
        /// <summary>Tuning: denar value divided by this to reach a magnitude, before clamping.</summary>
        public const int DenarsPerMagnitudePoint = 100;

        /// <summary>Tuning: the floor a gift/payment's magnitude is clamped to — even a token sum still registers as a small kindness.</summary>
        public const int MinMagnitude = 15;

        /// <summary>Tuning: the ceiling a gift/payment's magnitude is clamped to — no single hand-off reads as an overwhelming debt of gratitude.</summary>
        public const int MaxMagnitude = 60;

        /// <summary>
        ///   A denar value (an item's <c>ItemObject.Value</c>, or a gold sum) scaled to an appreciation
        ///   magnitude: <paramref name="value" /> / <see cref="DenarsPerMagnitudePoint" />, clamped to
        ///   [<see cref="MinMagnitude" />, <see cref="MaxMagnitude" />]. A non-positive value still yields
        ///   the floor — the ACT of giving is never worth zero, only worth little.
        /// </summary>
        public static int FromValue(int value)
        {
            int magnitude = value / DenarsPerMagnitudePoint;

            if (magnitude < MinMagnitude) return MinMagnitude;

            return magnitude > MaxMagnitude ? MaxMagnitude : magnitude;
        }
    }
}
