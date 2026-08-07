// Code written by Gabriel Mailhot, 07/08/2026.
// Living World pillar, Increment 1: when a lord-vs-lord scheme resolves, it becomes a VISIBLE feud by
// worsening the REAL vanilla relation between plotter and target (and cooling the target's close allies
// toward the plotter). That relation drop is the only diplomatic lever: vanilla's
// DefaultDiplomacyModel.GetScoreOfDeclaringWar reads a relationScore term, so a feud that holds lets the
// kingdom AI escalate to war ON ITS OWN. This policy decides the deltas and the ally fan-out; the mod applies
// them via ChangeRelationAction. No war is ever decreed here (or anywhere in this feature).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   The relation deltas a resolved scheme turns into: how hard the plotter/target bond sours, how much
   ///   the target's close allies cool toward the plotter, and how many allies feel it. A plain result carrier
   ///   from <see cref="FeudRelationPolicy" />; the host reads it and applies the changes.
   /// </summary>
   public sealed class FeudRelationOutcome
   {
      /// <summary>An outcome that changes nothing, for a scheme end that is not a visible feud (a plot that fizzled unseen).</summary>
      public static readonly FeudRelationOutcome None = new();

      /// <summary>How many of the target's closest allies cool toward the plotter (a bounded fan-out, not the whole realm).</summary>
      public int AllyFanOutCap { get; set; }

      /// <summary>The smaller relation delta applied to each cooled ally toward the plotter (negative, or 0 for None).</summary>
      public int AllyCoolingDelta { get; set; }

      /// <summary>True when this resolution is a real, visible feud that should move relations.</summary>
      public bool CreatesFeud { get; set; }

      /// <summary>The relation delta applied between plotter and target (negative, or 0 for None).</summary>
      public int PlotterTargetDelta { get; set; }
   }

   /// <summary>
   ///   Turns a resolved scheme (its kind and how it ended) into the relation deltas that make the feud
   ///   VISIBLE and let vanilla diplomacy escalate on its own. The magnitudes are the tension knob: too small
   ///   and repeated feuds never build toward the war threshold vanilla already checks; too large and one plot
   ///   detonates a war outright, which is not this feature's call to make.
   /// </summary>
   public static class FeudRelationPolicy
   {
      /// <summary>Per cooled ally: at least this much, so a fan-out is never a no-op even for a small feud.</summary>
      public const int MinAllyCooling = 2;

      /// <summary>Ally cooling is this fraction of the plotter/target rupture: third parties adjust, but far less than the two principals.</summary>
      public const int AllyCoolingDivisor = 4;

      /// <summary>A caught-but-not-landed plot: the enmity is real, but roughly half a success, since the harm never completed.</summary>
      public const int DiscoveredBasePenalty = 7;

      /// <summary>How many of the target's closest allies cool when a plot is merely caught (a tighter circle than a landed one).</summary>
      public const int DiscoveredFanOutCap = 3;

      /// <summary>Slander is a personal, public affront to the name, so it sours the bond itself a little harder than sabotage.</summary>
      public const int SlanderRelationBonus = 2;

      /// <summary>A landed plot that actually harmed the target: the deepest single-feud rupture this policy produces.</summary>
      public const int SucceededBasePenalty = 12;

      /// <summary>How many of the target's closest allies cool when a plot lands (a wider circle learns of a completed harm).</summary>
      public const int SucceededFanOutCap = 5;

      /// <summary>
      ///   Decides the feud deltas for a scheme that has reached <paramref name="outcome" />. Only a landed
      ///   plot (<see cref="SchemeState.Succeeded" />) or a caught one (<see cref="SchemeState.Discovered" />)
      ///   is a visible feud; any other end (a plot that fizzled unseen, or a still-active state) returns
      ///   <see cref="FeudRelationOutcome.None" /> and moves nothing.
      /// </summary>
      public static FeudRelationOutcome Resolve(SchemeKind kind, SchemeState outcome)
      {
         int basePenalty;
         int fanOut;

         switch (outcome)
         {
            case SchemeState.Succeeded:
               basePenalty = SucceededBasePenalty;
               fanOut = SucceededFanOutCap;

               break;
            case SchemeState.Discovered:
               basePenalty = DiscoveredBasePenalty;
               fanOut = DiscoveredFanOutCap;

               break;
            default: return FeudRelationOutcome.None;
         }

         int magnitude = basePenalty + (kind == SchemeKind.Slander ? SlanderRelationBonus : 0);
         int allyCooling = Math.Max(MinAllyCooling, magnitude / AllyCoolingDivisor);

         return new FeudRelationOutcome {
            CreatesFeud = true,
            PlotterTargetDelta = -magnitude,
            AllyCoolingDelta = -allyCooling,
            AllyFanOutCap = fanOut
         };
      }
   }
}
