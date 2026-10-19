// Code written by Gabriel Mailhot, 08/10/2026.
// What a captor remembers of taking the player. Windmolen (Nexus, 08/10/2026): taken after a battle of a hundred men a
// side, the captor called him a lone spy; he had no memory of the battle (none is written for a lord the player never
// spoke with) and nothing told him how he took the player. The capture is now his memory, in his words.

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>What is known of the capture.</summary>
   public sealed class CaptureFacts
   {
      public string PlayerName { get; init; }

      /// <summary>The nearest place, when known.</summary>
      public string Place { get; init; }

      /// <summary>The player was taken at the end of a battle they fought (not alone on the road, not a surrender).</summary>
      public bool AfterBattle { get; init; }

      /// <summary>The men the player led into that battle.</summary>
      public int PlayerMen { get; init; }
   }

   /// <summary>The captor's memory of the capture. Pure.</summary>
   public static class CaptureMemoryText
   {
      /// <summary>TUNE: from this many men, the player is said to have led a company.</summary>
      public const int CompanyFrom = 10;

      public static string Compose(CaptureFacts f)
      {
         string who = string.IsNullOrWhiteSpace(f?.PlayerName) ? "the player" : f.PlayerName.Trim();
         string near = string.IsNullOrWhiteSpace(f?.Place) ? "" : $" near {f.Place.Trim()}";
         if (f == null || !f.AfterBattle) return $"I took {who} prisoner{near}.";

         string host = f.PlayerMen >= CompanyFrom ? $", where they led some {f.PlayerMen} men," : "";

         return $"I defeated {who} in battle{near}{host} and took them prisoner.";
      }
   }
}
