// Code written by Gabriel Mailhot, 07/10/2026.
// Where a tale's scene happens, and who walks into it. fkasad (Nexus, 07/10/2026): "Picture an Aserai character
// scouting a forest at night compared to a Battanian character doing the same. Or a Nord or Sturgian crawling through
// the sand to approach an enemy camp. Even a guy from Danustica lost in the snow." The scene's own canvas says what the
// place is; the game knows the ground under the player, the season, the hour and the player's people, and the narrator
// is told them so the same wood reads differently to different feet.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What the game knows of a scene's surroundings. Every field may be missing.</summary>
   public sealed class SceneSurroundingsFacts
   {
      /// <summary>The engine's name for the ground under the player ("Forest", "Dune"...).</summary>
      public string EngineTerrain { get; init; }

      /// <summary>The season, as the calendar names it.</summary>
      public string Season { get; init; }

      /// <summary>The hour of the day, 0 to 23; -1 when unknown.</summary>
      public int Hour { get; init; } = -1;

      /// <summary>The player's people ("Aserai").</summary>
      public string PlayerCulture { get; init; }
   }

   /// <summary>The narrator's block about the scene's surroundings. Pure.</summary>
   public static class SceneSurroundingsText
   {
      private static readonly IReadOnlyDictionary<string, string> Words = new Dictionary<string, string> {
         ["Forest"] = "forest",
         ["Snow"] = "snow-covered country",
         ["Desert"] = "desert sand",
         ["Dune"] = "desert sand",
         ["Steppe"] = "open steppe",
         ["Plain"] = "open plain",
         ["RuralArea"] = "open farmland",
         ["Mountain"] = "mountain ground",
         ["Canyon"] = "a rocky canyon",
         ["Swamp"] = "marshland",
         ["Beach"] = "the shore",
         ["River"] = "a riverbank",
         ["ShallowRiver"] = "a riverbank",
         ["Fording"] = "a ford"
      };

      /// <summary>The engine's ground as words a narrator can use; null for water or anything unknown.</summary>
      public static string TerrainWord(string engineTerrain)
         => engineTerrain != null && Words.TryGetValue(engineTerrain, out string w) ? w : null;

      /// <summary>The part of the day an hour falls in; null when the hour is unknown.</summary>
      public static string PartOfDay(int hour)
         => hour < 0 || hour > 23 ? null
            : hour < 5 ? "night"
            : hour < 8 ? "dawn"
            : hour < 12 ? "morning"
            : hour < 17 ? "afternoon"
            : hour < 20 ? "dusk"
            : "night";

      /// <summary>The block for the narrator's prompt, or null when nothing is known.</summary>
      public static string Compose(SceneSurroundingsFacts f)
      {
         if (f == null) return null;

         var where = new List<string>();
         string ground = TerrainWord(f.EngineTerrain);
         if (ground != null) where.Add(ground);
         if (!string.IsNullOrWhiteSpace(f.Season)) where.Add("in " + f.Season.Trim().ToLowerInvariant());
         string part = PartOfDay(f.Hour);
         if (part != null) where.Add(part == "night" ? "at night" : "at " + part);

         var lines = new List<string>();
         if (where.Count > 0)
            lines.Add("THE SURROUNDINGS: " + string.Join(", ", where) + ". Let the ground, the season and the light show in what is seen, heard and felt.");
         if (!string.IsNullOrWhiteSpace(f.PlayerCulture))
            lines.Add($"THE PLAYER IS {f.PlayerCulture.Trim()}. Let where they come from colour what they notice and how they fare: what their people know well, and what is strange to their people here. A touch now and then, never a lecture.");

         return lines.Count == 0 ? null : string.Join("\n", lines);
      }
   }
}
