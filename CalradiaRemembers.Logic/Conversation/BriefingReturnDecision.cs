// Code written by Gabriel Mailhot, 13/09/2026.
//
// WHY THIS EXISTS, and it is worth writing down because it cost three attempts.
//
// The briefing-to-meeting return went wrong three times in one day, and not one of the three was a mistake
// about what a step DOES. They were all mistakes about the shape of the sequence:
//
//   1. A step that was never called (ConversationCast.Introduce, written and never wired).
//   2. A precondition INVERTED - the return waited for Mission.Current to become null, copied from
//      DuelCoordinator where that is right for the opposite reason (a duel needs its own arena, so the
//      current mission must end first; a briefing return wants the scene the player never left).
//   3. A step that should not have existed at all - staging a scene, which opened an interior mission from
//      the campaign map and crashed the game natively.
//
// Every one of those is a property of the COMPOSITION, not of any step's contents. Gabriel's reading
// (13/09/2026): a Command-style plan makes the composition into data, and data is testable without a game,
// without a save, and in milliseconds. He is right, and faults 2 and 3 are exactly the kind a pure test
// catches instantly.
//
// WHAT IT DELIBERATELY DOES NOT DO. No pattern can tell you that CreateAndOpenMissionController crashes when
// called from the map. The plan becomes provable; the EFFECTS stay unprovable outside the running game, which
// is why this mod's self-tests drive real objects. So the split here is the same one the rest of the codebase
// uses: a pure policy DECIDES, the host ACTS. The decision is what was wrong all three times.
//
// It is also why this is a decision and not an interpreter over ChatManager.Close: that method's finally
// guarantees FreeResources, and a close that fails to run it once killed every conversation in the session
// until the game was restarted. Indirection does not belong there. It belongs here, where it can be read.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>What the pending briefing return should do on this tick.</summary>
   public enum ReturnMove
   {
      /// <summary>Nothing is pending; this is not a briefing close.</summary>
      Idle,

      /// <summary>Keep waiting: the briefing window has not finished closing.</summary>
      Wait,

      /// <summary>Open the meeting over the live host the player never left.</summary>
      OpenOverLiveHost,

      /// <summary>
      ///   The return cannot happen. Give back what the hand-off deferred - the map scene, the clock, the
      ///   encounter - rather than leave the player on a frozen screen.
      /// </summary>
      GiveTheWorldBack
   }

   /// <summary>
   ///   The whole decision the briefing return makes, as a pure function of four facts. Every fault of
   ///   13/09/2026 is expressible here, and therefore assertable in a unit test.
   /// </summary>
   public static class BriefingReturnDecision
   {
      /// <summary>
      ///   How many ticks the return waits for the briefing window before giving up. Generous: the window is
      ///   killed within the same Close() call that arms the return, so one or two ticks is the real case, and
      ///   this only bounds a situation nobody has seen.
      /// </summary>
      public const int PatienceTicks = 600;

      /// <summary>
      ///   IS THERE STILL A MEETING TO GO BACK TO? Bannerlord hosts a conversation in one of two ways and the
      ///   briefing return only ever asked about one of them, which is the whole of the 13/09/2026 fault:
      ///   <list type="bullet">
      ///     <item>a MISSION - the player walked up to somebody inside a loaded scene;</item>
      ///     <item>a MAP CONVERSATION - a layer over the map screen, with no Mission behind it at all.</item>
      ///   </list>
      ///   Either one is a host the chat opens cleanly over. Neither one is a scene to be BUILT: if this reads
      ///   false, the answer is to give the world back, never to stage anything.
      /// </summary>
      public static bool HostIsLive(bool missionLive, bool mapConversationLive) =>
         missionLive || mapConversationLive;

      /// <summary>
      ///   THE DECISION. <paramref name="armed" /> is whether a briefing close asked for a return;
      ///   <paramref name="chatIsOpen" /> whether the briefing window is still up;
      ///   <paramref name="hostIsLive" /> whether the meeting still has a host to open over;
      ///   <paramref name="ticksWaited" /> how long we have been waiting.
      ///   <para>
      ///     NOTE THE ASYMMETRY THAT WAS WRONG BEFORE: a live host is the REASON to act, never the reason to
      ///     wait. The return wants the meeting the player never left. Staging one is not a move this decision
      ///     can even express, which is the point - the crash of 13/09/2026 is now unrepresentable.
      ///   </para>
      ///   <para>
      ///     WHAT "HOST" MEANS, and getting this wrong cost a fourth attempt on 13/09/2026. The parameter used
      ///     to be called sceneIsLive and the caller filled it with Mission.Current != null. That is only ONE of
      ///     the two ways Bannerlord hosts a conversation. Gabriel's meeting with Ira was the other: a MAP
      ///     conversation, a layer over the map screen with no Mission behind it at all (his log: "Chat overlay
      ///     backdrop ... SceneLayer, MapMenuOverlay, MapConversation" on NavalMapScreen). So the briefing read
      ///     "no scene" about a meeting that was standing perfectly well, gave the world back, and ended it.
      ///     The caller now asks the question this parameter's NAME always claimed to ask.
      ///   </para>
      /// </summary>
      public static ReturnMove Decide(bool armed, bool chatIsOpen, bool hostIsLive, int ticksWaited)
      {
         if (!armed) return ReturnMove.Idle;
         if (ticksWaited > PatienceTicks) return ReturnMove.GiveTheWorldBack;
         if (chatIsOpen) return ReturnMove.Wait;

         return hostIsLive ? ReturnMove.OpenOverLiveHost : ReturnMove.GiveTheWorldBack;
      }
   }

   /// <summary>One step of a chat close, in the order it runs.</summary>
   public enum ChatCloseStep
   {
      /// <summary>Everything that RECORDS: memories, the story log, resolutions, the transcript event.</summary>
      RecordWhatHappened,

      /// <summary>Free the character tableaux (GPU render targets) before the movie goes.</summary>
      ReleasePortraits,

      /// <summary>Take the window down.</summary>
      KillWindow,

      /// <summary>Turn the host map scene's rendering back on.</summary>
      ResumeMapScene,

      /// <summary>End the vanilla conversation this chat was riding.</summary>
      EndVanillaConversation,

      /// <summary>End a mission we staged whose conversation never started.</summary>
      EndStagedMission,

      /// <summary>Finish the map encounter, when there is one and nothing else is holding it.</summary>
      EndMapEncounter,

      /// <summary>Let the clock run again.</summary>
      RestoreTime
   }

   /// <summary>Why a chat is closing, which is what decides how much of the world it gives back.</summary>
   public enum ChatCloseKind
   {
      /// <summary>The conversation is over: give everything back and return the player to the map.</summary>
      Ordinary,

      /// <summary>
      ///   A briefing closing into the meeting it was nested inside. The window goes and another opens; nothing
      ///   about the world should move in between, because the very next open would undo all of it.
      /// </summary>
      HandOff
   }

   /// <summary>
   ///   Which steps a close runs, in order. The list IS the behaviour, so a unit test can read it.
   /// </summary>
   public static class ChatClosePlan
   {
      /// <summary>The steps for this kind of close, in the order they must run.</summary>
      public static IReadOnlyList<ChatCloseStep> For(ChatCloseKind kind)
      {
         var plan = new List<ChatCloseStep> {
            // Both kinds record and both take the window down. The private word is remembered whichever
            // close this is - a hand-off defers the WORLD, never the memory.
            ChatCloseStep.RecordWhatHappened,
            ChatCloseStep.ReleasePortraits,
            ChatCloseStep.KillWindow
         };

         if (kind == ChatCloseKind.HandOff) return plan;

         // Everything below is the world being given back, and every one of these is something the next open
         // would immediately undo. The churn between them is where two days of loading screens and one native
         // crash lived.
         plan.Add(ChatCloseStep.ResumeMapScene);
         plan.Add(ChatCloseStep.EndVanillaConversation);
         plan.Add(ChatCloseStep.EndStagedMission);
         plan.Add(ChatCloseStep.EndMapEncounter);
         plan.Add(ChatCloseStep.RestoreTime);

         return plan;
      }

      /// <summary>
      ///   The steps a hand-off deferred, for whoever has to settle the debt when the return does not happen.
      ///   Derived from the two plans rather than listed again, so it can never drift from them.
      /// </summary>
      public static IReadOnlyList<ChatCloseStep> Deferred()
      {
         var handedOff = new HashSet<ChatCloseStep>(For(ChatCloseKind.HandOff));
         var rest = new List<ChatCloseStep>();

         foreach (ChatCloseStep step in For(ChatCloseKind.Ordinary))
            if (!handedOff.Contains(step))
               rest.Add(step);

         return rest;
      }
   }
}
