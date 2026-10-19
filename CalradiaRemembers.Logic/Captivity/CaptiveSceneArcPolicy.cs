// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>
   ///   Which named dramatic arc the lead captor's opening act follows this scene: NOT just varied
   ///   content, but a varied STRUCTURE, so the same captor does not run the identical pacing every
   ///   time (replayability).
   /// </summary>
   public enum SceneArc
   {
      /// <summary>Intro, RisingTension, Initiate, Intensify, Climax: the captor plays with their prey first.</summary>
      SlowBurn,

      /// <summary>Intro, Initiate, Intensify, Climax: today's baseline pacing.</summary>
      Classic,

      /// <summary>Intro, Initiate, Climax: no build-up, straight to it.</summary>
      Sudden
   }

   /// <summary>
   ///   Picks the lead captor's opening arc as a WEIGHTED draw over <see cref="SceneArc" />, mirroring
   ///   <see cref="CaptiveEncounterPolicy.SelectIntent" />'s draw style: context LEANS the odds rather
   ///   than gating them, and every arc keeps a floor weight so it can still be drawn regardless of
   ///   context. A faceless bandit has no patience for theatre and leans Sudden; a lord with
   ///   Domination or Training in mind savors the build-up and leans SlowBurn; a lord after
   ///   PersonalDesire is torn evenly between the two; every other lord register (Interrogation,
   ///   Reward, Torture, and the non-sexual bandit/reckoning intents that reuse this draw) leans the
   ///   familiar Classic middle ground.
   /// </summary>
   public static class CaptiveSceneArcPolicy
   {
      private const int ArcFloorWeight = 10; // every arc stays reachable, whatever the context

      private const int BanditSuddenBoost = 45;
      private const int BanditClassicBoost = 15;

      private const int LordDominationSlowBurnBoost = 40;
      private const int LordDominationClassicBoost = 10;

      private const int LordDesireSlowBurnBoost = 20;
      private const int LordDesireClassicBoost = 20;

      private const int LordDefaultClassicBoost = 35; // Interrogation / Reward / Torture / other
      private const int LordDefaultSlowBurnBoost = 5;

      /// <summary>
      ///   Draws the opening arc. <paramref name="rollPercent" /> is called once with the total weight
      ///   and is expected to return a 0..(total-1) draw (mirrors <see cref="CaptiveEncounterPolicy.SelectIntent" />'s
      ///   dice). The internal cumulative order is Classic, SlowBurn, Sudden so an out-of-contract roll
      ///   (at or above the total) safely falls back to Classic, the least surprising choice.
      /// </summary>
      public static SceneArc Select(bool captorIsBandit, CaptiveSceneIntent intent, Func<int, int> rollPercent)
      {
         int slowBurn = ArcFloorWeight;
         int classic = ArcFloorWeight;
         int sudden = ArcFloorWeight;

         if (captorIsBandit)
         {
            sudden += BanditSuddenBoost;
            classic += BanditClassicBoost;
         }
         else
         {
            switch (intent)
            {
               case CaptiveSceneIntent.Domination:
               case CaptiveSceneIntent.Training:
                  slowBurn += LordDominationSlowBurnBoost;
                  classic += LordDominationClassicBoost;

                  break;
               case CaptiveSceneIntent.PersonalDesire:
                  slowBurn += LordDesireSlowBurnBoost;
                  classic += LordDesireClassicBoost;

                  break;
               default: // Interrogation, Reward, Torture, Reckoning, and every non-sexual bandit intent
                  slowBurn += LordDefaultSlowBurnBoost;
                  classic += LordDefaultClassicBoost;

                  break;
            }
         }

         // Cumulative order: Classic, SlowBurn, Sudden (Classic is drawn first on a tie or an
         // out-of-range roll, so the familiar arc is the safe default of the three).
         int total = classic + slowBurn + sudden;
         int roll = rollPercent(total);

         if (roll < classic) return SceneArc.Classic;
         if (roll < classic + slowBurn) return SceneArc.SlowBurn;
         if (roll < total) return SceneArc.Sudden;

         return SceneArc.Classic; // out-of-contract roll: fall back to the familiar middle ground
      }
   }
}
