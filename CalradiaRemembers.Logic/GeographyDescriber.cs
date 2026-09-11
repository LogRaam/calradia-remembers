// Code written by Gabriel Mailhot, 10/07/2026.
// Pure helper that turns raw map deltas plus a straight-line map distance into plain phrases an NPC can speak to
// describe where a place lies relative to them ("a few days' ride to the north-west"). No engine types, no I/O, so
// it lives in the pure logic project and is fully unit-tested. Bannerlord map axes: +X is EAST, +Y is NORTH.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for describing a bearing (distance band plus 8-wind direction) between two map points.</summary>
    public static class GeographyDescriber
    {
        /// <summary>TUNE: within this straight-line map distance the two points count as the same spot ("right here").</summary>
        public const float SamePlaceRadius = 12f;

        /// <summary>TUNE: distances under this read as "a short ride".</summary>
        public const float ShortRideMax = 60f;

        /// <summary>TUNE: distances under this read as "a few days' ride".</summary>
        public const float FewDaysMax = 180f;

        /// <summary>TUNE: distances under this read as "a long way off"; beyond it, "the far side of Calradia".</summary>
        public const float LongWayMax = 400f;

        /// <summary>
        ///   The 8-wind compass direction from a source to a target given the deltas (dx = targetX - sourceX,
        ///   dy = targetY - sourceY). Returns one of "north", "north-east", "east", "south-east", "south",
        ///   "south-west", "west", "north-west", or "" (empty) when the two points effectively coincide (their
        ///   straight-line separation is under <see cref="SamePlaceRadius"/>).
        /// </summary>
        public static string Direction(float dx, float dy)
        {
            if (Math.Sqrt((dx * dx) + (dy * dy)) < SamePlaceRadius)
                return "";

            var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (angle < 0)
                angle += 360.0;

            if (angle < 22.5) return "east";
            if (angle < 67.5) return "north-east";
            if (angle < 112.5) return "north";
            if (angle < 157.5) return "north-west";
            if (angle < 202.5) return "west";
            if (angle < 247.5) return "south-west";
            if (angle < 292.5) return "south";
            if (angle < 337.5) return "south-east";
            return "east";
        }

        /// <summary>
        ///   A coarse distance band phrase from a straight-line map distance:
        ///   under <see cref="SamePlaceRadius"/> gives "right here", under <see cref="ShortRideMax"/> gives
        ///   "a short ride", under <see cref="FewDaysMax"/> gives "a few days' ride", under <see cref="LongWayMax"/>
        ///   gives "a long way off", and otherwise "the far side of Calradia".
        /// </summary>
        public static string DistanceBand(float mapDistance)
        {
            if (mapDistance < SamePlaceRadius) return "right here";
            if (mapDistance < ShortRideMax) return "a short ride";
            if (mapDistance < FewDaysMax) return "a few days' ride";
            if (mapDistance < LongWayMax) return "a long way off";
            return "the far side of Calradia";
        }

        /// <summary>
        ///   The combined human bearing. When the distance is under <see cref="SamePlaceRadius"/> returns "right here".
        ///   Otherwise returns the band plus the direction (e.g. "a few days' ride to the north-west"). If the
        ///   direction is empty but the distance is not tiny (the points nearly coincide yet the distance says
        ///   otherwise), falls back to just the distance band with no direction suffix.
        /// </summary>
        public static string Bearing(MapOffset step, float mapDistance)
        {
            if (mapDistance < SamePlaceRadius)
                return "right here";

            var band = DistanceBand(mapDistance);
            var direction = Direction(step.East, step.North);
            if (direction.Length == 0)
                return band;

            return band + " to the " + direction;
        }
    }
}
