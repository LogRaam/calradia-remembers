// Code written by Gabriel Mailhot, 04/09/2026.
// World Almanac political minimap, the rasteriser (Inc C): turns the pure PoliticalMapPolicy colour grid into a PNG
// the panel shows through the runtime image pipeline (RuntimePortraitFactory -> sprite -> CrRuntimeImageWidget). One
// pixel per grid cell; the sea (NoTerritory) is transparent so only land is painted; a land cell that borders a
// different faction (or the sea) is darkened, giving each realm a crisp outline like the mockup. North is up (the
// grid's +Y is world-north, so the image is flipped vertically). Colours are decoded with the engine's own
// Color.FromUint so the kingdom colour's byte order is never guessed. Wiring the PNG into the card/VM is Inc D.

#region

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CalradiaRemembers.Logic.WorldAlmanac;
using LibColor = TaleWorlds.Library.Color;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Rasterises a political-map colour grid to PNG bytes. Engine-adjacent (System.Drawing); the grid logic it renders is the unit-tested PoliticalMapPolicy.</summary>
   public static class PoliticalMapImageRenderer
   {
      // How much a border pixel is darkened toward black (0 = black, 1 = unchanged).
      private const float BorderDarken = 0.5f;

      /// <summary>
      ///   Renders <paramref name="grid" /> to a 32-bit PNG (one pixel per cell): sea cells transparent, land cells
      ///   the kingdom colour, and (when <paramref name="drawBorders" />) any land cell touching a different colour
      ///   or the sea darkened for a crisp frontier/coastline. Returns null on an empty grid or any draw failure, so
      ///   the caller can fall back to no map rather than crash the panel.
      /// </summary>
      public static byte[] RenderPng(uint[,] grid, bool drawBorders = true)
      {
         if (grid == null) return null;

         int w = grid.GetLength(0);
         int h = grid.GetLength(1);
         if (w < 1 || h < 1) return null;

         try
         {
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
               for (var x = 0; x < w; x++)
               {
                  for (var y = 0; y < h; y++)
                  {
                     uint cell = grid[x, y];

                     Color pixel;
                     if (cell == PoliticalMapPolicy.NoTerritory)
                     {
                        pixel = Color.FromArgb(0, 0, 0, 0); // transparent sea
                     }
                     else
                     {
                        pixel = ToColor(cell);
                        if (drawBorders && IsEdge(grid, x, y, w, h, cell))
                           pixel = Darken(pixel, BorderDarken);
                     }

                     // North is up: grid row 0 is world-south, so it lands at the BOTTOM of the image.
                     bmp.SetPixel(x, h - 1 - y, pixel);
                  }
               }

               using (var ms = new MemoryStream())
               {
                  bmp.Save(ms, ImageFormat.Png);

                  return ms.ToArray();
               }
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[POLITICAL_MAP] rasterise failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   A stable signature of the grid's contents, for cache invalidation: the map only needs re-rendering when a
      ///   conquest changes who owns what, which changes the grid, which changes this. FNV-1a over the cells.
      /// </summary>
      public static uint ComputeSignature(uint[,] grid)
      {
         if (grid == null) return 0u;

         uint hash = 2166136261u;
         int w = grid.GetLength(0);
         int h = grid.GetLength(1);
         for (var x = 0; x < w; x++)
         {
            for (var y = 0; y < h; y++)
            {
               uint c = grid[x, y];
               for (var b = 0; b < 4; b++)
               {
                  hash ^= (c >> (b * 8)) & 0xFFu;
                  hash *= 16777619u;
               }
            }
         }

         return hash;
      }

      #region private

      // The kingdom colour, forced opaque so a faction's fill never shows the frame through it (the alpha channel is
      // reserved for the transparent sea). Decoded via the engine's own Color.FromUint so we never guess byte order.
      private static Color ToColor(uint colorArgb)
      {
         LibColor c = LibColor.FromUint(colorArgb);

         return Color.FromArgb(255, Channel(c.Red), Channel(c.Green), Channel(c.Blue));
      }

      private static int Channel(float f)
      {
         int v = (int) (f * 255f + 0.5f);

         return v < 0 ? 0 : v > 255 ? 255 : v;
      }

      private static Color Darken(Color c, float factor)
         => Color.FromArgb(c.A, (int) (c.R * factor), (int) (c.G * factor), (int) (c.B * factor));

      // True when any 4-neighbour differs from this cell's colour (a faction frontier) or is off the grid: the map's
      // outer land edge and every internal border get the darker outline.
      private static bool IsEdge(uint[,] grid, int x, int y, int w, int h, uint cell)
         => Differs(grid, x - 1, y, w, h, cell)
            || Differs(grid, x + 1, y, w, h, cell)
            || Differs(grid, x, y - 1, w, h, cell)
            || Differs(grid, x, y + 1, w, h, cell);

      // A neighbour "differs" when it is off-grid or holds a different colour (including the transparent sea).
      private static bool Differs(uint[,] grid, int x, int y, int w, int h, uint cell)
         => x < 0 || y < 0 || x >= w || y >= h || grid[x, y] != cell;

      #endregion
   }
}
