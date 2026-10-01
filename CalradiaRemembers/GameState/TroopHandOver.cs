// Code written by Gabriel Mailhot, 01/10/2026.

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   What a troop hand-over task records when the men have moved: one close for the garrison and the recruit tasks
   ///   (IGameStateBridge.SettleTroopHandOver), each saying in its own words what was brought.
   /// </summary>
   public sealed class TroopHandOver
   {
      /// <summary>How many men actually moved.</summary>
      public int Moved { get; set; }

      /// <summary>The task's evidence, without the day (the bridge adds it).</summary>
      public string Evidence { get; set; }

      /// <summary>What the giver remembers.</summary>
      public string Memory { get; set; }

      /// <summary>What the player reads when no reward line replaces it.</summary>
      public string PlayerLine { get; set; }
   }
}
