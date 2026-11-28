// Code written by Gabriel Mailhot, 28/11/2026.
// The chat's side of the conversation log. Gabriel (28/11/2026): "ajoute une option MCM qui permet de créer un log d'une
// conversation sous la forme que tu faisais déjà (ex: cr_group_third_20261127_235024.md) et qui explique le Why each turn went
// as it did". With the MCM switch off there is no collector, and every feed below is a null check and nothing more. With it
// on, the chat feeds the collector (ChatLogCollector) as it goes, the model recorder (ReportRecorder, the one exchange observer)
// hands it each call, and the close writes one Markdown file in "CR Conversations", beside "CR Reports", off the main thread.

#region

using System;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Diagnostics;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private ChatLogCollector _chatLog;

      /// <summary>This chat's collector, or null when the log is off. cr.test conversation_log reads it.</summary>
      internal ChatLogCollector ConversationLog => _chatLog;

      /// <summary>
      ///   Starts the conversation log when the MCM switch is on: the roles' models as they stand now, and the model recorder
      ///   pointed at this chat. Called once as the window is built. Never throws.
      /// </summary>
      internal void StartConversationLog()
      {
         try
         {
            if (!ModSettings.WriteConversationLog || _chatLog != null) return;

            bool enhanced = LlmServices.ActiveComposition == ChatCompositionMode.Enhanced;
            LlmServices services = NpcMemoryCampaignBehavior.Instance?.LlmServices;
            _chatLog = new ChatLogCollector(new ChatLogModels {
               Mode = ModelModePolicy.DisplayName(LlmServices.ActiveModelMode), Prose = LlmServices.ActiveModelId,
               Interpreter = enhanced ? services?.ResolveInterpreterModel() : null, Nsfw = services?.ResolveNsfwModel()
            }, _openedAt);
            ReportRecorder.Instance.Listener = _chatLog;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONVERSATION LOG] could not start: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>A "why" entry for the current turn (see <see cref="ChatLogWhyKind" />); a no-op when the log is off.</summary>
      internal void NoteWhy(string kind, string text) => _chatLog?.AddWhy(kind, text);

      /// <summary>A deed the game read this turn, as it reported it; a no-op when the log is off.</summary>
      internal void NoteLoggedAction(string text) => _chatLog?.AddAction(text);

      /// <summary>The player sent a line: the log's next turn begins. cr.test conversation_log calls it as the send does.</summary>
      internal void NoteTurnBegins() => _chatLog?.BeginTurn();

      private void NoteLoggedLine(bool isPlayer, string name, string text, bool isNarration)
      {
         if (_chatLog == null) return;

         _chatLog.AddLine(new ChatLogLine {
            Speaker = isPlayer ? "You" : name, Text = text, IsPlayer = isPlayer, Kind = ChatLogLine.KindOf(isPlayer, isNarration, text)
         });
      }

      /// <summary>
      ///   Writes the log as the chat closes and stops collecting: off the main thread, or at once when
      ///   <paramref name="waitForFile" /> (cr.test). Returns the file's path when written at once, else null. Never throws.
      /// </summary>
      internal string WriteConversationLog(bool waitForFile = false)
      {
         ChatLogCollector log = _chatLog;
         if (log == null) return null;

         _chatLog = null;
         if (ReferenceEquals(ReportRecorder.Instance.Listener, log)) ReportRecorder.Instance.Listener = null;
         try
         {
            DateTime closed = DateTime.Now;
            string day = null;
            try { day = Campaign.Current != null ? CampaignTime.Now.ToString() : null; }
            catch { /* the day is a courtesy */ }

            string text = ChatLogRenderer.Render(log.Snapshot(new ChatLogFacts {Character = _npcName, Place = _storyLocation, Day = day, Closed = closed}));
            string fileName = ChatLogRenderer.FileName(_npcName, closed);
            if (waitForFile) return Diagnostics.ConversationLogWriter.Write(fileName, text);

            Task.Run(() => Diagnostics.ConversationLogWriter.Write(fileName, text));

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONVERSATION LOG] could not be written: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
