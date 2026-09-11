// Code written by Gabriel Mailhot, 03/09/2026.
//
// WHY IT MATTERS: this is the whole logic of the World Almanac's political minimap. It turns the kingdoms'
// settlements into a coloured territory grid by nearest-settlement assignment (a Voronoi map), with the sea
// masked out; the engine side only gathers the anchors + a water predicate and rasterises the returned grid into
// the panel image. Every way the map can be wrong lives here: a cell painted the wrong kingdom's colour (Voronoi
// boundary off), the ocean bleeding faction colour (water not masked), a kingdom clipped at the image edge (bounds
// not padded), or the map flickering between redraws (a tie resolved non-deterministically). None of that is
// visible to a dotnet test once it is meshes and pixels, so it is pinned here.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.WorldAlmanac;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PoliticalMapPolicyTests
    {
        private const uint Red = 0xFFFF0000;
        private const uint Blue = 0xFF0000FF;

        // A world with no kingdom-held settlement must render an EMPTY map (all transparent), never crash and never
        // paint everything one colour. The floor case.
        [Test]
        public void GIVEN_no_anchors_WHEN_building_THEN_the_grid_is_all_transparent()
        {
            uint[,] grid = PoliticalMapPolicy.BuildColorGrid(
                new List<PoliticalMapAnchor>(), new PoliticalMapBounds {MinX = 0f, MaxX = 10f, MinY = 0f, MaxY = 10f}, 4, 4, null);

            for (var c = 0; c < 4; c++)
                for (var r = 0; r < 4; r++)
                    grid[c, r].Should().Be(PoliticalMapPolicy.NoTerritory);
        }

        // One dominant kingdom fills the whole (land) map: every cell takes its colour when nothing is water.
        [Test]
        public void GIVEN_one_anchor_and_no_water_WHEN_building_THEN_every_cell_takes_its_colour()
        {
            var anchors = new List<PoliticalMapAnchor> {new PoliticalMapAnchor(5f, 5f, Red)};

            uint[,] grid = PoliticalMapPolicy.BuildColorGrid(anchors, new PoliticalMapBounds {MinX = 0f, MaxX = 10f, MinY = 0f, MaxY = 10f}, 4, 4, null);

            for (var c = 0; c < 4; c++)
                for (var r = 0; r < 4; r++)
                    grid[c, r].Should().Be(Red);
        }

        // The Voronoi boundary is the entire point of the map: a cell must take the colour of the kingdom whose
        // settlement is CLOSEST. Two anchors on a line split the row down the middle.
        [Test]
        public void GIVEN_two_anchors_WHEN_building_THEN_each_cell_takes_the_nearest_colour()
        {
            var anchors = new List<PoliticalMapAnchor>
            {
                new PoliticalMapAnchor(0f, 0f, Red),
                new PoliticalMapAnchor(10f, 0f, Blue)
            };

            // 11 columns across x = 0..10, one row: columns left of centre are nearer Red, right nearer Blue.
            uint[,] grid = PoliticalMapPolicy.BuildColorGrid(anchors, new PoliticalMapBounds {MinX = 0f, MaxX = 10f, MinY = 0f, MaxY = 0f}, 11, 1, null);

            grid[0, 0].Should().Be(Red);
            grid[2, 0].Should().Be(Red);
            grid[8, 0].Should().Be(Blue);
            grid[10, 0].Should().Be(Blue);
        }

        // The sea must stay uncoloured (KingdomFrontiers masks water the same way): a cell the predicate reports as
        // water is transparent even when a settlement sits right next to it, or faction colour bleeds into the ocean.
        [Test]
        public void GIVEN_a_water_predicate_WHEN_building_THEN_water_cells_stay_transparent()
        {
            var anchors = new List<PoliticalMapAnchor> {new PoliticalMapAnchor(5f, 0f, Red)};
            Func<float, float, bool> isWater = (x, y) => x < 3f; // the western edge is sea

            uint[,] grid = PoliticalMapPolicy.BuildColorGrid(anchors, new PoliticalMapBounds {MinX = 0f, MaxX = 9f, MinY = 0f, MaxY = 0f}, 10, 1, isWater);

            grid[0, 0].Should().Be(PoliticalMapPolicy.NoTerritory); // x = 0, sea
            grid[9, 0].Should().Be(Red);                            // x = 9, land nearest Red
        }

        // The grid must cover the settled extent plus a margin, or a kingdom sitting on the frontier is clipped off
        // the edge of the image.
        [Test]
        public void GIVEN_anchors_WHEN_computing_bounds_THEN_the_extent_is_padded_on_every_side()
        {
            var anchors = new List<PoliticalMapAnchor>
            {
                new PoliticalMapAnchor(2f, 3f, Red),
                new PoliticalMapAnchor(8f, 7f, Blue)
            };

            PoliticalMapBounds b = PoliticalMapPolicy.ComputeBounds(anchors, 4f);

            b.MinX.Should().Be(-2f);
            b.MaxX.Should().Be(12f);
            b.MinY.Should().Be(-1f);
            b.MaxY.Should().Be(11f);
        }

        // A deterministic map: an exact tie between two settlements must resolve the SAME way every build, or the
        // map flickers between redraws. The rule is "first anchor in the list wins".
        [Test]
        public void GIVEN_a_cell_equidistant_from_two_anchors_WHEN_building_THEN_the_first_anchor_wins()
        {
            var anchors = new List<PoliticalMapAnchor>
            {
                new PoliticalMapAnchor(0f, 0f, Red),
                new PoliticalMapAnchor(2f, 0f, Blue)
            };

            // 3 columns across x = 0..2; the middle column at x = 1 is exactly equidistant from both.
            uint[,] grid = PoliticalMapPolicy.BuildColorGrid(anchors, new PoliticalMapBounds {MinX = 0f, MaxX = 2f, MinY = 0f, MaxY = 0f}, 3, 1, null);

            grid[1, 0].Should().Be(Red); // the first anchor, deterministically
        }

        // Defensive: a settings-driven resolution of zero (or negative) must yield an empty grid, not throw and take
        // the whole panel down.
        [Test]
        public void GIVEN_a_non_positive_dimension_WHEN_building_THEN_it_returns_an_empty_grid_without_throwing()
        {
            var anchors = new List<PoliticalMapAnchor> {new PoliticalMapAnchor(1f, 1f, Red)};

            uint[,] grid = null;
            Action act = () => grid = PoliticalMapPolicy.BuildColorGrid(anchors, new PoliticalMapBounds {MinX = 0f, MaxX = 2f, MinY = 0f, MaxY = 2f}, 0, 4, null);

            act.Should().NotThrow();
            grid.Length.Should().Be(0);
        }

        // The grid-to-world mapping must hit the exact corners (inclusive endpoints) so the sampled area is precisely
        // the padded extent, and a single-cell axis must not divide by zero.
        [Test]
        public void GIVEN_bounds_WHEN_mapping_cells_to_world_THEN_the_corners_and_centre_land_where_expected()
        {
            var bounds = new PoliticalMapBounds {MinX = 0f, MaxX = 100f, MinY = 0f, MaxY = 50f};

            MapPoint x0_y0 = bounds.CellToWorld(new PoliticalMapBounds.GridSample {Col = 0, Row = 0, Width = 11, Height = 6});
            float x0 = x0_y0.X, y0 = x0_y0.Y;
            MapPoint x1_y1 = bounds.CellToWorld(new PoliticalMapBounds.GridSample {Col = 10, Row = 5, Width = 11, Height = 6});
            float x1 = x1_y1.X, y1 = x1_y1.Y;
            float xMid = bounds.CellToWorld(new PoliticalMapBounds.GridSample {Col = 5, Row = 0, Width = 11, Height = 6}).X;

            x0.Should().Be(0f);
            y0.Should().Be(0f);
            x1.Should().Be(100f);
            y1.Should().Be(50f);
            xMid.Should().Be(50f);

            // A single-column axis samples the min edge instead of dividing by (width - 1) = 0.
            float xSingle = bounds.CellToWorld(new PoliticalMapBounds.GridSample {Col = 0, Row = 0, Width = 1, Height = 6}).X;
            xSingle.Should().Be(0f);
        }
    }
}
