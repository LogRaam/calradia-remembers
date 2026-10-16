// Code written by Gabriel Mailhot, 15/10/2026.
// Where the conversation's hand-overs (take_gold, give_item, give_troops) and the encounter context meet the vassal war
// duty's levy (Gabriel, 15/10/2026: coin, else troops, horses or arms), and the audience channels learn which ruler calls
// the player in. The war duty supplies these at startup
// (NpcMemoryServiceSubmodule), so the bridge, the verbs and the context builder ask one door and never know the duty
// itself, the way LetterIntentionResolver.WarDutyOpenFor is supplied. Unset (or nothing owed), every answer is "none".

#region

using System;
using CalradiaRemembers.Logic.WarDuty;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>The one door to the war levy for the conversation's hand-overs and context.</summary>
   internal static class WarLevyDesk
   {
      /// <summary>What the player still owes this liege, in coin; 0 when nothing.</summary>
      internal static Func<Hero, int> OwedTo;

      /// <summary>Counts a hand-over worth this much toward this liege's levy; returns what is still owed after.</summary>
      internal static Func<Hero, int, int> Pay;

      /// <summary>The soldiers this liege would take toward the levy right now (nothing moves).</summary>
      internal static Func<Hero, LevyTroopTake> PlanTroops;

      /// <summary>Moves the soldiers into this liege's party and counts them toward the levy; returns what moved.</summary>
      internal static Func<Hero, LevyTroopTake> TakeTroops;

      /// <summary>The terms this liege holds the player to, worded for the prompt; null when nothing is owed.</summary>
      internal static Func<Hero, string> TermsFor;

      /// <summary>The ruler who calls the player in over the war duty right now (WarDutyLadder.CallsThePlayerIn); null when none.</summary>
      internal static Func<Hero> CallingRuler;

      /// <summary>The ruler calling the player in, safely; null when none.</summary>
      internal static Hero Summoner()
      {
         try { return CallingRuler?.Invoke(); }
         catch { return null; }
      }

      /// <summary>What the player owes this liege, safely.</summary>
      internal static int Owed(Hero liege)
      {
         try { return liege == null ? 0 : OwedTo?.Invoke(liege) ?? 0; }
         catch { return 0; }
      }

      /// <summary>The one line an outcome adds after a hand-over toward the levy.</summary>
      internal static string AfterPayment(int stillOwed)
         => stillOwed <= 0 ? " The levy is paid in full." : $" {stillOwed} denars of the levy are still owed.";
   }
}
