// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>
   ///   The "director" of a captive scene: a small state machine that owns the dramatic structure
   ///   so the model never has to (and never loops a beat). The lead captor opens with a named
   ///   <see cref="SceneArc" /> drawn by <see cref="CaptiveSceneArcPolicy" /> (SlowBurn / Classic /
   ///   Sudden) so the same captor does not run the identical pacing every time. After ANY act's
   ///   Climax the director decides the next act:
   ///   <list type="bullet">
   ///     <item>
   ///       a queued collective aggressor (another single member, or the whole remaining band at
   ///       once) takes their turn, OR
   ///     </item>
   ///     /model
   ///     <item>
   ///       with a decreasing chance the current aggressor runs ANOTHER act (a new position),
   ///       OR
   ///     </item>
   ///     <item>the scene moves to its Aftermath (a single come-down beat) and then concludes.</item>
   ///   </list>
   ///   A hard cap on total acts guarantees termination. Randomness is injected via
   ///   <see cref="IRandomSource" /> so every branch can be arranged deterministically in tests.
   /// </summary>
   public sealed class CaptiveSceneDirector
   {
      private const int MaxActs = 3; // lead + up to two more — bounds total length
      private readonly Queue<CaptiveAggressorKind> _pendingOthers = new();

      private readonly IRandomSource _rng;
      private CaptiveSceneStage[] _actStages;
      private int _completedActs;
      private int _stageIndex;

      public CaptiveSceneDirector(bool collective, int extraAggressors, IRandomSource rng, bool captorIsBandit, CaptiveSceneIntent intent)
      {
         _rng = rng ?? throw new ArgumentNullException(nameof(rng));

         // The lead's opening act (the only one that carries an Intro). Its shape is the named arc
         // the policy draws for this context; only the lead's OPENING act varies, subsequent
         // collective/repeat acts stay compressed exactly as before.
         SceneArc arc = CaptiveSceneArcPolicy.Select(captorIsBandit, intent, _rng.Next);
         _actStages = ArcStages(arc);
         CurrentAggressor = CaptiveAggressorKind.Lead;

         if (collective && extraAggressors >= 1)
            QueueOthers(extraAggressors);

         Sync();
      }

      /// <summary>Who acts this turn.</summary>
      public CaptiveAggressorKind CurrentAggressor { get; private set; } = CaptiveAggressorKind.Lead;

      /// <summary>The beat to perform this turn.</summary>
      public CaptiveSceneStage CurrentStage { get; private set; } = CaptiveSceneStage.Intro;

      /// <summary>True once the structure is exhausted and the scene should close.</summary>
      public bool IsConcluding => CurrentStage == CaptiveSceneStage.Conclude;

      /// <summary>True when the captor should react to a just-made player intervention this beat.</summary>
      public bool ReactToIntervention { get; private set; }

      /// <summary>
      ///   "Continue" — the player lets the story move on. Advance to the next beat of the structure.
      /// </summary>
      public void AdvanceStory()
      {
         ReactToIntervention = false;

         if (CurrentStage == CaptiveSceneStage.Conclude) return;

         // Aftermath is a single terminal beat, walked outside the act-stage machinery: the NEXT
         // Continue after it always lands on Conclude, the true terminal.
         if (CurrentStage == CaptiveSceneStage.Aftermath)
         {
            CurrentStage = CaptiveSceneStage.Conclude;
            CurrentAggressor = CaptiveAggressorKind.Lead;

            return;
         }

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

      #region private

      // The lead's opening act only (the sole act that carries an Intro, and, for SlowBurn, a
      // RisingTension build-up before the act begins). Subsequent acts always use the compressed
      // shapes in DecideNextAct, never one of these.
      private static CaptiveSceneStage[] ArcStages(SceneArc arc)
      {
         switch (arc)
         {
            case SceneArc.SlowBurn:
               return new[] {
                  CaptiveSceneStage.Intro, CaptiveSceneStage.RisingTension, CaptiveSceneStage.Initiate,
                  CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax
               };
            case SceneArc.Sudden:
               return new[] {CaptiveSceneStage.Intro, CaptiveSceneStage.Initiate, CaptiveSceneStage.Climax};
            default: // Classic
               return new[] {
                  CaptiveSceneStage.Intro, CaptiveSceneStage.Initiate, CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax
               };
         }
      }

      // Decreasing odds of "another round" so most scenes are 1–2 acts, rarely the capped 3.
      private static int ContinueChance(int completedActs)
         => completedActs == 1 ? 55
            : completedActs == 2 ? 35
            : 0;

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
               ? new[] {CaptiveSceneStage.Initiate, CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax}
               : new[] {CaptiveSceneStage.Initiate, CaptiveSceneStage.Climax};
            _stageIndex = 0;
            Sync();

            return;
         }

         // No queued others: roll whether the lead goes again (a NEW act/position) or stops here.
         if (_rng.Next(100) < ContinueChance(_completedActs))
         {
            CurrentAggressor = CaptiveAggressorKind.Lead;
            _actStages = new[] {CaptiveSceneStage.Initiate, CaptiveSceneStage.Intensify, CaptiveSceneStage.Climax};
            _stageIndex = 0;
            Sync();

            return;
         }

         SetConclude();
      }

      private void QueueOthers(int extraAggressors)
      {
         int others = Math.Min(extraAggressors, 2); // party-size already caps witnesses at 2

         // TOGETHER: the remaining men take her at once in one fuller sub-act (lead solo, then a
         // collective scene) — only with 2+ others. SEQUENTIAL: each takes a compressed turn, and
         // may decline ("or not").
         if (others >= 2 && _rng.Next(100) < 50)
         {
            _pendingOthers.Enqueue(CaptiveAggressorKind.GroupTogether);

            return;
         }

         for (var i = 0; i < others; i++)
            if (_rng.Next(100) < 70)
               _pendingOthers.Enqueue(CaptiveAggressorKind.AnotherSingle);
      }

      // Not the true terminal: a single Aftermath come-down beat runs first (walked directly by
      // AdvanceStory, outside the act-stage machinery), and only the FOLLOWING Continue reaches
      // Conclude. Idempotent: DecideNextAct never fires again once Aftermath is reached, since
      // AdvanceStory intercepts it before Advance() is called.
      private void SetConclude()
      {
         CurrentStage = CaptiveSceneStage.Aftermath;
         CurrentAggressor = CaptiveAggressorKind.Lead;
      }

      private void Sync() => CurrentStage = _actStages[_stageIndex];

      #endregion
   }
}