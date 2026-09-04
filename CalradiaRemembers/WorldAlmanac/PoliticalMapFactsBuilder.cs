// Code written by Gabriel Mailhot, 03/09/2026.
// World Almanac political minimap, the engine seam (Inc B): gathers the live settlement anchors the pure
// PoliticalMapPolicy needs (position + owning kingdom's colour), supplies a water predicate from the map scene, and
// returns the colour grid. Reading the world is engine-bound and untestable by dotnet; the assignment logic it
// feeds is the pure, unit-tested PoliticalMapPolicy. Mirrors KingdomFrontiers' territory gather (settlement anchors
// + MapSceneWrapper terrain lookup), reimplemented so CR rasterises the grid into a 2D panel image (Inc C).

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Builds the political-map colour grid from the live campaign: one anchor per kingdom-held settlement, sea masked.</summary>
   public static class PoliticalMapFactsBuilder
   {
      /// <summary>Default grid resolution (cells per side). KingdomFrontiers uses 150 on the 3D map; a panel bitmap upscales fine.</summary>
      public const int DefaultResolution = 200;

      /// <summary>
      ///   One anchor per town/castle/village that belongs to a kingdom: its map position and that kingdom's colour.
      ///   Empty when no campaign is running. Never throws (a bad settlement is skipped, not fatal).
      /// </summary>
      public static List<PoliticalMapAnchor> GatherAnchors()
      {
         var anchors = new List<PoliticalMapAnchor>();
         if (Campaign.Current?.Settlements == null) return anchors;

         foreach (Settlement s in Campaign.Current.Settlements)
         {
            try
            {
               if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;

               Kingdom k = ResolveKingdom(s);
               if (k == null) continue;

               Vec2 p = s.GetPosition2D;
               anchors.Add(new PoliticalMapAnchor(p.X, p.Y, k.Color));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[POLITICAL_MAP] skipped a settlement while gathering anchors: {ex.GetType().Name}: {ex.Message}");
            }
         }

         return anchors;
      }

      /// <summary>
      ///   The full colour grid for the current world at <paramref name="resolution" /> cells per side: each land
      ///   cell coloured by the nearest kingdom's settlement, the sea left transparent. An empty world yields an
      ///   all-transparent grid (the pure policy's floor case).
      /// </summary>
      public static uint[,] BuildColorGrid(int resolution = DefaultResolution)
      {
         List<PoliticalMapAnchor> anchors = GatherAnchors();
         PoliticalMapBounds bounds = PoliticalMapPolicy.ComputeBounds(anchors);

         return PoliticalMapPolicy.BuildColorGrid(anchors, bounds, resolution, resolution, IsWater);
      }

      #region private

      // A settlement's kingdom: its map faction if that is a kingdom, else its owner clan's kingdom. Null for an
      // unaffiliated settlement (rebel/no kingdom), which the caller drops from the anchor set.
      private static Kingdom ResolveKingdom(Settlement s)
      {
         if (s.MapFaction is Kingdom mapKingdom) return mapKingdom;
         if (s.OwnerClan?.Kingdom != null) return s.OwnerClan.Kingdom;

         return null;
      }

      // True when the map point is sea or a large lake, so the political map leaves it transparent rather than
      // bleeding faction colour into the water. Rivers/beaches/fording stay land (they sit inside territory).
      // Reads the live map scene; any failure treats the point as land (colour it) rather than punching a hole.
      private static bool IsWater(float worldX, float worldY)
      {
         try
         {
            var scene = Campaign.Current?.MapSceneWrapper;
            if (scene == null) return false;

            // The terrain query takes a CampaignVec2 whose isOnLand flag can snap the sample to the land or sea
            // navmesh; sample BOTH ways and call it water if EITHER reports a sea type (the same both-ways robustness
            // KingdomFrontiers uses), so a point over the sea is not missed by a flag snapping it onto a coast.
            var pos = new Vec2(worldX, worldY);

            return IsSea(scene.GetTerrainTypeAtPosition(new CampaignVec2(pos, false)))
                   || IsSea(scene.GetTerrainTypeAtPosition(new CampaignVec2(pos, true)));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[POLITICAL_MAP] water lookup failed at ({worldX:0.#},{worldY:0.#}), treating as land: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      // The sea and large lakes are masked out of the political map; rivers/beaches/fording stay land (they sit
      // inside territory).
      private static bool IsSea(TerrainType terrain)
         => terrain == TerrainType.Water
            || terrain == TerrainType.Lake
            || terrain == TerrainType.CoastalSea
            || terrain == TerrainType.OpenSea;

      #endregion
   }
}
