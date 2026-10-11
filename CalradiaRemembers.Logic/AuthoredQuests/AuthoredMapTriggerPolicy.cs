// Code written by Gabriel Mailhot, 11/10/2026.
// A tale that begins by itself on the map rather than in a conversation (Gabriel, 10/10/2026: the player's party waits
// on the map in his wife's home region, by day, and there is a chance the tale begins: "si le joueur est marie a une
// Khuzait, et qu'il attend dans les steppes, alors il y a des chances que le conte debute"). The tale names how long the
// party must have stood still, whether it must be day, whether the land must be of the giver's own people, and the
// chance each day. Once the conditions hold, the dice are cast once that day, for all such tales together.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Where the player's party is now, for a tale that begins on the map.</summary>
   public sealed class MapTriggerFacts
   {
      /// <summary>Hours the party has stood in the same place.</summary>
      public int StillHours { get; init; }

      public bool IsDay { get; init; }

      /// <summary>Inside a town, castle or village: not out in the open.</summary>
      public bool InSettlement { get; init; }

      /// <summary>In a battle or a siege.</summary>
      public bool InBattle { get; init; }

      public bool AtSea { get; init; }

      /// <summary>The nearest settlement is of the giver's own culture.</summary>
      public bool InGiversLands { get; init; }

      /// <summary>The name of that nearest settlement's culture, for the author's why-not line.</summary>
      public string LandsCulture { get; init; }
   }

   /// <summary>Whether a tale that begins on the map may begin here and now, the dice aside. Pure.</summary>
   public static class AuthoredMapTriggerPolicy
   {
      /// <summary>Null when it may; otherwise the first thing that stands in the way, in plain words.</summary>
      public static string WhyNot(AuthoredMapTrigger t, MapTriggerFacts f)
      {
         if (t == null) return "the tale does not begin on the map";
         f ??= new MapTriggerFacts();

         if (f.AtSea) return "your party is at sea";
         if (f.InBattle) return "your party is in a battle or a siege";
         if (f.InSettlement) return "your party is inside a settlement, not out in the open";
         if (t.ByDay && !f.IsDay) return "it is night, and the tale begins by day";
         if (f.StillHours < t.StillHours) return $"your party has stood still {f.StillHours} hours of the {t.StillHours} the tale waits for";
         if (t.InGiversLands && !f.InGiversLands) return $"these are not the giver's own lands (the nearest place is {f.LandsCulture ?? "of no known people"})";

         return null;
      }

      /// <summary>The day's single roll: the tale begins when the roll falls under its chance.</summary>
      public static bool Begins(double chancePerDay, double roll) => roll < chancePerDay;
   }
}
