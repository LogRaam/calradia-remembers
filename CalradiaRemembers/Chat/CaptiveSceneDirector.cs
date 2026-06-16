// Code written by Gabriel Mailhot, 15/06/2026.
// Sprint 22: captive scenes are walked as an explicit structure of "acts" (an aggressor + a stage
//            sequence) so the LLM performs one named beat per turn and can never loop a beat. After
//            each Climax the director decides — deterministically for queued collective aggressors,
//            otherwise by a decreasing random roll — whether to run another act or conclude, so a
//            scene can naturally have "another round" without ever looping forever.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The "director" of a captive scene: a small state machine that owns the dramatic structure
   ///   so the model never has to (and never loops a beat). The lead captor always opens with the
   ///   full arc Intro → Initiate → Intensify → Climax. After ANY act's Climax the director decides
   ///   the next act:
   ///   <list type="bullet">
   ///     <item>a queued collective aggressor (another single member, or the whole remaining band at
   ///       once) takes their turn, OR</item>
   ///     <item>with a decreasing chance the current aggressor runs ANOTHER act (a new position),
   ///       OR</item>
   ///     <item>the scene concludes.</item>
   ///   </list>
   ///   A hard cap on total acts guarantees termination. The structure is generated on the fly, so a
   ///   future strong player intervention could redirect it (Gabriel's Q-option 3, not built).
   /// </summary>
   public sealed class CaptiveSceneDirector
   {
      private const int MaxActs = 3; // lead + up to two more — bounds total length

      private readonly Queue<CaptiveAggressorKind> _pendingOthers = new Queue<CaptiveAggressorKind>();
      private CaptiveSceneStage[] _actStages;
      private int  _stageIndex;
      private int  _completedActs;

      public CaptiveSceneDirector(bool collective, int extraAggressors)
      {
         // The lead's opening act — the only one that carries an Intro.
         _actStages       = new[] { CaptiveSceneStage.Intro, CaptiveSceneStage.Initiate, CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax };
         CurrentAggressor = CaptiveAggressorKind.Lead;

         if (collective && extraAggressors >= 1)
            QueueOthers(extraAggressors);

         Sync();
      }

      #region public surface

      /// <summary>The beat to perform this turn.</summary>
      public CaptiveSceneStage CurrentStage { get; private set; } = CaptiveSceneStage.Intro;

      /// <summary>Who acts this turn.</summary>
      public CaptiveAggressorKind CurrentAggressor { get; private set; } = CaptiveAggressorKind.Lead;

      /// <summary>True when the captor should react to a just-made player intervention this beat.</summary>
      public bool ReactToIntervention { get; private set; }

      /// <summary>True once the structure is exhausted and the scene should close.</summary>
      public bool IsConcluding => CurrentStage == CaptiveSceneStage.Conclude;

      /// <summary>
      ///   "Continue" — the player lets the story move on. Advance to the next beat of the structure.
      /// </summary>
      public void AdvanceStory()
      {
         ReactToIntervention = false;
         if (CurrentStage == CaptiveSceneStage.Conclude) return;
         Advance();
      }

      /// <summary>
      ///   "Try something else" — the player typed an action to reorient or deepen the roleplay at
      ///   THIS stage. The structure does NOT advance; the captor reacts to the intervention in place.
      ///   The player alone decides when the story moves on (via Continue), so this never auto-advances.
      /// </summary>
      public void DevelopInPlace()
      {
         if (CurrentStage == CaptiveSceneStage.Conclude) return;
         ReactToIntervention = true;
      }

      #endregion

      #region private

      private void QueueOthers(int extraAggressors)
      {
         int others = Math.Min(extraAggressors, 2); // party-size already caps witnesses at 2

         // TOGETHER: the remaining men take her at once in one fuller sub-act (lead solo, then a
         // collective scene) — only with 2+ others. SEQUENTIAL: each takes a compressed turn, and
         // may decline ("or not").
         if (others >= 2 && MBRandom.RandomInt(100) < 50)
         {
            _pendingOthers.Enqueue(CaptiveAggressorKind.GroupTogether);
            return;
         }

         for (int i = 0; i < others; i++)
            if (MBRandom.RandomInt(100) < 70)
               _pendingOthers.Enqueue(CaptiveAggressorKind.AnotherSingle);
      }

      private void Advance()
      {
         _stageIndex++;
         if (_stageIndex < _actStages.Length)
         {
            Sync();
            return;
         }

         DecideNextAct(); // the current act just finished (we passed its Climax)
      }

      private void DecideNextAct()
      {
         _completedActs++;

         if (_completedActs >= MaxActs)
         {
            SetConclude();
            return;
         }

         // A queued collective aggressor always gets their turn next.
         if (_pendingOthers.Count > 0)
         {
            CurrentAggressor = _pendingOthers.Dequeue();
            _actStages = CurrentAggressor == CaptiveAggressorKind.GroupTogether
               ? new[] { CaptiveSceneStage.Initiate, CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax }
               : new[] { CaptiveSceneStage.Initiate, CaptiveSceneStage.Climax };
            _stageIndex = 0;
            Sync();
            return;
         }

         // No queued others: roll whether the lead goes again (a NEW act/position) or stops here.
         if (MBRandom.RandomInt(100) < ContinueChance(_completedActs))
         {
            CurrentAggressor = CaptiveAggressorKind.Lead;
            _actStages = new[] { CaptiveSceneStage.Initiate, CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax };
            _stageIndex = 0;
            Sync();
            return;
         }

         SetConclude();
      }

      // Decreasing odds of "another round" so most scenes are 1–2 acts, rarely the capped 3.
      private static int ContinueChance(int completedActs)
         => completedActs == 1 ? 55
          : completedActs == 2 ? 35
          : 0;

      private void SetConclude()
      {
         CurrentStage     = CaptiveSceneStage.Conclude;
         CurrentAggressor = CaptiveAggressorKind.Lead;
      }

      private void Sync() => CurrentStage = _actStages[_stageIndex];

      #endregion
   }
}
