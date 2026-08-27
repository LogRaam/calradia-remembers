// Code written by Gabriel Mailhot, 27/08/2026.
// Spotlight pillar, pure: when someone in the scene orders an active participant to hold their tongue ("you will
// speak when I command it, and not before"), does the target OBEY (dropping back to a witness) or DEFY it? A
// silence order is a status move (Johnstone's Impro), so it turns on the STANDING GAP first and the target's
// temper second. Standing is deterministic and dominant, exactly as Gabriel called it: a liege is obeyed by his
// vassal, and an influential lord who tells a lesser one to be quiet is obeyed whatever the lesser one's temper,
// while a nobody cannot silence a great lord however meekly they ask. Only inside a narrow band of roughly equal
// standing does personality decide: an agreeable or calculating soul yields, a bold or proud one holds the floor.
// The mod folds rank and clan influence into StandingGap and reads the liege bond; this only weighs them.

#region

using CalradiaRemembers.Logic.Grudges;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>Whether an ordered participant yields the floor or holds it.</summary>
   public enum SilenceResponse
   {
      /// <summary>They obey and drop back to a witness.</summary>
      Comply,

      /// <summary>They refuse and stay a full participant.</summary>
      Defy
   }

   /// <summary>Everything <see cref="SilenceOrderPolicy.Resolve" /> weighs to decide whether a silence order lands.</summary>
   public sealed class SilenceOrderContext
   {
      /// <summary>The temper of the one being told to be silent (Grudges' shared trait shape, -2..+2).</summary>
      public TraitFacts TargetTraits { get; set; }

      /// <summary>
      ///   The commander's standing MINUS the target's, as the mod computes it from rank and clan influence
      ///   (positive = the commander outweighs the target). Standing is the dominant, deterministic factor.
      /// </summary>
      public int StandingGap { get; set; }

      /// <summary>The commander is the target's liege or ruler: a vassal obeys the order outright, whatever their temper.</summary>
      public bool CommanderIsLiege { get; set; }
   }

   /// <summary>
   ///   Resolves a silence order. Standing decides it at the extremes (a liege, or a wide influence/rank gap, is
   ///   deterministic either way); only within a narrow band of near-equal standing does the target's temper
   ///   tip it. Every constant is a tuning knob.
   /// </summary>
   public static class SilenceOrderPolicy
   {
      /// <summary>At or above this standing gap the target obeys whatever their temper: an influential lord silences a lesser one.</summary>
      public const int HardComplyGap = 3;

      /// <summary>At or below this standing gap the order is laughed off whatever the temper: a nobody cannot silence a great lord.</summary>
      public const int HardDefyGap = -3;

      /// <summary>The scored band's bar: a lean at or above this yields the floor. Set so two equals of neutral temper do NOT fall silent on command.</summary>
      public const int ComplyThreshold = 1;

      /// <summary>Standing still weighs heavily inside the band, not only at the extremes.</summary>
      public const int GapWeight = 4;

      /// <summary>An agreeable, deferential heart yields.</summary>
      public const int MercyWeight = 3;

      /// <summary>A calculating one yields by choice, picking its battles.</summary>
      public const int CalculatingWeight = 2;

      /// <summary>A bold one defies being told to be quiet.</summary>
      public const int ValorWeight = 3;

      /// <summary>A proud one defies on principle, their dignity at stake.</summary>
      public const int HonorWeight = 2;

      /// <summary>Decides whether the silence order lands. A null context defaults to <see cref="SilenceResponse.Defy" /> (keep the voice, never silence on nothing).</summary>
      public static SilenceResponse Resolve(SilenceOrderContext ctx)
      {
         if (ctx == null) return SilenceResponse.Defy;

         // Deterministic standing: a liege is obeyed; a wide gap decides either way before temper is even read.
         if (ctx.CommanderIsLiege) return SilenceResponse.Comply;
         if (ctx.StandingGap >= HardComplyGap) return SilenceResponse.Comply;
         if (ctx.StandingGap <= HardDefyGap) return SilenceResponse.Defy;

         // Near-equal standing: standing still leads, but temper tips it.
         TraitFacts t = ctx.TargetTraits ?? new TraitFacts();
         int complyLean = ctx.StandingGap * GapWeight
                        + t.Mercy * MercyWeight
                        + t.Calculating * CalculatingWeight
                        - t.Valor * ValorWeight
                        - t.Honor * HonorWeight;

         return complyLean >= ComplyThreshold ? SilenceResponse.Comply : SilenceResponse.Defy;
      }
   }
}
