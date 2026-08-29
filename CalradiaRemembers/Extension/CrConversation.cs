// Code written by Gabriel Mailhot, 24/07/2026.
// Extension Surface: the PUBLIC conversation-lifecycle events. A third-party mod subscribes to learn, per
// conversation, which NPC (a stable hero StringId), which save/campaign is loaded, the in-game day, and, when
// it ends, the whole attributed transcript (including third-party witness lines, which the addon author called
// the tricky part of his own proxy). Subscribers only LISTEN; only CR raises these, and every subscriber is
// invoked under a guard so one that throws can never break the conversation or the game.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Extension;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The public conversation-lifecycle events other mods subscribe to. CR is the only raiser.</summary>
   public static class CrConversation
   {
      // Hands out the per-session SessionId's line ordering (LineIndex). Pure logic; see
      // CalradiaRemembers.Logic.Extension.ConversationLineSequencer for the WHY (Heroes Voices grouping/ordering).
      private static readonly ConversationLineSequencer _sequencer = new ConversationLineSequencer();

      /// <summary>
      ///   Fired when a conversation OPENS, once the NPC is known and before the first exchange, so a listener
      ///   can inject relevant memories (via <see cref="CrNpcMemory.AddMemory" />) in time for the first prompt.
      /// </summary>
      public static event Action<CrConversationInfo> Started;

      /// <summary>
      ///   Fired when a conversation CLOSES, carrying the whole attributed transcript (every speaker's lines and
      ///   narration, oldest first). This is where a listener records what was said.
      /// </summary>
      public static event Action<CrConversationLog> Ended;

      /// <summary>
      ///   Fired in REAL TIME each time a line is displayed in a conversation (an NPC line, a witness line, scene
      ///   narration, or the player's own), carrying the clean words and enough speaker detail to pick a voice.
      ///   This is the surface a text-to-speech / subtitle / lip-sync addon reads. Player-authored lines and
      ///   out-of-character notes arrive with <see cref="CrSpeechLine.IsPlayer" /> true, so a voice-over can skip
      ///   them; use <see cref="Ended" /> to stop or flush playback when the conversation closes.
      /// </summary>
      public static event Action<CrSpeechLine> LineSpoken;

      // Raised by CR (internal). A caller only subscribes; it never raises.
      internal static void RaiseStarted(string npcId, int gameDay)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return;

         string sessionId = Guid.NewGuid().ToString("N");
         _sequencer.StartSession(sessionId);

         Invoke(Started, new CrConversationInfo(npcId, CurrentSaveId(), gameDay, sessionId));
      }

      internal static void RaiseEnded(string npcId, int gameDay, IReadOnlyList<CrTranscriptLine> lines)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return;

         Invoke(Ended, new CrConversationLog(npcId, CurrentSaveId(), gameDay, lines ?? new List<CrTranscriptLine>(), _sequencer.SessionId));
      }

      internal static void RaiseLineSpoken(CrSpeechLine line)
      {
         if (line == null || string.IsNullOrWhiteSpace(line.Text)) return;

         line.SessionId = _sequencer.SessionId;
         line.LineIndex = _sequencer.NextIndex();

         Invoke(LineSpoken, line);
      }

      /// <summary>The loaded campaign's stable unique id, or null when no campaign is loaded.</summary>
      internal static string CurrentSaveId() => Campaign.Current?.UniqueGameId;

      // Invokes each subscriber under its own guard: a third-party handler that throws must not take down the
      // others or the conversation. Mirrors the "never let an outsider break the game" stance of the Feed door.
      private static void Invoke<T>(Action<T> evt, T arg)
      {
         if (evt == null) return;

         foreach (Delegate handler in evt.GetInvocationList())
            try { ((Action<T>) handler)(arg); }
            catch (Exception ex)
            {
               ModLog.Error($"[CONV EVENT] a subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }
   }
}
