// Code written by Gabriel Mailhot, 24/07/2026.
// Extension Surface: the PUBLIC conversation-lifecycle events. A third-party mod subscribes to learn, per
// conversation, which NPC (a stable hero StringId), which save/campaign is loaded, the in-game day, and, when
// it ends, the whole attributed transcript (including third-party witness lines, which the addon author called
// the tricky part of his own proxy). Subscribers only LISTEN; only CR raises these, and every subscriber is
// invoked under a guard so one that throws can never break the conversation or the game.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
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

      /// <summary>
      ///   Makes the NPC in the CURRENTLY OPEN conversation voice a reaction to an outcome that resolved OUTSIDE a
      ///   player message, in its own words, without the player having to announce it. Use it when a decision the
      ///   player made through your own UI (a popup, a menu) should draw an in-character response, or when a deed
      ///   you executed resolved after the NPC had already spoken. Give the raw fact ("the player chose to spare the
      ///   prisoner"); CR frames it in character. This is one extra LLM turn.
      ///   <para>
      ///     Returns false, doing nothing, when no conversation is open, the conversation has ended, or a turn is
      ///     already in flight (you may retry when the player next has the floor). Call on the main (UI) thread, like
      ///     every other conversation interaction. For a deed dispatched through <see cref="CrActions" />, prefer
      ///     setting <c>ActionResult.Recap</c> instead, which fires this same follow-up automatically in the same beat.
      ///   </para>
      /// </summary>
      public static bool PromptReply(string outcomeHint)
      {
         try
         {
            if (string.IsNullOrWhiteSpace(outcomeHint)) return false;

            Chat.ChatViewModel vm = Chat.ChatManager.ActiveViewModel;

            return vm != null && vm.TryPromptReply(outcomeHint);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV] PromptReply threw and was isolated: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   The id of the conversation open NOW, the same <see cref="CrSpeechLine.SessionId" /> every line of it carries, or
      ///   null when no conversation is open. Hand it back to <see cref="ShowPicture" /> so a picture finished after the
      ///   player left is dropped rather than shown in the next conversation.
      /// </summary>
      public static string OpenSessionId
         => Chat.ChatManager.IsOpen && Chat.ChatManager.ActiveViewModel != null ? _sequencer.SessionId : null;

      /// <summary>
      ///   Shows a picture your mod made (an illustration of a line, a scene) in the open conversation's picture panel,
      ///   the one CR's tales use: fitted without distortion, under <paramref name="caption" /> (one line, at most 120
      ///   characters). <paramref name="sessionId" /> is the conversation it was made for (<see cref="OpenSessionId" />, or
      ///   the <see cref="CrSpeechLine.SessionId" /> of the line you illustrated): if that conversation has closed or
      ///   another is open, nothing is shown. A .png, .jpg or .jpeg file that exists; one picture at a time, the latest
      ///   replacing the one before. Write each picture under a NEW file name: CR keeps a loaded image by its path for the
      ///   session.
      ///   <para>
      ///     Returns false, showing nothing, when the picture is refused (the reason is logged as [EXT PICTURE]); true when
      ///     it is accepted, and it is then drawn on the game thread at the next frame, so you may call this from any
      ///     thread, including the continuation of your own generation task. Never throws. Since 2.8.13 (CrApi.Version 3).
      ///   </para>
      /// </summary>
      public static bool ShowPicture(string sessionId, string imagePath, string caption)
      {
         try
         {
            string why = PictureRefusal(sessionId, imagePath);
            if (why != null)
            {
               ModLog.Info($"[EXT PICTURE] refused ({why}): {System.IO.Path.GetFileName(imagePath ?? "")}");

               return false;
            }

            string title = ExternalPicturePolicy.Caption(caption);
            MainThreadDispatcher.Enqueue(() =>
            {
               // The conversation may have closed between the call and this frame.
               if (PictureRefusal(sessionId, imagePath) != null) return;
               Chat.ChatManager.ActiveViewModel?.ShowSceneImage(imagePath, title);
               ModLog.Info($"[EXT PICTURE] shown: {System.IO.Path.GetFileName(imagePath)}");
            });

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[EXT PICTURE] ShowPicture threw and was isolated: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      private static string PictureRefusal(string sessionId, string imagePath)
      {
         string open = OpenSessionId;

         return ExternalPicturePolicy.WhyRefused(new ExternalPictureFacts {
            ConversationOpen = open != null,
            SessionMatches = open != null && string.Equals(open, sessionId, StringComparison.Ordinal),
            FileExists = !string.IsNullOrWhiteSpace(imagePath) && System.IO.File.Exists(imagePath),
            Extension = string.IsNullOrWhiteSpace(imagePath) ? "" : System.IO.Path.GetExtension(imagePath)
         });
      }

      // Raised by CR (internal). A caller only subscribes; it never raises.
      internal static void RaiseStarted(string npcId, int gameDay)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return;

         // Memories written on the map until now are announced before this conversation's begin (MemoryWritten).
         MemoryPictures.LookAtEveryone(null);
         string sessionId = Guid.NewGuid().ToString("N");
         _sequencer.StartSession(sessionId);

         Invoke(Started, new CrConversationInfo(npcId, CurrentSaveId(), gameDay, sessionId));
      }

      internal static void RaiseEnded(string npcId, int gameDay, IReadOnlyList<CrTranscriptLine> lines)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return;

         Invoke(Ended, new CrConversationLog(npcId, CurrentSaveId(), gameDay, lines ?? new List<CrTranscriptLine>(), _sequencer.SessionId));
         // The memories this conversation wrote carry its id (Bhelogan, 20/11/2026: "a link back to the source talk").
         MemoryPictures.LookAtEveryone(_sequencer.SessionId);
         // A bond the conversation moved is announced now, not at the next hour.
         CrRomance.CheckForChanges();
      }

      internal static void RaiseLineSpoken(CrSpeechLine line)
      {
         if (line == null || string.IsNullOrWhiteSpace(line.Text)) return;

         line.SessionId = _sequencer.SessionId;
         line.LineIndex = _sequencer.NextIndex();

         Invoke(LineSpoken, line);
      }

      /// <summary>The id of the conversation open now (or the last one), for CR's own per-conversation state.</summary>
      internal static string CurrentSessionId => _sequencer.SessionId;

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
