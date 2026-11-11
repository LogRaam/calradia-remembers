// Code written by Gabriel Mailhot, 10/11/2026.
// The scout_place task: ride close to a town, castle or village of another realm and report what it holds.
//
// bdionysis80 (Nexus, 10/11/2026): a war council pledged to scout Hvalvik's garrison, and no task could hold it (the
// nearest, scout_army, is an army in the field; 2.8.8 refused rather than bind another realm's army). Gabriel
// (10/11/2026): "on fait le type de quete eclairer un lieu". Scout_town stays the other job, done inside an enemy town
// through its gang leader; this one is seen from outside, so it needs no war and no gang leader.
//
// Who may ask: any giver, for a real town, castle or village held by a realm other than their own (one does not send a
// scout to look at one's own walls) and other than the player's (the player sees their own realm's places already).
// What is reported: what a scout riding by would see and count, read from the game the moment the player is in sight
// (the owner, the garrison and militia, the walls, how long the food lasts; for a village its militia and hearths), so
// the giver hears a true report rather than one the model makes up.

namespace CalradiaRemembers.Logic.Quests
{
   /// <summary>The place a scout_place task names, and how it stands. One fact per line.</summary>
   public sealed class ScoutPlaceFacts
   {
      /// <summary>The named place is a real town, castle or village of the world.</summary>
      public bool IsTownCastleOrVillage { get; init; }

      /// <summary>The place is held by the giver's own realm: nothing to scout.</summary>
      public bool HeldByGiversRealm { get; init; }

      /// <summary>The place is held by the player's own realm: the player already knows it.</summary>
      public bool HeldByPlayersRealm { get; init; }
   }

   /// <summary>What the player's scout saw of the place. One fact per line; null where the game has no such thing there.</summary>
   public sealed class ScoutPlaceSighting
   {
      /// <summary>The place's name.</summary>
      public string Place { get; init; }

      /// <summary>"town", "castle" or "village".</summary>
      public string Kind { get; init; }

      /// <summary>The clan that holds it, or null.</summary>
      public string OwnerClan { get; init; }

      /// <summary>The realm it belongs to, or null.</summary>
      public string Realm { get; init; }

      /// <summary>Men in the garrison, or null for a village.</summary>
      public int? Garrison { get; init; }

      /// <summary>The militia.</summary>
      public int Militia { get; init; }

      /// <summary>The wall level (1 to 3), or null for a village.</summary>
      public int? WallLevel { get; init; }

      /// <summary>Days the food lasts at the current loss, or null when it is not falling (or a village).</summary>
      public int? FoodDays { get; init; }

      /// <summary>A village's hearths, or null for a town or castle.</summary>
      public int? Hearths { get; init; }

      /// <summary>The game day the place was seen.</summary>
      public int Day { get; init; }
   }

   /// <summary>Decides whether a scout_place task may be set, and words what the scout saw. Pure.</summary>
   public static class ScoutPlacePolicy
   {
      /// <summary>TUNE: map units within which the player's party has the place in sight (the same reach as scouting an army).</summary>
      public const float SightRange = 3.0f;

      /// <summary>A real town, castle or village held by neither the giver's realm nor the player's.</summary>
      public static bool MayAsk(ScoutPlaceFacts facts)
         => facts != null && facts.IsTownCastleOrVillage && !facts.HeldByGiversRealm && !facts.HeldByPlayersRealm;

      /// <summary>
      ///   The report the giver reads (the task's evidence): "Scouted the castle of Hvalvik on day 812: held by the Gundaroving
      ///   of the Nords; a garrison of about 140, militia about 60, walls of level 2, food for about 18 days."
      /// </summary>
      public static string Report(ScoutPlaceSighting s)
      {
         if (s == null) return "";

         string holder = s.OwnerClan != null && s.Realm != null ? $"held by the {s.OwnerClan} of the {s.Realm}"
            : s.Realm != null ? $"held by the {s.Realm}"
            : s.OwnerClan != null ? $"held by the {s.OwnerClan}"
            : "its holder unknown";

         var seen = new System.Collections.Generic.List<string>();
         if (s.Garrison is int g) seen.Add($"a garrison of about {Round(g)}");
         seen.Add($"militia about {Round(s.Militia)}");
         if (s.WallLevel is int w) seen.Add($"walls of level {w}");
         if (s.FoodDays is int f) seen.Add($"food for about {f} days");
         if (s.Hearths is int h) seen.Add($"about {Round(h)} hearths");

         return $"Scouted the {s.Kind ?? "place"} of {s.Place} on day {s.Day}: {holder}; {string.Join(", ", seen)}";
      }

      // A scout counts at a distance: to the nearest ten.
      private static int Round(int n) => n < 10 ? n : (n + 5) / 10 * 10;
   }
}
