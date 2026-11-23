// Code written by Gabriel Mailhot, 07/10/2026.
// The house a character was born into, when it is not the one they belong to now. Maifailun (Nexus, 07/10/2026): Lidia,
// wed into the Takhi, knew her father's name but not his house, and told the player she was born a Takhi. The family
// line now says where such a character came from.

namespace CalradiaRemembers.Logic
{
   /// <summary>What is known of the house a character was born into.</summary>
   public sealed class BirthHouseFacts
   {
      /// <summary>The house the character belongs to now.</summary>
      public string CurrentClan { get; init; }

      /// <summary>The house the character was born into (the engine's origin clan, else the father's).</summary>
      public string BirthClan { get; init; }

      /// <summary>The realm that birth house serves, when known ("the Western Empire").</summary>
      public string BirthRealm { get; init; }

      /// <summary>The character is or was married: the move between houses is told as a marriage only then.</summary>
      public bool ByMarriage { get; init; }

      /// <summary>Married in from a house the game does not name (BirthClanSource.MarriedInUnknownHouse); BirthClan is then empty.</summary>
      public bool HouseUnknown { get; init; }
   }

   /// <summary>The family line's note on the birth house. Pure.</summary>
   public static class BirthHouseText
   {
      /// <summary>"Born into the Comnos (of the Western Empire); joined the Takhi by marriage.", or null when there is nothing to say.</summary>
      public static string Line(BirthHouseFacts f)
      {
         if (f == null || string.IsNullOrWhiteSpace(f.CurrentClan)) return null;

         // EvilPoptart1987 (21/11/2026): a wife married before the campaign began, with no parent the game knows, spoke of
         // growing up in her husband's house. Her own house cannot be named, but where she did NOT grow up can.
         if (f.HouseUnknown && f.ByMarriage) return $"Not born into the {f.CurrentClan.Trim()}: joined it by marriage, from another house; never speak of growing up among the {f.CurrentClan.Trim()}.";

         if (string.IsNullOrWhiteSpace(f.BirthClan)) return null;
         string born = f.BirthClan.Trim(), now = f.CurrentClan.Trim();
         if (string.Equals(born, now, System.StringComparison.OrdinalIgnoreCase)) return null;

         string realm = string.IsNullOrWhiteSpace(f.BirthRealm) ? "" : $" (of {f.BirthRealm.Trim()})";

         return f.ByMarriage ? $"Born into the {born}{realm}; joined the {now} by marriage." : $"Born into the {born}{realm}; now of the {now}.";
      }
   }
}
