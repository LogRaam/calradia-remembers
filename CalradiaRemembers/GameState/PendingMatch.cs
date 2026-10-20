// Code written by Gabriel Mailhot, 01/10/2026.

namespace CalradiaRemembers.GameState
{
   /// <summary>A match some lord has sent the player to broker, as the one with the say over the lady reads it.</summary>
   public sealed class PendingMatch
   {
      /// <summary>The lady (or the one to be wed).</summary>
      public string BrideName { get; set; }

      /// <summary>The suitor who sent the player.</summary>
      public string SuitorName { get; set; }

      /// <summary>
      ///   The one taught speaks for the groom and asks the PLAYER, who holds the say over the bride (tashmetu, 20/10/2026:
      ///   the player's daughter or sister).
      /// </summary>
      public bool AsksThePlayer { get; set; }

      /// <summary>In a match that asks the player, the one taught is the groom himself (not his father or his head).</summary>
      public bool AskerIsTheSuitor { get; set; }
   }
}
