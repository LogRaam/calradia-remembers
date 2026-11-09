// Code written by Gabriel Mailhot, 09/11/2026.
// One regard cost per deed. Some verbs charge their own cost in regard when they run: a demand for coin with a hold
// (extort) writes the attempt, its regard hit and a grudge whether the character pays or refuses
// (ExtortionBehavior.Settle). The reader, having read the deed, often ALSO writes a change_relation for the anger that
// goes with it. Honoured, that is the same cost twice.
//
// Found 09/11/2026 on the cost bench (Gabriel: "vas-y pour l'interprete, les refus et give_troops"): on a refused
// demand ("You will have nothing from me"), the interpreter read change_relation(-12) and no extort, so the game never
// recorded the attempt. Teaching it to read the refusal as extort makes extort AND change_relation in one turn likely;
// this rule keeps that to one cost. Same shape as TurnPaymentPolicy (one payment per deal).

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>The facts about one action of a turn, as far as a regard cost is concerned.</summary>
   public sealed class TurnRegardFacts
   {
      /// <summary>The action being weighed ("change_relation", ...).</summary>
      public string ActionType { get; init; }

      /// <summary>Its delta, null when it carries none or none that reads as a number.</summary>
      public int? Delta { get; init; }

      /// <summary>
      ///   The types of this turn's deeds that the game will really carry out. The host leaves out a deed the game is
      ///   about to refuse (an extort with no hold over the character), so the anger that goes with it still counts.
      /// </summary>
      public IReadOnlyList<string> DeedsTheGameWillCarryOut { get; init; }
   }

   /// <summary>Whether a change_relation is the same regard cost as a deed of the same turn.</summary>
   public static class TurnRegardPolicy
   {
      /// <summary>The verbs whose executor charges its own regard cost.</summary>
      private static readonly string[] DeedsChargingTheirOwnRegard = {"extort"};

      /// <summary>
      ///   True when the action is a negative change_relation in a turn whose deeds include one that charges its own
      ///   regard cost: the same cost written twice, dropped whichever came first. A warmth is never dropped here.
      /// </summary>
      public static bool IsSameCostAsADeedThisTurn(TurnRegardFacts facts)
      {
         if (facts == null || !string.Equals(facts.ActionType, "change_relation", StringComparison.OrdinalIgnoreCase)) return false;
         if (facts.Delta is not < 0 || facts.DeedsTheGameWillCarryOut == null) return false;

         return facts.DeedsTheGameWillCarryOut.Any(t => DeedsChargingTheirOwnRegard.Any(d => string.Equals(t, d, StringComparison.OrdinalIgnoreCase)));
      }
   }
}
