// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, Phase 2a: the PLAYER themselves ends their OWN marriage, entirely through conversation (no UI
// button, owner ruling). This pure policy decides only whether the set-aside spouse would ALSO have wanted
// out (mutual consent), so the engine can feed DivorceFacts.MutualConsent into the SAME tiered consequence
// engine (DivorceConsequencePolicy) the NPC-NPC case and the Phase 2b spouse-demand case already share.

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>
   ///   Every already-resolved fact <see cref="PlayerDivorceConsentPolicy.SpouseConsents" /> needs. The
   ///   engine gathers these live from the player's own spouse at the moment end_own_marriage fires.
   /// </summary>
   public sealed class PlayerDivorceConsentFacts
   {
      /// <summary>
      ///   True when the spouse ALREADY has a pending Phase-2b divorce demand of their own
      ///   (<see cref="SpouseDivorceDemandPolicy" />/<c>SpouseDivorceDemandBehavior.IsDemanding</c>) — the
      ///   clearest possible signal that they too want the marriage to end.
      /// </summary>
      public bool SpouseHasPendingDemand { get; set; }

      /// <summary>The spouse's own personal relation toward the player.</summary>
      public int SpouseRelationWithPlayer { get; set; }
   }

   /// <summary>
   ///   Pure gate deciding whether the player's own spouse would ALSO have wanted this marriage to end
   ///   (mutual consent), when the PLAYER is the one who initiates the split. Feeds
   ///   <see cref="DivorceFacts.MutualConsent" />: true here means the existing tiered consequence engine
   ///   applies its SOFT verdicts (relief, no grudge storm); false means the full repudiated matrix (the
   ///   set-aside spouse's own reaction, their immediate family, and their three best friends all grudge
   ///   the player) applies instead.
   /// </summary>
   public static class PlayerDivorceConsentPolicy
   {
      /// <summary>
      ///   True when the set-aside spouse would also have wanted out: either they already have a pending
      ///   divorce demand of their own (Phase 2b), or their relation with the player already reads as
      ///   unhappy by the SAME bar the NPC-NPC case (Phase 1) uses to judge an unhappy spouse
      ///   (<see cref="DivorcePolicy.UnhappyThreshold" />) — reusing that one threshold rather than
      ///   inventing a second "unhappy" reading for what is fundamentally the same question, only asked
      ///   from the other side of the marriage.
      /// </summary>
      public static bool SpouseConsents(PlayerDivorceConsentFacts facts)
      {
         if (facts == null) return false;

         return facts.SpouseHasPendingDemand || facts.SpouseRelationWithPlayer <= DivorcePolicy.UnhappyThreshold;
      }
   }
}
