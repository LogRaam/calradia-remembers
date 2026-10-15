// Code written by Gabriel Mailhot, 15/10/2026.
// The levy a ruler asks of a vassal who did not ride to war (Gabriel, 15/10/2026: "le Lord pourrait alors demander
// compensation en taxes ... pour soutenir la guerre pour remplacer son absence au front"). It grows with what the
// vassal's house is: its rank and the fiefs the realm granted it. Capped at the largest single payment take_gold can
// carry, so a vassal can always settle it in one hand-over (the coupling BastardBlackmailPolicy states for its own
// demand). Goods and troops count toward it at the game's own price, which the host reads.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>What the levy reads.</summary>
   public sealed class WarLevyFacts
   {
      /// <summary>The player's clan tier (0..6).</summary>
      public int ClanTier { get; init; }

      /// <summary>How many fiefs (towns and castles) the player's clan holds.</summary>
      public int FiefCount { get; init; }

      /// <summary>The MCM "War levy scale" multiplier (1 = as designed).</summary>
      public double Scale { get; init; } = 1.0;
   }

   /// <summary>Pure: how much coin stands for a vassal's absence from a war.</summary>
   public static class WarLevyPolicy
   {
      /// <summary>Coin per clan tier (counted from tier 0 as one). Tuning.</summary>
      public const int PerTier = 1000;

      /// <summary>Coin per fief held: the realm granted it, the realm's war is its charge. Tuning.</summary>
      public const int PerFief = 1500;

      /// <summary>The least a levy asks. Tuning.</summary>
      public const int MinLevy = 1000;

      /// <summary>The most a levy asks: one take_gold transfer, so it can always be settled in one hand-over.</summary>
      public const int MaxLevy = GoldTransferLimit.MaxSingleTransfer;

      /// <summary>The levy, in coin.</summary>
      public static int Amount(WarLevyFacts facts)
      {
         if (facts == null) return MinLevy;

         int tier = Math.Max(0, Math.Min(6, facts.ClanTier));
         int fiefs = Math.Max(0, facts.FiefCount);
         double scale = facts.Scale > 0 && !double.IsNaN(facts.Scale) && !double.IsInfinity(facts.Scale) ? facts.Scale : 1.0;

         double raw = (PerTier * (tier + 1) + PerFief * fiefs) * scale;
         int rounded = (int) Math.Round(raw / 100.0) * 100;

         return Math.Max(MinLevy, Math.Min(MaxLevy, rounded));
      }
   }
}
