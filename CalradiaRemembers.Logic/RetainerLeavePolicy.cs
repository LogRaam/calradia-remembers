// Code written by Gabriel Mailhot, 11/08/2026.
// Retainer feasibility spike (a lord/lady of ANOTHER clan riding IN the player's own party for a time, WITHOUT
// marriage and WITHOUT joining the player's clan). The author's rule is that such a retainer must break off the
// moment their own faction goes to war with the player: an enemy lord cannot keep travelling inside the very
// army they now field against. This pure gate is the single, test-covered decision behind both the WarDeclared
// hook and the daily safety check in RetainerSpikeBehavior, so the two paths can never quietly diverge. It
// reasons over plain facts only (the mod's CampaignBehaviorBase reads the engine and hands them in), mirroring
// the split EscortContinuationPolicy already draws for the follow_me escort's own end conditions.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Pure end-condition gate for an ACTIVE retainer arrangement (a foreign lord riding in the player's party).</summary>
   public static class RetainerLeavePolicy
   {
      /// <summary>
      ///   True when a currently-tracked retainer must leave the player's party and be handed back to their own
      ///   clan. Two facts force it: the author's flagship rule, that the retainer's faction is now at war with
      ///   the player (an enemy lord does not ride inside the army they may next fight), and death, so a retainer
      ///   who has died is always dropped from tracking rather than lingering as a stale id. When nothing is
      ///   tracked there is nothing to end, so the answer is a plain, idempotent false.
      /// </summary>
      /// <param name="retainerFactionAtWarWithPlayer">Their map faction is at war with the player's map faction.</param>
      /// <param name="retainerAlive">The retainer hero is still alive.</param>
      /// <param name="stillTracked">The mod is still tracking this hero as an active retainer.</param>
      public static bool MustLeave(bool retainerFactionAtWarWithPlayer, bool retainerAlive, bool stillTracked)
      {
         if (!stillTracked) return false;
         if (!retainerAlive) return true;

         return retainerFactionAtWarWithPlayer;
      }
   }
}
