// Code written by Gabriel Mailhot, 06/08/2026.
// Player report (2026-08-05): letters to a FAR correspondent were lost almost every time. On top of the
// distance-independent interception cap (CourierWaylayPolicy) and the global InvulnerableCouriers opt-out, the
// player may buy an ARMED ESCORT for a single correspondent's whole thread, making its couriers immune to
// interception. This is the pure part: what that guarantee costs. The premium scales with the distance to the
// recipient, because escorting a letter across the whole map is the very case the report was about, and it should
// cost more than guarding one to the next town over. Engine-free so the price is unit-tested in isolation; the mod
// supplies the map distance (Campaign.Current.Models.MapDistanceModel) and charges the returned denars.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure pricing for the per-correspondent armed-escort premium (a distance-scaled one-off fee).</summary>
    public static class EscortCostPolicy
    {
        /// <summary>
        ///   TUNE: the fixed part of the fee, paid whatever the distance. Raising and arming a mounted guard
        ///   detail costs more than the 500-denar hire of a lone rider (TaverneLetterBehavior.CourierHireCost),
        ///   so even the shortest escorted road starts above it.
        /// </summary>
        public const int BasePremium = 800;

        /// <summary>
        ///   TUNE: denars added per unit of map distance to the recipient. A longer road means more days of pay
        ///   and provisioning for the guard, so a cross-map thread costs more than a neighbouring one. The map
        ///   distance comes from the engine's MapDistanceModel, whose scale is TUNE-sensitive, hence the clamp.
        /// </summary>
        public const float PremiumPerDistanceUnit = 1.5f;

        /// <summary>
        ///   Clamp floor: the premium never drops below the base, which also fails a negative / NaN distance safe
        ///   to a sane price rather than something free or nonsensical.
        /// </summary>
        public const int MinPremium = 800;

        /// <summary>
        ///   Clamp ceiling: however far the recipient, the escort is never extortionate. Caps what an
        ///   unexpectedly large engine distance could otherwise inflate the fee to.
        /// </summary>
        public const int MaxPremium = 6000;

        /// <summary>
        ///   The one-off denars to escort a correspondent's whole thread, given the map distance to that
        ///   recipient. A base fee plus a per-distance rate, clamped to [<see cref="MinPremium" />,
        ///   <see cref="MaxPremium" />]. A negative or NaN distance is treated as zero (falls back to the base).
        /// </summary>
        public static int EscortPremium(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f) distance = 0f;

            int raw = BasePremium + (int) Math.Round(distance * PremiumPerDistanceUnit);

            if (raw < MinPremium) return MinPremium;
            if (raw > MaxPremium) return MaxPremium;

            return raw;
        }
    }
}
