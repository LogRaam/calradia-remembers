// Code written by Gabriel Mailhot, 01/11/2026.
// RaveN017Mod (Nexus, 01/11/2026): Rhagaea offered 20,000 denars for a prisoner, nothing happened, and when told the money
// had not come she sent 1,000. Gabriel, same day: "Le probleme qu'il faut regler: l'incoherence entre ce que le LLM dit et ce
// que le joueur recoit, et eviter les abus de joueurs qui pourraient soutirer trop d'or au LLM par rapport a la valeur des
// choses." A sum handed over with nothing in return has no worth to be measured against, so it is bounded by what a person
// would give away: a share of their purse, by their regard ("part de la bourse selon le regard"). Counted over a window, or
// a player asking again and again would take the same share of what is left until the purse is empty.
//
// Cabinaw (Nexus, 06/11/2026): "NPCs aren't paying out the quests they have given. They give less than the agreed amount
// and it says 'This is all this NPC is willing to part with' even if they have the money for it." A reward for a task the
// game has VERIFIED done is owed, like an oath's debt: it is no gift and the ceiling never holds it back. A reward for a
// task not yet seen done stays a gift (or the player could talk the reward out of them first), and the notice says the
// agreed sum comes in full once the game records the task done.

#region

using System;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What a gift of coin is weighed on.</summary>
   public sealed class GiftFacts
   {
      /// <summary>What the giver's purse holds now.</summary>
      public int Purse { get; init; }

      /// <summary>The giver's personal regard for the player.</summary>
      public int Regard { get; init; }

      /// <summary>What they already gave the player inside the window, freely. Oath money does not count.</summary>
      public int GivenInWindow { get; init; }

      /// <summary>What they still owe the player by a sworn pay_gold oath: a debt is never held back as a gift.</summary>
      public int OwedByOath { get; init; }

      /// <summary>The gold still owed for their tasks the game has verified done (awaiting reward): owed, not given.</summary>
      public int OwedForTask { get; init; }
   }

   /// <summary>The most a character gives away freely. Pure.</summary>
   public static class GiftCeilingPolicy
   {
      /// <summary>TUNE: the days over which what a character gave away counts against their next gift.</summary>
      public const int WindowDays = 30;

      /// <summary>TUNE: a few coins anyone not hostile may spare, however thin the share of a thin purse.</summary>
      public const int SmallCoin = 100;

      /// <summary>TUNE: the share of the purse a character would give away at this regard. Nothing below wary.</summary>
      public static double ShareFor(int regard)
      {
         if (regard < Regard.WaryTier) return 0.0;
         if (regard < Regard.WarmTier) return 0.01;
         if (regard < Regard.ConfidingTier) return 0.03;
         if (regard < Regard.DevotedTier) return 0.05;
         if (regard < Regard.DeepBondTier) return 0.10;

         return 0.20;
      }

      /// <summary>
      ///   The most this character hands over now: their share of what they held before this window's gifts, less those
      ///   gifts, or what an oath still owes when that is more, and never beyond the purse.
      /// </summary>
      public static int Ceiling(GiftFacts f)
      {
         if (f == null) return 0;

         int purse = Math.Max(0, f.Purse);
         int given = Math.Max(0, f.GivenInWindow);
         double share = ShareFor(f.Regard);

         int free = 0;
         if (share > 0.0)
         {
            var whole = (int) ((purse + (long) given) * share);
            free = Math.Max(whole, SmallCoin) - given;
         }

         return Math.Min(purse, Math.Max(Math.Max(0, free), Owed(f)));
      }

      /// <summary>What this character owes the player, by oath and for verified tasks: never held back as a gift.</summary>
      public static int Owed(GiftFacts f)
         => f == null ? 0 : Math.Max(0, f.OwedByOath) + Math.Max(0, f.OwedForTask);

      /// <summary>The part of a sum handed over that pays the verified tasks' rewards (paid first, before any oath).</summary>
      public static int TowardTasks(GiftFacts f, int amount)
         => f == null ? 0 : Math.Min(Math.Max(0, amount), Math.Max(0, f.OwedForTask));

      /// <summary>What still counts of the last window's gifts on this day: nothing once the window has run out.</summary>
      public static int GivenStill(int? windowStartDay, int gifted, int today)
         => windowStartDay is int start && today >= start && today - start < WindowDays ? Math.Max(0, gifted) : 0;

      /// <summary>
      ///   Added to a gift's notice when less was given than was named, and why. Read by the player and by the model, so
      ///   neither is left with a sum that does not match (RaveN017Mod) and neither invents a reason.
      /// </summary>
      public static string ReducedNote(GiftOutcome o)
      {
         if (o == null || o.Asked <= o.Given) return "";

         return o.LimitedByPurse
            ? $" (not the {o.Asked} named: that is all {o.GiverName} holds)"
            : $" (not the {o.Asked} named: that is all {o.GiverName} will part with freely{AwaitsTheDeed(o.TaskRewardAwaitsTheDeed)})";
      }

      /// <summary>The refusal when a character will give nothing more away for now, whatever their purse holds.</summary>
      public static string WillNotGiveNote(string giverName, bool taskRewardAwaitsTheDeed = false)
         => $"({giverName} will part with no more coin freely for now, so none changed hands{AwaitsTheDeed(taskRewardAwaitsTheDeed)}.)";

      // Cabinaw: the player reads why the agreed reward did not come, and that it will, instead of a slight.
      private static string AwaitsTheDeed(bool awaits)
         => awaits ? "; the reward agreed for a task is paid in full once the task is recorded done" : "";
   }

   /// <summary>How a gift came out.</summary>
   public sealed class GiftOutcome
   {
      /// <summary>The sum named.</summary>
      public int Asked { get; init; }

      /// <summary>The sum that changed hands.</summary>
      public int Given { get; init; }

      /// <summary>The purse held it back, not the character's willingness.</summary>
      public bool LimitedByPurse { get; init; }

      /// <summary>The giver's name.</summary>
      public string GiverName { get; init; }

      /// <summary>The giver set the player a task with a gold reward the game has not yet seen done.</summary>
      public bool TaskRewardAwaitsTheDeed { get; init; }
   }
}
