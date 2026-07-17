// Code written by Gabriel Mailhot, 17/07/2026.
// DIVORCE: why a dissolution could not complete at its deadline, classified so the host reacts HONESTLY
// (romance audit M-D2/M-D6). MarriageDissolutionService.TryDissolve refuses on two very different grounds and
// the host used to treat every refusal the same way: a false "the household closed ranks" abort plus a relation
// penalty on the player. A pregnancy or captivity is TRANSIENT (it always clears: birth, release, or death), so
// the marriage simply is not dissoluble YET, and punishing the player for that is wrong. A role the leaving
// spouse holds (clan leader, party leader, governor) is STRUCTURAL: it never clears on its own, so a silent
// retry loops forever (and let a player freeze ANY divorce merely by handing their spouse a party). This pure
// classifier lets both hosts tell the two apart and is unit-tested rather than only observed in game.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a marriage dissolution was refused at its deadline, ranked by how the host must respond.</summary>
   public enum DivorceBlock
   {
      /// <summary>No block these facts can see: the refusal, if any, came from genuine spousal resistance.</summary>
      None,

      /// <summary>
      ///   A pregnancy or captivity is in play. Self-clearing (birth, release, or death), so the host must
      ///   DEFER and retry, never abort with a false "resisted" narrative or a relation penalty.
      /// </summary>
      Transient,

      /// <summary>
      ///   The leaving spouse commands a party, governs a fief, or heads their own clan. Never clears on its
      ///   own, so the host must RELEASE what it can before retrying, not loop in silence forever.
      /// </summary>
      Structural
   }

   /// <summary>
   ///   Classifies a dissolution refusal from the same facts <see cref="DivorcePolicy" />'s hosts observe.
   ///   Kept in lock-step with the guards in <c>MarriageDissolutionService.TryDissolve</c>: pregnancy and
   ///   captivity first (transient wins even when a structural role also applies, because the marriage cannot
   ///   dissolve at all while either holds), then the role-based structural blocks.
   /// </summary>
   public static class DivorceBlockPolicy
   {
      /// <summary>
      ///   TRANSIENT when either party is pregnant or captive (it will clear, so defer, never punish);
      ///   otherwise STRUCTURAL when the leaving spouse leads a clan, a party, or governs a settlement (it will
      ///   not clear, so release then retry); otherwise NONE.
      /// </summary>
      public static DivorceBlock Classify(bool eitherPregnant,
                                          bool eitherCaptive,
                                          bool leavingIsClanLeader,
                                          bool leavingLeadsParty,
                                          bool leavingIsGovernor)
      {
         if (eitherPregnant || eitherCaptive) return DivorceBlock.Transient;

         if (leavingIsClanLeader || leavingLeadsParty || leavingIsGovernor) return DivorceBlock.Structural;

         return DivorceBlock.None;
      }
   }
}
