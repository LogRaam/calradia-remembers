// Code written by Gabriel Mailhot, 03/09/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
    /// <summary>One settlement's contribution to the political map: its world position and its kingdom's colour.</summary>
    public readonly struct PoliticalMapAnchor
    {
        public PoliticalMapAnchor(float x, float y, uint colorArgb)
        {
            X = x;
            Y = y;
            ColorArgb = colorArgb;
        }

        public float X { get; }
        public float Y { get; }

        /// <summary>The owning kingdom's colour, carried through opaquely (the rasteriser decides the byte order).</summary>
        public uint ColorArgb { get; }
    }

    /// <summary>The world-space rectangle the grid samples over. Grid indices map linearly onto this box.</summary>
    public readonly struct PoliticalMapBounds
    {
        public float MinX { get; init; }
        public float MaxX { get; init; }
        public float MinY { get; init; }
        public float MaxY { get; init; }

        /// <summary>
        ///   Which cell of which grid is being sampled. FOUR INTS in a row, two coordinates and two extents,
        ///   and the pairs are interleaved in the worst way: col is divided by width and row by height, so
        ///   crossing the halves produces a perfectly valid fraction of the wrong axis and the political map
        ///   comes out transposed. Named rather than positional (2026-09-10).
        /// </summary>
        public sealed class GridSample
        {
            /// <summary>The cell's column, 0-based.</summary>
            public int Col { get; set; }

            /// <summary>The cell's row, 0-based.</summary>
            public int Row { get; set; }

            /// <summary>How many columns the grid has. A single-column grid samples the min edge.</summary>
            public int Width { get; set; }

            /// <summary>How many rows the grid has. A single-row grid samples the min edge.</summary>
            public int Height { get; set; }
        }

        /// <summary>
        ///   The world point a grid cell samples. Column 0 maps to <see cref="MinX" />, column width-1 to
        ///   <see cref="MaxX" /> (inclusive endpoints), and likewise for rows. A single-cell axis samples the min
        ///   edge rather than dividing by zero.
        /// </summary>
        public MapPoint CellToWorld(GridSample sample)
        {
            if (sample == null) return new MapPoint(MinX, MinY);

            float fx = sample.Width <= 1 ? 0f : (float) sample.Col / (sample.Width - 1);
            float fy = sample.Height <= 1 ? 0f : (float) sample.Row / (sample.Height - 1);

            return new MapPoint(MinX + fx * (MaxX - MinX), MinY + fy * (MaxY - MinY));
        }
    }

    /// <summary>
    ///   The pure heart of the World Almanac political map: turns the kingdoms' settlements into a coloured
    ///   territory grid by nearest-settlement assignment (a Voronoi political map), with the sea masked out. It is
    ///   engine-free (the caller supplies the settlement anchors (position + kingdom colour) and a water predicate),
    ///   so the whole assignment is unit-tested here rather than only eyeballed on the rendered map. The rasteriser
    ///   (engine side) turns the returned grid into a bitmap; this class never touches an image.
    ///   <para>
    ///     Approach mirrors the territory computation the KingdomFrontiers mod uses on the live 3D map (grid +
    ///     nearest-settlement Voronoi + water mask), reimplemented pure so CR can render it into a 2D panel image
    ///     instead of terrain meshes.
    ///   </para>
    /// </summary>
    public static class PoliticalMapPolicy
    {
        /// <summary>A cell that belongs to no kingdom (sea, or a world with no kingdom-held settlement): fully transparent.</summary>
        public const uint NoTerritory = 0u;

        /// <summary>Padding (world units) added around the settled extent so no edge kingdom is clipped. Matches KingdomFrontiers' 4.</summary>
        public const float DefaultBoundsPadding = 4f;

        /// <summary>
        ///   The bounding box of the anchor positions, expanded by <paramref name="padding" /> on every side so a
        ///   kingdom sitting on the frontier is not cut off at the image edge. An empty anchor list yields a zero box.
        /// </summary>
        public static PoliticalMapBounds ComputeBounds(IReadOnlyList<PoliticalMapAnchor> anchors, float padding = DefaultBoundsPadding)
        {
            if (anchors == null || anchors.Count == 0) return new PoliticalMapBounds {MinX = 0f, MaxX = 0f, MinY = 0f, MaxY = 0f};

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (var i = 0; i < anchors.Count; i++)
            {
                PoliticalMapAnchor a = anchors[i];
                if (a.X < minX) minX = a.X;
                if (a.X > maxX) maxX = a.X;
                if (a.Y < minY) minY = a.Y;
                if (a.Y > maxY) maxY = a.Y;
            }

            return new PoliticalMapBounds {MinX = minX - padding, MaxX = maxX + padding, MinY = minY - padding, MaxY = maxY + padding};
        }

        /// <summary>
        ///   Builds a <paramref name="width" /> x <paramref name="height" /> colour grid: each cell takes the colour
        ///   of the NEAREST anchor (squared distance), unless <paramref name="isWater" /> reports its world point as
        ///   sea, in which case it stays <see cref="NoTerritory" />. A tie between two anchors resolves to the one
        ///   earlier in the list, so the same inputs always produce the same map (no flicker between redraws). No
        ///   anchors, or a non-positive dimension, yields an all-transparent grid rather than throwing.
        /// </summary>
        /// <param name="isWater">World (x, y) to true when that point is sea; null treats everything as land.</param>
        public static uint[,] BuildColorGrid(
            IReadOnlyList<PoliticalMapAnchor> anchors,
            PoliticalMapBounds bounds,
            int width,
            int height,
            Func<float, float, bool> isWater)
        {
            if (width < 1 || height < 1) return new uint[0, 0];

            var grid = new uint[width, height];
            if (anchors == null || anchors.Count == 0) return grid; // all NoTerritory

            for (var col = 0; col < width; col++)
            {
                for (var row = 0; row < height; row++)
                {
                    MapPoint here = bounds.CellToWorld(new PoliticalMapBounds.GridSample
                       {Col = col, Row = row, Width = width, Height = height});
                    float wx = here.X, wy = here.Y;

                    if (isWater != null && isWater(wx, wy))
                    {
                        grid[col, row] = NoTerritory;
                        continue;
                    }

                    grid[col, row] = NearestColor(anchors, wx, wy);
                }
            }

            return grid;
        }

        // The colour of the anchor closest to (wx, wy) by squared distance. Strictly-less keeps the FIRST of any tie,
        // so the assignment is deterministic across redraws.
        private static uint NearestColor(IReadOnlyList<PoliticalMapAnchor> anchors, float wx, float wy)
        {
            uint best = NoTerritory;
            float bestDistSq = float.MaxValue;

            for (var i = 0; i < anchors.Count; i++)
            {
                PoliticalMapAnchor a = anchors[i];
                float dx = wx - a.X;
                float dy = wy - a.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = a.ColorArgb;
                }
            }

            return best;
        }
    }
}
