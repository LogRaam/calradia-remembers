// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The rivals' traits a scheme's odds rest on — engine-gathered, pure to reason over. Each 0..100.</summary>
   public sealed class SchemeFactors
   {
      /// <summary>How badly the plotter wants the target harmed (from their bitter relation): adds momentum.</summary>
      public int Animosity { get; set; }

      /// <summary>The plotter's daring: drives how fast the scheme gains ground each tick.</summary>
      public int PlotterBoldness { get; set; }

      /// <summary>The plotter's guile: lowers both the chance of discovery and of a backfire.</summary>
      public int PlotterCunning { get; set; }

      /// <summary>The target's watchfulness: raises the chance they catch wind of the plot.</summary>
      public int TargetVigilance { get; set; }
   }

   /// <summary>
   ///   Advances a scheme by one daily tick. A single roll decides this tick's fate, partitioned so
   ///   the outcome is deterministic for a given roll (testable): the lowest band is a backfire
   ///   (Failed), the next is discovery (Progressing → Discovered, secret only), and the rest is
   ///   quiet progress toward success. Terminal schemes never change.
   /// </summary>
   public static class SchemeAdvancer
   {
      private const int BaseMomentum = 8;
      private const int DiscoveredBackfirePenalty = 25; // an exposed plot is far likelier to collapse
      private const int MaxBackfireBase = 15;
      private const int MaxDiscoveryChance = 60;
      private const int MinBackfireChance = 2;

      /// <summary>
      ///   Advances <paramref name="scheme" /> one tick in place. Plotted schemes simply get under way;
      ///   active schemes roll once for backfire → discovery → progress, succeeding when progress
      ///   reaches 100. Terminal schemes are left untouched.
      /// </summary>
      public static void Advance(Scheme scheme, SchemeFactors factors, IRandomSource random)
      {
         if (scheme == null || random == null) return;
         if (IsTerminal(scheme.State)) return;

         // A freshly plotted scheme spends its first tick simply getting under way.
         if (scheme.State == SchemeState.Plotted)
         {
            scheme.State = SchemeState.Progressing;

            return;
         }

         int roll = random.Next(100);

         int backfire = BackfireChance(scheme.State, factors);
         if (roll < backfire)
         {
            scheme.State = SchemeState.Failed;

            return;
         }

         // Only a still-secret plot can be discovered; an exposed one just races on.
         if (scheme.State == SchemeState.Progressing && roll < backfire + DiscoveryChance(factors))
         {
            scheme.State = SchemeState.Discovered;

            return;
         }

         scheme.Progress += Momentum(factors);

         if (scheme.Progress < 100) return;
         scheme.Progress = 100;
         scheme.State = SchemeState.Succeeded;
      }

      /// <summary>Per-tick chance (%) the scheme collapses: a clumsy plotter risks it, and an exposed plot far more.</summary>
      public static int BackfireChance(SchemeState state, SchemeFactors f)
      {
         int cunning = f?.PlotterCunning ?? 0;
         int baseChance = Clamp(MaxBackfireBase - cunning / 10, MinBackfireChance, MaxBackfireBase);

         return state == SchemeState.Discovered
            ? baseChance + DiscoveredBackfirePenalty
            : baseChance;
      }

      /// <summary>Per-tick chance (%) a still-secret scheme is caught: vigilance raises it, cunning lowers it.</summary>
      public static int DiscoveryChance(SchemeFactors f)
      {
         if (f == null) return 0;

         return Clamp(f.TargetVigilance / 4 - f.PlotterCunning / 10, 0, MaxDiscoveryChance);
      }

      /// <summary>Whether the scheme has reached a final state and should no longer be ticked.</summary>
      public static bool IsTerminal(SchemeState state)
         => state == SchemeState.Succeeded || state == SchemeState.Failed;

      /// <summary>Progress a confident, hateful plot makes each tick (the rest of the lifecycle is the roll).</summary>
      public static int Momentum(SchemeFactors f)
         => f == null
            ? BaseMomentum
            : BaseMomentum + f.PlotterBoldness / 10 + f.Animosity / 10;

      #region private

      private static int Clamp(int value, int min, int max)
         => Math.Max(min, Math.Min(max, value));

      #endregion
   }
}