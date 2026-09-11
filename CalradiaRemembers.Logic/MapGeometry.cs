// Code written by Gabriel Mailhot, 10/09/2026.
// Extracted during the facts-object realignment. Three separate policies were each taking campaign-map
// coordinates as loose runs of floats, and the runs were the longest unguarded ones left in Logic:
// NemesisMaterializePolicy.WithinRegion took five (anchorX, anchorY, partyX, partyY, radius),
// CardinalDirectionPolicy.Describe took four (fromX, fromY, toX, toY), and GeographyDescriber.Bearing
// took three (dx, dy, mapDistance). None of them could tell a swapped pair from a correct one, and a
// swapped from/to in Describe does not fail, it just names the opposite compass point.
//
// The honest fix here is not a facts bag but the two types the arithmetic was already about: a POSITION
// and the STEP between two positions. Naming the step's components East and North rather than X and Y is
// the part that pays: the compass rule reads off the field names instead of off a comment about which
// axis the engine calls which.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   A position on the campaign map, in the engine's own coordinates.
   ///   <para>
   ///     The constructor keeps a positional (x, y) pair, deliberately, and it is the one place in Logic
   ///     that does. Every drawing and mapping API ever written orders a point that way, so a reader has
   ///     the convention before they arrive; and a named-fields alternative would make the call sites
   ///     longer without making a mistake in them any less likely. The runs this type exists to remove
   ///     are the FOUR- and FIVE-long ones above it, where no such convention could help.
   ///   </para>
   /// </summary>
   public readonly struct MapPoint
   {
      public MapPoint(float x, float y)
      {
         X = x;
         Y = y;
      }

      /// <summary>Eastings. The Bannerlord map's +X points east.</summary>
      public float X { get; }

      /// <summary>Northings. The Bannerlord map's +Y points north.</summary>
      public float Y { get; }

      /// <summary>
      ///   The step FROM this position TO <paramref name="destination" />. Reads in the order it means, so
      ///   <c>a.StepTo(b)</c> can never be mistaken for <c>b.StepTo(a)</c> the way a positional pair can.
      /// </summary>
      public MapOffset StepTo(MapPoint destination)
         => new(destination.X - X, destination.Y - Y);

      /// <summary>
      ///   Squared distance to another position, so a radius test never pays for a square root. Symmetric,
      ///   which is why the two positions of such a test are safe to take side by side.
      /// </summary>
      public float SquaredDistanceTo(MapPoint other)
      {
         float dx = other.X - X, dy = other.Y - Y;

         return dx * dx + dy * dy;
      }
   }

   /// <summary>
   ///   How far east and how far north one map position lies from another. Built by
   ///   <see cref="MapPoint.StepTo" /> rather than by hand at a call site, so the sign of each component is
   ///   fixed by the direction the caller actually named.
   /// </summary>
   public readonly struct MapOffset
   {
      public MapOffset(float east, float north)
      {
         East = east;
         North = north;
      }

      /// <summary>Positive when the destination lies east of the origin.</summary>
      public float East { get; }

      /// <summary>Positive when the destination lies north of the origin.</summary>
      public float North { get; }

      /// <summary>True when the two positions coincide, so there is no direction to name.</summary>
      public bool IsNowhere => East == 0f && North == 0f;
   }
}
