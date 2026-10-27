// Code written by Gabriel Mailhot, 27/10/2026.
// Turns the hair colour Bannerlord renders (sampled off the race's hair gradient, FaceColorReader) into the word the
// player's encyclopedia page, and every NPC who sees the player, uses for it. Extracted from FaceColorReader.ClassifyHair,
// private inside the net472 mod and so never tested, like the eye bands before it (EyeColorBands).
//
// WHY. Windmolen (Nexus, 27/10/2026): "in the section where the player's appearance is described, my character [...] has
// red hair. He does not. [...] in my previous campaign the player character also, incorrectly, was supposed to have red
// hair." The old test called a colour red when red > green >= blue with a saturation above 0.35, and asked it BEFORE the
// brown bands: almost every brown is that warm, so an ordinary brown (110, 70, 40) read as red. Red is now kept for a
// true red, its green well under its red; a reddish brown is auburn; the rest are the browns they always were.

namespace CalradiaRemembers.Logic
{
   /// <summary>A rendered hair colour, its three channels 0..255.</summary>
   public sealed class HairRgb
   {
      /// <summary>The red channel.</summary>
      public int R { get; init; }

      /// <summary>The green channel.</summary>
      public int G { get; init; }

      /// <summary>The blue channel.</summary>
      public int B { get; init; }
   }

   /// <summary>The words for a rendered hair colour. TUNE: the boundaries are judgement on sample colours, not derived.</summary>
   public static class HairColorBands
   {
      /// <summary>A plain hair-colour word (the composer appends " hair"), or null when there is no colour.</summary>
      public static string Describe(HairRgb c)
      {
         if (c == null) return null;

         double luma = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
         int max = System.Math.Max(c.R, System.Math.Max(c.G, c.B));
         int min = System.Math.Min(c.R, System.Math.Min(c.G, c.B));
         double sat = max == 0 ? 0 : (max - min) / (double) max;
         bool warm = c.R > c.G && c.G >= c.B;

         if (luma < 50) return "dark";
         if (warm && c.R >= 140 && c.G <= 0.6 * c.R) return "red";
         if (warm && c.G < 0.58 * c.R && sat > 0.5 && luma < 120) return "auburn";
         if (luma < 110) return "dark brown";
         if (sat < 0.15 && luma > 140) return "grey";
         if (luma > 155) return "fair";

         return "brown";
      }
   }
}
