// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 6 of the group-scene plan, the per-turn machinery (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: three modes, "le mode classique avec un seul modele+NSFW, le mode Enhanced avec deux modeles
// PROSE+INTERPRET+NSFW, et le mode dramaturge avec DRAMA+PROSE+INTERPRET+NSFW". The inner life and the keeper run only in the
// Dramaturge mode: in the simulator a same-batch baseline with none of this equalled the full system on the drama, so it is
// NOT A/B-validated, and Standard and Enhanced must not change at all.
//
// When it runs: the MCM says Dramaturge AND the in-chat switch holds that position (Roleplay and NSFW are one-call modes),
// on an ordinary 1:1 turn of the main character answering a line the player really wrote. A synthetic line (an opener, a
// reaction, a captor's cue), a captive or captor scene, a voice of the ensemble, a council: none of it runs there.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>The facts the Dramaturge machinery is decided on, for one turn.</summary>
   public sealed class DramaturgeTurnFacts
   {
      /// <summary>The MCM "Chat Composition" says Dramaturge.</summary>
      public bool DramaturgeMode { get; init; }

      /// <summary>The in-chat switch holds the MCM mode's position (Enhanced/Dramaturge), not Roleplay or NSFW.</summary>
      public bool SwitchOnTheMcmMode { get; init; }

      /// <summary>The turn's speaker is the main character of the conversation.</summary>
      public bool IsMainSpeaker { get; init; }

      /// <summary>The "player line" is a stage direction the mod wrote (an opener, a reaction, a captor's cue).</summary>
      public bool IsSyntheticPlayerLine { get; init; }

      /// <summary>The player is held captive in this scene.</summary>
      public bool IsCaptiveEncounter { get; init; }

      /// <summary>The player holds the character captive in this scene.</summary>
      public bool IsCaptorScene { get; init; }

      /// <summary>The turn is one voice of an ensemble turn, or the council's.</summary>
      public bool IsGroupVoice { get; init; }
   }

   /// <summary>Whether the Dramaturge machinery runs on a turn, and how long its cheap calls may take.</summary>
   public static class DramaturgeModePolicy
   {
      /// <summary>
      ///   How long one inner-life update or one keeper reading may take before the turn carries on without it. The cheap
      ///   calls ride the interpreter client (a flash model, under 2 s in the simulator); the composed opening's check waits 30 s
      ///   because nothing waits on it, but the keeper stands between the player and the reply, so it is cut much sooner.
      /// </summary>
      public const int CheapCallTimeoutSeconds = 12;

      /// <summary>
      ///   How long the next turn waits for the inner life still being updated from the last line before it is written
      ///   without the newest state (the player usually takes longer than this to type).
      /// </summary>
      public const int InnerLifeWaitSeconds = 4;

      /// <summary>
      ///   How long a dramaturge reading that came while the turn was still finishing (the interpreter holds the input after the
      ///   reply) waits for the input to open before it is set aside (Gabriel's chat with Rancid Thylea, 28/11/2026).
      /// </summary>
      public const int ReadingHoldSeconds = 60;

      /// <summary>
      ///   True only in the Dramaturge mode, with the switch on it, on an ordinary main-character turn answering a real
      ///   player line. Everywhere else (Standard, Enhanced, Roleplay, NSFW, groups, captives) nothing extra runs.
      /// </summary>
      public static bool Runs(DramaturgeTurnFacts facts)
      {
         if (facts == null) return false;

         return facts.DramaturgeMode && facts.SwitchOnTheMcmMode && facts.IsMainSpeaker && !facts.IsSyntheticPlayerLine
                && !facts.IsCaptiveEncounter && !facts.IsCaptorScene && !facts.IsGroupVoice;
      }

      /// <summary>The cheap calls' time limit.</summary>
      public static TimeSpan CheapCallLimit => TimeSpan.FromSeconds(CheapCallTimeoutSeconds);
   }
}
