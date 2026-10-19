// Code written by Gabriel Mailhot, 04/09/2026.

#region

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.WorldAlmanac;
using LibColor = TaleWorlds.Library.Color;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Rasterises a political-map colour grid to PNG bytes. Engine-adjacent (System.Drawing); the grid logic it
   ///   renders is the unit-tested PoliticalMapPolicy.
   /// </summary>
   public static class PoliticalMapImageRenderer
   {
      // Bump whenever the rendered look changes: the cache filename includes it, so a new style is never masked by a
      // stale PNG written by an older renderer (the content signature only sees the grid, not the paint).
      public const int RenderVersion = 6;

      // How much a land fill is pulled toward parchment (0 = raw, saturated banner colour, 1 = plain parchment). Low
      // so each region reads as its faction's DOMINANT colour (Vlandia red, Aserai yellow) with only a hint of
      // parchment for cohesion; raise it toward 0.4 for the softer historical-atlas pastel.
      private const float LandSoftness = 0.15f;

      // The cell bitmap is upscaled this much with bicubic filtering before saving: the panel then stretches an
      // already-smooth image instead of stair-stepped 1-pixel cells.
      private const int OutputScale = 2;
      private static readonly Color BorderColor = Color.FromArgb(255, 74, 60, 48);
      private static readonly Color Parchment = Color.FromArgb(255, 238, 230, 212);

      // Historical-atlas palette (per the 2026-09 reference map the user supplied): the sea is the parchment beige
      // (a blue sea collided with the Aserai turquoise, whose desert read as water), land fills are softened toward
      // that same parchment so the hue stays the faction's but the value goes pastel, and one uniform ink line marks
      // every frontier and coastline.
      private static readonly Color SeaColor = Color.FromArgb(255, 244, 222, 204);

      /// <summary>
      ///   A stable signature of the grid's contents, for cache invalidation: the map only needs re-rendering when a
      ///   conquest changes who owns what, which changes the grid, which changes this. FNV-1a over the cells.
      /// </summary>
      public static uint ComputeSignature(uint[,] grid)
      {
         if (grid == null) return 0u;

         var hash = 2166136261u;
         int w = grid.GetLength(0);
         int h = grid.GetLength(1);
         for (var x = 0; x < w; x++)
         {
            for (var y = 0; y < h; y++)
            {
               uint c = grid[x, y];
               for (var b = 0; b < 4; b++)
               {
                  hash ^= c >> b * 8 & 0xFFu;
                  hash *= 16777619u;
               }
            }
         }

         return hash;
      }

      /// <summary>
      ///   Renders <paramref name="grid" /> to a 32-bit PNG: sea cells a parchment beige, land cells the kingdom's
      ///   banner colour softened toward parchment, and (when <paramref name="drawBorders" />) every frontier and
      ///   coastline inked in one uniform dark line, historical-atlas style.
      ///   The cell bitmap is upscaled <see cref="OutputScale" />x with bicubic filtering for smooth edges on the
      ///   panel. Returns null on an empty grid or any draw failure, so the caller can fall back to no map rather
      ///   than crash the panel.
      /// </summary>
      public static byte[] RenderPng(uint[,] grid, bool drawBorders = true)
      {
         if (grid == null) return null;

         int w = grid.GetLength(0);
         int h = grid.GetLength(1);

         if (w < 1 || h < 1) return null;

         try
         {
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (var x = 0; x < w; x++)
            {
               for (var y = 0; y < h; y++)
               {
                  uint cell = grid[x, y];

                  Color pixel;
                  if (cell == PoliticalMapPolicy.NoTerritory) pixel = SeaColor;
                  else
                     pixel = drawBorders && IsEdge(grid, x, y, w, h, cell)
                        ? BorderColor
                        : Soften(ToColor(cell));

                  // North is up: grid row 0 is world-south, so it lands at the BOTTOM of the image.
                  bmp.SetPixel(x, h - 1 - y, pixel);
               }
            }

            using var scaled = new Bitmap(w * OutputScale, h * OutputScale, PixelFormat.Format32bppArgb);
            using Graphics g = Graphics.FromImage(scaled);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half; // centred sampling: no half-pixel drift on the upscale
            g.DrawImage(bmp, 0, 0, scaled.Width, scaled.Height);

            using var ms = new MemoryStream();
            scaled.Save(ms, ImageFormat.Png);

            return ms.ToArray();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[POLITICAL_MAP] rasterise failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #region private

      private static int Channel(float f)
      {
         var v = (int) (f * 255f + 0.5f);

         return v < 0 ? 0 : v > 255 ? 255 : v;
      }

      // A neighbour "differs" when it is off-grid or holds a different colour (including the sea).
      private static bool Differs(uint[,] grid, int x, int y, int w, int h, uint cell)
         => x < 0 || y < 0 || x >= w || y >= h || grid[x, y] != cell;

      // True when any 4-neighbour differs from this cell (another faction, the sea, or off the grid): frontiers and
      // coastlines alike get the same ink line, atlas-style.
      private static bool IsEdge(uint[,] grid, int x, int y, int w, int h, uint cell)
         => Differs(grid, x - 1, y, w, h, cell) || Differs(grid, x + 1, y, w, h, cell) || Differs(grid, x, y - 1, w, h, cell) || Differs(grid, x, y + 1, w, h, cell);

      // Pulls a banner colour toward parchment, keeping the faction's hue but landing the fill in the soft pastel
      // range of a historical atlas (saturated fills would shout against the panel).
      private static Color Soften(Color c)
         => Color.FromArgb(255,
            (int) (c.R + (Parchment.R - c.R) * LandSoftness),
            (int) (c.G + (Parchment.G - c.G) * LandSoftness),
            (int) (c.B + (Parchment.B - c.B) * LandSoftness));

      // The kingdom colour, forced opaque so a faction's fill never shows the frame through it. Decoded via the
      // engine's own Color.FromUint so we never guess byte order.
      private static Color ToColor(uint colorArgb)
      {
         LibColor c = LibColor.FromUint(colorArgb);

         return Color.FromArgb(255, Channel(c.Red), Channel(c.Green), Channel(c.Blue));
      }

      #endregion
   }
}