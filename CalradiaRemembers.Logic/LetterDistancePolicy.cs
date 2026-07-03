// Code written by Gabriel Mailhot, 03/07/2026.
// Whether a spouse writes to lament an absence is a matter of DISTANCE, not merely how long since they last
// spoke: she does not fret when the player has only stepped outside the walls, only when he is genuinely far.
// The chance rises with the courier's journey in days and is never a certainty, so even at the far edge of the
// world an absent-spouse letter stays an occasional thing.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The distance (in courier travel days) at which a spouse or consort may write to lament the player's
    ///   absence, as a graduated CHANCE: none while near, rising between <see cref="NearDays" /> and
    ///   <see cref="FarDays" />, and capped at <see cref="MaxChancePercent" /> so it is never systematic.
    /// </summary>
    public static class LetterDistancePolicy
    {
        /// <summary>At or under this many travel days the player is near enough that no absence is felt.
        /// Tuning: roughly half the east-west width of Calradia.</summary>
        public const int NearDays = 5;

        /// <summary>At or over this many travel days the chance is at its ceiling. Tuning: roughly the full
        /// east-west width of Calradia, so only a truly distant player draws these letters.</summary>
        public const int FarDays = 10;

        /// <summary>The ceiling chance, in percent: even at the far edge an absence letter is never certain. Tuning.</summary>
        public const int MaxChancePercent = 60;

        /// <summary>
        ///   The percent chance (0..<see cref="MaxChancePercent" />) that an absence letter fires at the given
        ///   courier travel days: none at or under <see cref="NearDays" />, a linear rise to
        ///   <see cref="FarDays" />, and the ceiling beyond.
        /// </summary>
        public static int AbsenceLetterChancePercent(double travelDays)
        {
            if (travelDays <= NearDays) return 0;
            if (travelDays >= FarDays) return MaxChancePercent;

            return (int) Math.Round(MaxChancePercent * (travelDays - NearDays) / (FarDays - NearDays));
        }
    }
}
