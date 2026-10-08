// Code written by Gabriel Mailhot, 08/10/2026.
// Where a staged conversation in a settlement opens (Gabriel's play test, 08/10/2026: a post-duel conversation with a
// notable opened in the lord's hall, where he never stands, and the chat opened without him). Where the character is,
// when the game knows it; otherwise the former choice: the tavern for a companion, else the hall, the village centre,
// the tavern.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What the choice of location is made on.</summary>
   public sealed class AudienceLocationFacts
   {
      /// <summary>The location id the game places the character in, or null.</summary>
      public string WhereTheCharacterIs { get; init; }

      /// <summary>The location ids the settlement has.</summary>
      public IReadOnlyList<string> Available { get; init; }

      /// <summary>A companion's private word belongs in the tavern when the character is placed nowhere.</summary>
      public bool PreferTavern { get; init; }
   }

   /// <summary>Picks the location a staged conversation opens at. Pure.</summary>
   public static class AudienceLocationPolicy
   {
      /// <summary>The location id, or null when the settlement has none to offer.</summary>
      public static string Choose(AudienceLocationFacts f)
      {
         IReadOnlyList<string> available = f?.Available ?? new string[0];
         bool Has(string id) => id != null && available.Contains(id);

         if (Has(f?.WhereTheCharacterIs) && f.WhereTheCharacterIs != "prison") return f.WhereTheCharacterIs;

         IEnumerable<string> order = f?.PreferTavern == true
            ? new[] {"tavern", "lordshall", "village_center"}
            : new[] {"lordshall", "village_center", "tavern"};

         return order.FirstOrDefault(Has);
      }
   }
}
