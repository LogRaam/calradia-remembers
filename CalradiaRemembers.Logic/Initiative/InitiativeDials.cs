// Code written by Gabriel Mailhot, 29/10/2026.
// The older pace dials (Lords Approaching You, Companions Asking For A Word; Letters from NPCs until 31/10/2026, when the
// letter box made letters wait unopened and stop asking for a turn) under the world's
// initiative. Before it, each set its own rate (a daily chance, a mailbox budget, a cooldown across the companions); with
// the director on, those rates still applied FIRST and the director paced what was left, so the two multiplied and the
// world came far less often than either said (found 29/10/2026). Gabriel (28/10/2026) wanted one conductor and the old
// dials read once into its weights. While the director paces, an old dial is now a weight on its family (Never stops it,
// Rarely halves it, Often adds half) and its own rate steps aside; the rules against repetition (a lord's own cooldown,
// a correspondent's, the mailbox's variety) stay. With the director Off, every dial works exactly as before.

namespace CalradiaRemembers.Logic.Initiative
{
   /// <summary>Reads the older pace dials as family weights, and steps their own rates aside while the director paces. Pure.</summary>
   public static class InitiativeDials
   {
      /// <summary>How much room a family gets for Lords Approaching You: Never none, Rarely half, Often half again.</summary>
      public static int WeightPercent(InterceptionPacing pacing) => pacing switch {
         InterceptionPacing.Never => 0,
         InterceptionPacing.Rarely => 50,
         InterceptionPacing.Often => 150,
         _ => 100
      };

      /// <summary>The same for Companions Asking For A Word.</summary>
      public static int WeightPercent(Companions.CompanionAudiencePacing pacing) => pacing switch {
         Companions.CompanionAudiencePacing.Never => 0,
         Companions.CompanionAudiencePacing.Rarely => 50,
         Companions.CompanionAudiencePacing.Often => 150,
         _ => 100
      };

      /// <summary>A family's weight from the director's own slider and the old dial: both are the player's word.</summary>
      public static int Combined(int familyWeightPercent, int dialPercent)
         => familyWeightPercent <= 0 || dialPercent <= 0 ? 0 : familyWeightPercent * dialPercent / 100;

      /// <summary>
      ///   A daily chance (a lord riding out, a word asked on the road): its own while the director is off; while it paces,
      ///   only on or off, so every day there is a reason the request is put to the director.
      /// </summary>
      public static int ChancePercent(int ownChancePercent, bool directorPaces)
         => !directorPaces ? ownChancePercent : ownChancePercent > 0 ? 100 : 0;

      /// <summary>A cooldown that limits a whole kind (all companions at once): its own while the director is off, none while it paces.</summary>
      public static int KindCooldownDays(int ownDays, bool directorPaces) => directorPaces ? 0 : ownDays;
   }
}
