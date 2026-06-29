// Code written by Gabriel Mailhot, 28/06/2026.
// "Stance with teeth", increment 3.2c — the rescue branch. While the player is convoyed away as a captive, their
// free clan companions may ride to intercept the captor band and cut them loose. The chance scales with how many
// companions can actually ride: a lone wanderer is unlikely to mount a rescue, a full retinue very likely. Pure
// and tunable; the host owns finding the companions, spawning the rescue party, and the battle that frees the player.

namespace CalradiaRemembers.Logic
{
   /// <summary>Whether (and how likely) the player's companions mount a rescue of their captive lord.</summary>
   public static class CompanionRescuePolicy
   {
      /// <summary>No matter how large the retinue, a rescue is never a certainty in a given day. Tuning.</summary>
      public const int MaxChancePercent = 60;

      /// <summary>Each free companion who can ride adds this to the daily rescue chance. Tuning.</summary>
      public const int PerCompanionPercent = 20;

      /// <summary>The daily chance the player's companions attempt a rescue, given how many can ride (capped).</summary>
      public static int RescueChancePercent(int freeCompanions)
         => freeCompanions <= 0
            ? 0
            : System.Math.Min(MaxChancePercent, freeCompanions * PerCompanionPercent);

      /// <summary>True when a 0..99 roll lands inside the rescue chance for this many free companions.</summary>
      public static bool ShouldAttempt(int freeCompanions, int rollPercent)
         => rollPercent >= 0 && rollPercent < RescueChancePercent(freeCompanions);
   }
}
