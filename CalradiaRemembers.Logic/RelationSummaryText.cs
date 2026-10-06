// Code written by Gabriel Mailhot, 06/10/2026.
// The chat header's line of standings. Fakade (Nexus, 06/10/2026), with True Relations: a character at +10 with him
// showed -61, his clan leader's relation. The character's own relation is shown beside the clan's when it differs (only
// under a per-hero relation mod), so the player sees the number the character is told; otherwise the line is as before.

namespace CalradiaRemembers.Logic
{
   /// <summary>The standings the header line is written from.</summary>
   public sealed class RelationSummaryFacts
   {
      /// <summary>The clan's standing toward the player (its leader's relation).</summary>
      public int Clan { get; init; }

      /// <summary>The character's own game relation, when it differs from the clan's; null otherwise.</summary>
      public int? Own { get; init; }

      /// <summary>CR's own personal regard, built in conversation.</summary>
      public int Personal { get; init; }
   }

   /// <summary>Writes the header line. Pure.</summary>
   public static class RelationSummaryText
   {
      public static string Compose(RelationSummaryFacts f)
      {
         if (f == null) return "";
         string own = f.Own is int o ? $"   ·   Own {o:+#;-#;0}" : "";

         return $"Clan {f.Clan:+#;-#;0}{own}   ·   Personal {f.Personal:+#;-#;0}";
      }
   }
}
