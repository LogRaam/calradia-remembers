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
      ///   or "nearby" when the two points coincide. The result names where <paramref name="toX" />/<paramref
      ///   name="toY" /> lies relative to <paramref name="fromX" />/<paramref name="fromY" />.
      /// </summary>
      public static string Describe(double fromX, double fromY, double toX, double toY)
      {
         double dx = toX - fromX;
         double dy = toY - fromY;

         if (dx == 0d && dy == 0d) return "nearby";

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
