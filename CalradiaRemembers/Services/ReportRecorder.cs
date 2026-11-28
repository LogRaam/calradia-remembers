// Code written by Gabriel Mailhot, 14/11/2026.
// Keeps the latest exchanges with the model in memory, so a player's report (PlayerReportService) carries the prompt the
// character was given and the reply as it came back, even with file logging off. Gabriel (14/11/2026): "on voit tout le
// contexte, les logs, la conversation"; and, the same day, "le MODEL EXCHANGE est contextualisé pour la conversation en
// cours?": a report takes only the exchanges since its conversation opened (Since). Nothing is written anywhere until the
// player asks for a report. Lives in Services, beside the clients it is attached to (a Services -> Diagnostics edge was
// refused by the dependency ratchet). Gabriel (28/11/2026), the conversation log: "une option MCM qui permet de créer un log
// d'une conversation": while a chat with the log on is open, each call is also handed to its collector (Listener), slim (the
// model, the tokens, the cost, the finish), never the prompt; one observer serves both.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Diagnostics;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Services
{
   /// <summary>The latest exchanges with the model, kept in memory for a player report. Thread-safe.</summary>
   internal sealed class ReportRecorder : ILlmExchangeObserver
   {
      /// <summary>How many exchanges are kept: the last turns of a conversation with their interpreter calls.</summary>
      internal const int Kept = 24;

      private readonly LinkedList<(DateTime At, ReportExchange Exchange)> _exchanges = new();

      private ReportRecorder() { }

      /// <summary>The open chat's conversation log, or null (the MCM switch off, or no chat open). Set and cleared by the chat.</summary>
      internal volatile ChatLogCollector Listener;

      /// <summary>The one recorder, attached to every client the mod builds.</summary>
      internal static ReportRecorder Instance { get; } = new();

      /// <summary>Records one finished exchange; the oldest is dropped past <see cref="Kept" />. Never throws.</summary>
      public void OnExchange(LlmRequest request, LlmResponse response, string model)
      {
         if (request == null) return;

         DateTime now = DateTime.Now;
         var exchange = new ReportExchange {
            Time = now.ToString("HH:mm:ss"), Model = model, Subject = request.Subject, SystemPrompt = request.SystemPrompt,
            Messages = (request.Messages ?? new List<LlmMessage>()).Select(m => $"{m.Role.ToString().ToLowerInvariant()}: {m.Content}").ToList(),
            Reply = response?.Content, Error = response is {IsSuccess: false} ? response.ErrorMessage ?? "failed" : null
         };

         lock (_exchanges)
         {
            _exchanges.AddLast((now, exchange));
            while (_exchanges.Count > Kept) _exchanges.RemoveFirst();
         }

         ChatLogCollector log = Listener;
         log?.AddCall(new ChatLogCall {
            Model = model, PromptTokens = response?.Usage?.PromptTokens, CachedTokens = response?.Usage?.CachedPromptTokens,
            CompletionTokens = response?.Usage?.CompletionTokens, Cost = response?.Usage?.Cost, FinishReason = response?.FinishReason,
            WasRetried = response?.WasRetried == true, Error = exchange.Error
         }, request.Subject);
      }

      /// <summary>The kept exchanges that finished at or after <paramref name="since" />, oldest first, as a copy.</summary>
      internal IReadOnlyList<ReportExchange> Since(DateTime since)
      {
         lock (_exchanges) return _exchanges.Where(e => e.At >= since).Select(e => e.Exchange).ToList();
      }
   }
}
