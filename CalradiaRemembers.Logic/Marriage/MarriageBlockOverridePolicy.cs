// Code written by Gabriel Mailhot, 11/08/2026.
// COURTSHIP TRIAL, the safety rail. When a courtship trial is served the match must OPEN, but a passed trial may
// only ever clear the ONE soft, courtship-progression gate the player hit (RegardTooLow, "MARRIAGE NOT YET AN
// OFFER"): it must NEVER wave away a hard blocker the game itself forbids marrying across (an active war, a
// captive on either side, the player already wed, the same clan, or a pairing unlawful by age/sex/existing
// marriage). EncounterContextBuilder's ordered resolver already returns every hard reason BEFORE it reaches the
// trial gate, so the trial is beyond their reach by construction; this pure table states that soft-vs-hard split
// EXPLICITLY, in one tested place, so the decision can never silently widen as reasons are added or reordered.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>Pure soft-vs-hard table: which <see cref="MarriageBlockReason" /> a passed courtship trial may override.</summary>
   public static class MarriageBlockOverridePolicy
   {
      /// <summary>
      ///   True when a HARD marriage blocker stands that no courtship trial may ever move: the game itself
      ///   forbids marrying across it. <see cref="MarriageBlockReason.None" /> (not a block at all) and
      ///   <see cref="MarriageBlockReason.RegardTooLow" /> (the single SOFT courtship-progression gate) are the
      ///   only two that are not hard. Every other reason is hard, and an unknown/future reason is treated as
      ///   hard so a new value can never be waved away by accident until it is deliberately classified here.
      /// </summary>
      public static bool IsHardBlock(MarriageBlockReason reason)
      {
         switch (reason)
         {
            case MarriageBlockReason.None:         return false; // nothing bars the match
            case MarriageBlockReason.RegardTooLow: return false; // the one SOFT gate the trial exists to clear
            case MarriageBlockReason.PlayerAlreadyWed:
            case MarriageBlockReason.SameClan:
            case MarriageBlockReason.NotMarriageable:
            case MarriageBlockReason.Captive:
            case MarriageBlockReason.AtWar:
            case MarriageBlockReason.LeadsTheirOwnHouse: // the game seals no such match, a trial or not
               return true;
            default:
               return true; // unknown/future reason: fail safe as HARD, never silently overridable
         }
      }

      /// <summary>
      ///   True when a served courtship trial may override <paramref name="reason" /> and open the match. A
      ///   trial that has NOT been passed overrides nothing. A passed trial overrides ONLY the soft
      ///   <see cref="MarriageBlockReason.RegardTooLow" /> gate; a hard blocker keeps the offer closed no matter
      ///   how long the prospect rode along.
      /// </summary>
      public static bool CanOverride(MarriageBlockReason reason, bool trialPassed)
      {
         if (!trialPassed) return false;

         return reason == MarriageBlockReason.RegardTooLow;
      }
   }
}
