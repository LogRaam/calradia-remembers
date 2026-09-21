// Code written by Gabriel Mailhot, 21/06/2026.

using NpcMemoryService.Core.Standing;

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   When a plotter can recruit the PLAYER into their scheme, and what the player's help does to
   ///   it. The host gathers the regard and the alliance fact from the game; this holds the rule.
   /// </summary>
   public static class SchemeAgentPolicy
   {
      /// <summary>How far the player's help advances the scheme in one stroke.</summary>
      public const int AidProgressBoost = 30;

      /// <summary>
      ///   The plotter must trust the player at least this much to involve them in something illicit. RENAMED from
      ///   MinPlotterRegard on 21/09/2026: it is fed <c>plotter.GetRelation(player)</c>, the ENGINE's ledger, and
      ///   "regard" is the word this mod uses for its OWN (NpcProfile.ReputationWithPlayer). The two are different
      ///   numbers for the same pair of people, and a name that lies about which one is read is how lord recruitment
      ///   came to gate on the wrong ledger for months.
      /// </summary>
      public const int MinPlotterRelation = VanillaRelation.TrustedTier;

      /// <summary>
      ///   Applies the player's help to a scheme in place: it gets under way if freshly plotted, then
      ///   leaps forward by <see cref="AidProgressBoost" />, succeeding outright if that carries it to
      ///   fruition. A no-op on a terminal scheme. The host resolves the effect and rumour afterward.
      /// </summary>
      public static void ApplyPlayerAid(Scheme scheme)
      {
         if (scheme == null || SchemeAdvancer.IsTerminal(scheme.State)) return;

         if (scheme.State == SchemeState.Plotted) scheme.State = SchemeState.Progressing;

         scheme.Progress += AidProgressBoost;

         if (scheme.Progress < 100) return;
         scheme.Progress = 100;
         scheme.State = SchemeState.Succeeded;
      }

      /// <summary>
      ///   True when the plotter could ask the player to act as their agent: the scheme is still
      ///   SECRET and active (Plotted or Progressing — never once exposed or finished), the plotter
      ///   regards the player highly enough to trust them with it, and the player is not allied with
      ///   the target (one does not ask the fox to mind the henhouse).
      /// </summary>
      public static bool CanRecruitPlayer(SchemeState state, int plotterRelationWithPlayer, bool playerAlliedWithTarget)
      {
         if (state != SchemeState.Plotted && state != SchemeState.Progressing) return false;
         if (playerAlliedWithTarget) return false;

         return new VanillaRelation(plotterRelationWithPlayer).IsTrusted;
      }

      /// <summary>
      ///   True when the player could still warn the target: the scheme is secret (Plotted or
      ///   Progressing). Once exposed or finished, there is nothing left to warn of.
      /// </summary>
      public static bool CanWarnTarget(SchemeState state)
         => state == SchemeState.Plotted || state == SchemeState.Progressing;

      /// <summary>
      ///   The player warns the target, dragging a secret scheme into the light (→ Discovered).
      ///   It does not fail outright — the target now has a fighting chance, which the daily tick's
      ///   elevated backfire on an exposed plot plays out. A no-op once already exposed or terminal.
      /// </summary>
      public static void ExposeToTarget(Scheme scheme)
      {
         if (scheme == null) return;
         if (scheme.State == SchemeState.Plotted || scheme.State == SchemeState.Progressing)
            scheme.State = SchemeState.Discovered;
      }
   }
}