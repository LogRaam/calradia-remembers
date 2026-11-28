// Code written by Gabriel Mailhot, 28/11/2026.
// What one open chat gathers for its conversation log (ChatLogRenderer). Gabriel (28/11/2026): "une option MCM qui permet de
// créer un log d'une conversation ... et qui explique le Why each turn went as it did". The chat feeds it as it goes: each line
// it records, each deed the game read, each fallback and recall, and the model recorder (ReportRecorder) each call. Lines are
// kept here, not read back from the chat's transcript at close, because a long chat folds its transcript into a summary and
// the recorder keeps only its latest exchanges. Later stages append their judge, referee, dramaturge and keeper entries
// through AddWhy. Thread-safe: model calls finish on worker threads.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
    /// <summary>Gathers one chat's scene, model calls and "why" entries, turn by turn. Thread-safe.</summary>
    public sealed class ChatLogCollector
    {
        private readonly List<ChatLogCall> _calls = new List<ChatLogCall>();
        private readonly List<ChatLogLine> _lines = new List<ChatLogLine>();
        private readonly object _lock = new object();
        private readonly List<ChatLogWhy> _why = new List<ChatLogWhy>();
        private int _turn;

        /// <summary>A collector for a chat whose roles are served by <paramref name="models" />.</summary>
        public ChatLogCollector(ChatLogModels models, DateTime opened)
        {
            Models = models ?? new ChatLogModels();
            Opened = opened;
        }

        /// <summary>The roles' models, as set when the chat opened.</summary>
        public ChatLogModels Models { get; }

        /// <summary>When the chat opened.</summary>
        public DateTime Opened { get; }

        /// <summary>The current turn (0 before the player's first line).</summary>
        public int Turn
        {
            get { lock (_lock) return _turn; }
        }

        /// <summary>The player sent a line: what follows belongs to the next turn.</summary>
        public void BeginTurn()
        {
            lock (_lock) _turn++;
        }

        /// <summary>Records a line of the scene in the current turn. A blank line is skipped.</summary>
        public void AddLine(ChatLogLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Text)) return;

            lock (_lock)
                _lines.Add(new ChatLogLine {
                   Turn = _turn, Speaker = line.Speaker, To = line.To, Text = line.Text, IsPlayer = line.IsPlayer, Kind = line.Kind,
                   Actions = line.Actions?.ToList() ?? new List<string>()
               });
        }

        /// <summary>
        ///   A deed the game read this turn, as it reported it: shown under the latest character line of the turn (the line it
        ///   came from) and in the why section. With no character line yet this turn, only the why entry is kept.
        /// </summary>
        public void AddAction(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            lock (_lock)
            {
                _why.Add(new ChatLogWhy {Turn = _turn, Kind = ChatLogWhyKind.Action, Text = text});
                int at = _lines.FindLastIndex(l => l.Turn == _turn && !l.IsPlayer && l.Kind == ChatLogLineKind.Speech);
                if (at < 0) return;

                ChatLogLine l = _lines[at];
                _lines[at] = new ChatLogLine {
                    Turn = l.Turn, Speaker = l.Speaker, To = l.To, Text = l.Text, IsPlayer = l.IsPlayer, Kind = l.Kind,
                    Actions = (l.Actions ?? new List<string>()).Concat(new[] {text}).ToList()
                };
            }
        }

        /// <summary>A "why" entry for the current turn (see <see cref="ChatLogWhyKind" />). A blank text is skipped.</summary>
        public void AddWhy(string kind, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            lock (_lock) _why.Add(new ChatLogWhy {Turn = _turn, Kind = string.IsNullOrWhiteSpace(kind) ? "note" : kind, Text = text});
        }

        /// <summary>A model call that finished while the chat was open; its turn and its role (<see cref="ChatLogRoles" />) are set here.</summary>
        public void AddCall(ChatLogCall call, string subject)
        {
            if (call == null) return;

            lock (_lock)
                _calls.Add(new ChatLogCall {
                   Turn = _turn, Role = ChatLogRoles.Of(Models, call.Model, subject), Model = call.Model, PromptTokens = call.PromptTokens,
                   CachedTokens = call.CachedTokens, CompletionTokens = call.CompletionTokens, Cost = call.Cost, FinishReason = call.FinishReason,
                   WasRetried = call.WasRetried, Error = call.Error
               });
        }

        /// <summary>The facts for the log, as gathered so far; the place, day, setting and close time come from the caller.</summary>
        public ChatLogFacts Snapshot(ChatLogFacts where)
        {
            lock (_lock)
                return new ChatLogFacts {
                    Character = where?.Character, Place = where?.Place, Day = where?.Day, Setting = where?.Setting, Models = Models, Opened = Opened,
                    Closed = where?.Closed ?? Opened, Lines = _lines.ToList(), Calls = _calls.ToList(), Why = _why.ToList()
                };
        }
    }
}
