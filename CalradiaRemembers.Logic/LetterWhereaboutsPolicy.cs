// Code written by Gabriel Mailhot, 30/09/2026.
// Where a letter's writer is, and where the player is, in words the writer reads. Lordfadooboo (Nexus, 27/09/2026, on
// 2.6.5): "i beat a wanderer in a tournament and later got a letter from them that mentioned my riding in the field with
// THEIR retinue... i am alone. i have not hired a single follower." The phrase for the writer is in the second person
// ("on the march with your own retinue", addressed to the writer), and the same phrase was reused for the PLAYER: the
// letter read "The player is presently in the field, on the march with your own retinue", and "your" is the writer.
// Two phrases now, one per person, and the player's says "alone" when the player is.

namespace CalradiaRemembers.Logic
{
   /// <summary>Where someone is, as the letter needs it.</summary>
   public sealed class WhereaboutsFacts
   {
      /// <summary>The settlement they are in or staying at; null when none.</summary>
      public string SettlementName { get; set; }

      /// <summary>They travel in a party.</summary>
      public bool InParty { get; set; }

      /// <summary>Their party marches in an army.</summary>
      public bool InArmy { get; set; }

      /// <summary>How many people their party holds, themselves included (1 = alone).</summary>
      public int MenInParty { get; set; }

      /// <summary>Their home settlement, for someone with no party who is nowhere else; null when none.</summary>
      public string HomeName { get; set; }
   }

   /// <summary>The whereabouts wording of a letter. Pure.</summary>
   public static class LetterWhereaboutsPolicy
   {
      /// <summary>
      ///   Where the WRITER is, addressed to the writer ("within Pravend", "in the field, on the march with your own
      ///   retinue"); null when it cannot be said with confidence. Unchanged wording, now only ever used for the writer.
      /// </summary>
      public static string ForWriter(WhereaboutsFacts f)
      {
         if (f == null) return null;
         if (!string.IsNullOrWhiteSpace(f.SettlementName)) return $"within {f.SettlementName}";
         if (f.InParty)
            return f.InArmy ? "on campaign with the army, in the field" : "in the field, on the march with your own retinue";

         return string.IsNullOrWhiteSpace(f.HomeName) ? null : $"at home in {f.HomeName}";
      }

      /// <summary>
      ///   Where the PLAYER is, in the third person, never borrowing the writer's "your": riding alone when the party is
      ///   the player alone, at the head of their own company when it holds others. Null when it cannot be said.
      /// </summary>
      public static string ForPlayer(WhereaboutsFacts f)
      {
         if (f == null) return null;
         if (!string.IsNullOrWhiteSpace(f.SettlementName)) return $"within {f.SettlementName}";
         if (!f.InParty) return null;
         if (f.InArmy) return "on campaign with an army, in the field";

         return f.MenInParty <= 1
            ? "in the field, riding alone, with no one at their back"
            : "in the field, at the head of their own company";
      }
   }
}
