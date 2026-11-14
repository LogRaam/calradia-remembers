// Code written by Gabriel Mailhot, 14/11/2026.
// Keeps the latest exchanges with the model in memory, so a player's report (PlayerReportService) carries the prompt the
// character was given and the reply as it came back, even with file logging off. Gabriel (14/11/2026): "on voit tout le
// contexte, les logs, la conversation". Nothing is written anywhere until the player asks for a report.

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
      internal const int Kept = 12;

      private readonly LinkedList<ReportExchange> _exchanges = new();

      private ReportRecorder() { }

      /// <summary>The one recorder, attached to every client the mod builds.</summary>
      internal static ReportRecorder Instance { get; } = new();

      /// <summary>Records one finished exchange; the oldest is dropped past <see cref="Kept" />. Never throws.</summary>
      public void OnExchange(LlmRequest request, LlmResponse response, string model)
      {
         if (request == null) return;

         var exchange = new ReportExchange {
            Time = DateTime.Now.ToString("HH:mm:ss"), Model = model, Subject = request.Subject, SystemPrompt = request.SystemPrompt,
            Messages = (request.Messages ?? new List<LlmMessage>()).Select(m => $"{m.Role.ToString().ToLowerInvariant()}: {m.Content}").ToList(),
            Reply = response?.Content, Error = response is {IsSuccess: false} ? response.ErrorMessage ?? "failed" : null
         };

         lock (_exchanges)
         {
            _exchanges.AddLast(exchange);
            while (_exchanges.Count > Kept) _exchanges.RemoveFirst();
         }
      }

      /// <summary>The kept exchanges, oldest first, as a copy.</summary>
      internal IReadOnlyList<ReportExchange> Latest()
      {
         lock (_exchanges) return _exchanges.ToList();
      }
   }
}
