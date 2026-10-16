// Code written by Gabriel Mailhot, 15/10/2026.
// How a levy is paid face to face (Gabriel, 15/10/2026: "Si le joueur ne verse pas le montant d'argent pour soutenir la
// guerre pour remplacer son absence au front, alors il pourrait fournir des troupes, des chevaux, des armes"). Coin,
// horses, weapons and soldiers all count toward what is owed, at the game's own price, which the host reads. The
// hand-over is a levy, not a gift: the host skips the gift's gratitude, and these rules decide only what counts and how
// much. Soldiers are taken to the value still owed and never more than half the player's healthy men, so paying a levy
// cannot leave the vassal's own party unable to ride (Claude's default, accepted by Gabriel 15/10/2026).

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>What an item handed over is, as the levy reads it.</summary>
   public enum LevyGoodsKind
   {
      /// <summary>Anything else: a gift, never the levy.</summary>
      Other,

      /// <summary>A mount.</summary>
      Horse,

      /// <summary>A weapon (blades, polearms, bows, shields, ammunition).</summary>
      Weapon
   }

   /// <summary>One kind of soldier the player could hand over, the lowest tier first.</summary>
   public sealed class LevyTroopLot
   {
      /// <summary>How many of them are healthy.</summary>
      public int Healthy { get; init; }

      /// <summary>What one of them is worth: the game's recruitment cost.</summary>
      public int UnitValue { get; init; }
   }

   /// <summary>What the soldiers handed over toward a levy read.</summary>
   public sealed class LevyTroopFacts
   {
      /// <summary>The player's healthy regular soldiers, lowest tier first.</summary>
      public IReadOnlyList<LevyTroopLot> Lots { get; init; }

      /// <summary>What is still owed, in coin.</summary>
      public int Outstanding { get; init; }

      /// <summary>The room left in the liege's own party.</summary>
      public int Room { get; init; }
   }

   /// <summary>The soldiers taken toward a levy.</summary>
   public sealed class LevyTroopTake
   {
      /// <summary>How many are taken from each lot, in the order the lots were given.</summary>
      public IReadOnlyList<int> Counts { get; init; } = new int[0];

      /// <summary>How many men in all.</summary>
      public int Men { get; init; }

      /// <summary>What they are worth in all.</summary>
      public int Value { get; init; }
   }

   /// <summary>Pure: what a hand-over counts for against a levy.</summary>
   public static class WarLevyPayment
   {
      /// <summary>The share of the player's healthy soldiers a levy may take at most.</summary>
      public const double MaxTroopShare = 0.5;

      /// <summary>True when an item of this kind counts toward a levy (horses and weapons; Gabriel's "chevaux, armes").</summary>
      public static bool Counts(LevyGoodsKind kind) => kind == LevyGoodsKind.Horse || kind == LevyGoodsKind.Weapon;

      /// <summary>What goods worth <paramref name="value" /> count for: their worth, up to what is still owed.</summary>
      public static int GoodsCredit(int value, int outstanding) => Math.Max(0, Math.Min(value, outstanding));

      /// <summary>The soldiers taken toward the levy: the lowest tiers first, until the levy is met, the liege's party is full, or half the player's men are gone.</summary>
      public static LevyTroopTake Troops(LevyTroopFacts facts)
      {
         IReadOnlyList<LevyTroopLot> lots = facts?.Lots ?? new LevyTroopLot[0];
         var counts = new int[lots.Count];
         if (facts == null || facts.Outstanding <= 0 || facts.Room <= 0) return new LevyTroopTake {Counts = counts};

         var healthy = 0;
         foreach (LevyTroopLot lot in lots)
            healthy += Math.Max(0, lot?.Healthy ?? 0);

         int cap = Math.Min(facts.Room, (int) Math.Floor(healthy * MaxTroopShare));
         int men = 0, value = 0;

         for (var i = 0; i < lots.Count && men < cap && value < facts.Outstanding; i++)
         {
            LevyTroopLot lot = lots[i];
            if (lot == null || lot.Healthy <= 0 || lot.UnitValue <= 0) continue;

            while (counts[i] < lot.Healthy && men < cap && value < facts.Outstanding)
            {
               counts[i]++;
               men++;
               value += lot.UnitValue;
            }
         }

         return new LevyTroopTake {Counts = counts, Men = men, Value = value};
      }
   }
}
