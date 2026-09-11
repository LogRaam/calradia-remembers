// Code written by Gabriel Mailhot, 07/07/2026.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure 8-point compass bearing from one campaign-map position to another, for phrasing a quest's
   ///   direction ("the hideout lies to the north of Town X"). Assumes the map's +X points east and +Y
   ///   points north (the Bannerlord convention); if a build ever proves otherwise, only the north/south
   ///   sense flips, in one place.
   /// </summary>
   public static class CardinalDirectionPolicy
   {
      /// <summary>
      ///   Returns "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east",
      ///   or "nearby" when the two points coincide. The result names where the END of
      ///   <paramref name="step" /> lies relative to its start.
      ///   <para>
      ///     Takes the STEP rather than four loose coordinates (2026-09-10). The old signature was
      ///     <c>Describe(fromX, fromY, toX, toY)</c>: swap the pairs and it compiles, runs, and names the
      ///     exact opposite compass point, which a quest hint would then print to the player as fact. A
      ///     <see cref="MapOffset" /> is built by <see cref="MapPoint.StepTo" />, so the direction is fixed
      ///     by the call that named it.
      ///   </para>
      /// </summary>
      public static string Describe(MapOffset step)
      {
         if (step.IsNowhere) return "nearby";

         double dx = step.East;
         double dy = step.North;

         // atan2 with east = 0 degrees and north = +90; fold into eight 45-degree sectors.
         double deg = System.Math.Atan2(dy, dx) * (180d / System.Math.PI);
         if (deg < 0d) deg += 360d; // 0..360, counter-clockwise from east

         int sector = (int) System.Math.Round(deg / 45d) % 8;

         return sector switch {
            0 => "east",
            1 => "north-east",
            2 => "north",
            3 => "north-west",
            4 => "west",
            5 => "south-west",
            6 => "south",
            7 => "south-east",
            _ => "nearby"
         };
      }
   }
}
