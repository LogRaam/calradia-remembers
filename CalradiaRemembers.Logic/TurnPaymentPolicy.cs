// Code written by Gabriel Mailhot, 27/09/2026.
// One payment per deal. Several verbs move their own coin (a captive bought, sold or exchanged, silence paid for,
// blackmail paid, a purse taken by force), and the model, having written the deal, often ALSO writes a give_gold or
// take_gold for the words that go with it ("I will pay it now"). Honoured, that is the same payment twice
// (tashmetu, Nexus, 27/09/2026: a ransom paid, then a second payment attempted from the purse it had just emptied,
// whose refusal the model read as a theft). join_party's hire payment had the same fault and its own drop since
// 2.4; this is the general rule for the rest. extort joined the list 09/11/2026: its executor pays the coin taken, and
// the reader was then taught to read the demand more often (DeedReadingTests).

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Whether a give_gold or take_gold is the same payment as a deal written in the same turn.</summary>
   public static class TurnPaymentPolicy
   {
      /// <summary>The verbs whose executor moves the coin itself.</summary>
      private static readonly string[] DealsMovingTheirOwnCoin =
         {"buy_prisoner", "sell_prisoner", "exchange_prisoners", "pay_for_silence", "pay_blackmail", "rob_npc", "sell_item", "counter_offer", "extort"};

      /// <summary>
      ///   True when <paramref name="actionType" /> is a give_gold or take_gold in a turn that also carries a deal
      ///   moving its own coin: the same payment written twice, dropped whichever came first.
      /// </summary>
      public static bool IsSamePaymentAsADealThisTurn(string actionType, IEnumerable<string> actionTypesThisTurn)
      {
         bool payment = string.Equals(actionType, "give_gold", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(actionType, "take_gold", StringComparison.OrdinalIgnoreCase);
         if (!payment || actionTypesThisTurn == null) return false;

         return actionTypesThisTurn.Any(t => DealsMovingTheirOwnCoin.Any(deal => string.Equals(t, deal, StringComparison.OrdinalIgnoreCase)));
      }
   }
}
