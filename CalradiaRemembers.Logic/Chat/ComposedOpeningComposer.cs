// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 3 of the group-scene plan, the composed intro (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "au début de chaque CHAT on ajoute l'intro"; the composer "chooses among true facts and stages them; it
// invents nothing the game tracks". The two calls of a composed opening: the composer (the prose model, in place of the
// opener's own call) and the check (the interpreter model, cheap). In the simulator one composed event invented a raid and
// the check caught it; here the check reads the paragraph and the concern, and anything short of a plain "ok" falls back.
//
// The clients are parameters: the chat passes the live ones, cr.test composed_intro and the unit tests stand-ins with fixed
// replies. Never throws and never touches the game: the chat reads the facts on the main thread before, and applies the
// result on the main thread after.
//
// Stage 7 (28/11/2026, the Dramaturge mode only): the composer may also propose up to three events the world could bring later
// in the scene, from the same facts; they go to their own check (the simulator's: "N: ok" or "N: invents"), beside the
// opening's, and only those it plainly passes are kept for the dramaturge. Gabriel's rule stands: it chooses among true facts.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>How a composed opening ended.</summary>
   public sealed class ComposedOpeningResult
   {
      /// <summary>True when the paragraph passed the check and may be shown.</summary>
      public bool Delivered { get; init; }

      /// <summary>The composer's reply, read (null when no reply came).</summary>
      public ComposedOpeningReply Reply { get; init; }

      /// <summary>One line for the log and the conversation log: what was shown, or why the opening fell back.</summary>
      public string Why { get; init; } = "";

      /// <summary>True when the wait was cut by the caller (the player spoke first, the window closed).</summary>
      public bool Cancelled { get; init; }
   }

   /// <summary>Runs the composer and the check over one facts sheet. Never throws.</summary>
   public static class ComposedOpeningComposer
   {
      /// <summary>
      ///   Composes the opening from <paramref name="sheet" /> on <paramref name="composer" /> and checks it on
      ///   <paramref name="checker" />. Each call has its own time limit; <paramref name="ct" /> cuts both.
      /// </summary>
      public static async Task<ComposedOpeningResult> ComposeAsync(ComposedOpeningSheet sheet, ILlmClient composer, ILlmClient checker,
                                                                   TimeSpan composerLimit, TimeSpan checkLimit, CancellationToken ct)
      {
         if (sheet == null || composer == null || checker == null) return Fell("nothing to compose with");

         string facts = ComposedOpeningPrompt.FactsText(sheet);
         ComposedOpeningReply reply;
         try
         {
            LlmResponse r;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(composerLimit);
               r = await composer.CompleteAsync(new LlmRequest {
                  SystemPrompt = ComposedOpeningPrompt.ComposerSystem(sheet),
                  Messages = new[] {new LlmMessage(MessageRole.User, facts)},
                  // Reasoning off, as every short reading of the scene engine (GLM 4.6 spent the dramaturge's budget thinking, 28/11/2026).
                  Parameters = new LlmParameters {MaxTokens = 800, Creativity = 0.7f, AllowTruncationRetry = false, ReasoningOverride = "off"},
                  Subject = "composed opening"
               }, cts.Token).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested) return new ComposedOpeningResult {Cancelled = true, Why = "set aside before it came"};
            if (r == null || !r.IsSuccess) return Fell($"the composer failed ({r?.ErrorMessage ?? "no reply"})");

            reply = ComposedOpeningPrompt.Parse(r.Content);
         }
         catch (OperationCanceledException)
         {
            return ct.IsCancellationRequested ? new ComposedOpeningResult {Cancelled = true, Why = "set aside before it came"} : Fell("the composer timed out");
         }
         catch (Exception ex)
         {
            return Fell($"the composer threw {ex.GetType().Name}");
         }

         if (!reply.IsUsable) return Fell("the composer gave no setting", reply);
         if (DialogueHeuristics.LooksLikeRefusal(reply.Setting)) return Fell("the composer refused", reply);

         // Stage 7 (Dramaturge mode): the events go to their own check, beside the opening's, so no wait is added and one
         // inventing event never sinks the opening. Without the switch the composer was asked for none, and none is kept.
         IReadOnlyList<string> proposed = sheet.ProposeEvents ? reply.Events : new string[0];
         Task<bool[]> eventCheck = proposed.Count == 0 ? Task.FromResult(new bool[0]) : CheckEventsAsync(sheet, proposed, checker, checkLimit, ct);

         try
         {
            LlmResponse c;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(checkLimit);
               c = await checker.CompleteAsync(new LlmRequest {
                  SystemPrompt = ComposedOpeningPrompt.CheckSystem,
                  Messages = new[] {new LlmMessage(MessageRole.User, ComposedOpeningPrompt.CheckUser(sheet, reply))},
                  Parameters = new LlmParameters {MaxTokens = 300, Creativity = 0.1f, AllowTruncationRetry = false, ReasoningOverride = "off"},
                  Subject = "composed opening check"
               }, cts.Token).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested) return new ComposedOpeningResult {Cancelled = true, Reply = reply, Why = "set aside before the check came"};
            if (c == null || !c.IsSuccess) return Fell($"the check failed ({c?.ErrorMessage ?? "no reply"})", reply);

            ComposedOpeningVerdict verdict = ComposedOpeningPrompt.ReadVerdict(c.Content);
            if (!verdict.Passes) return Fell($"the check found {verdict.Reason}", reply);
         }
         catch (OperationCanceledException)
         {
            return ct.IsCancellationRequested
               ? new ComposedOpeningResult {Cancelled = true, Reply = reply, Why = "set aside before the check came"}
               : Fell("the check timed out", reply);
         }
         catch (Exception ex)
         {
            return Fell($"the check threw {ex.GetType().Name}", reply);
         }

         if (proposed.Count == 0)
            return new ComposedOpeningResult {Delivered = true, Reply = WithEvents(reply, new string[0]), Why = "composed and checked: nothing invented"};

         bool[] verdicts = await eventCheck.ConfigureAwait(false);
         List<string> kept = proposed.Where((e, i) => i < verdicts.Length && verdicts[i]).ToList();

         return new ComposedOpeningResult {
            Delivered = true, Reply = WithEvents(reply, kept),
            Why = $"composed and checked: nothing invented; {kept.Count} of {proposed.Count} events kept"
         };
      }

      /// <summary>
      ///   Checks each proposed event against the facts (Stage 7). Never throws: a failed, timed-out or unreadable check keeps
      ///   none (only a plain "ok" keeps an event).
      /// </summary>
      public static async Task<bool[]> CheckEventsAsync(ComposedOpeningSheet sheet, IReadOnlyList<string> events, ILlmClient checker, TimeSpan limit, CancellationToken ct)
      {
         var none = new bool[events?.Count ?? 0];
         if (sheet == null || checker == null || none.Length == 0) return none;

         try
         {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(limit);
               Task<LlmResponse> call = checker.CompleteAsync(new LlmRequest {
                  SystemPrompt = ComposedOpeningPrompt.EventCheckSystem,
                  Messages = new[] {new LlmMessage(MessageRole.User, ComposedOpeningPrompt.EventCheckUser(sheet, events))},
                  Parameters = new LlmParameters {MaxTokens = 300, Creativity = 0.1f, AllowTruncationRetry = false, ReasoningOverride = "off"},
                  Subject = "composed opening events check"
               }, cts.Token);
               Task done = await Task.WhenAny(call, Task.Delay(limit, ct)).ConfigureAwait(false);
               if (done != call) return none;

               LlmResponse r = await call.ConfigureAwait(false);

               return r != null && r.IsSuccess ? ComposedOpeningPrompt.ReadEventVerdicts(r.Content, none.Length) : none;
            }
         }
         catch (Exception)
         {
            return none;
         }
      }

      // The reply with only the events kept (the parsed list is never handed on unchecked).
      private static ComposedOpeningReply WithEvents(ComposedOpeningReply reply, IReadOnlyList<string> events)
         => new() {Doing = reply.Doing, Setting = reply.Setting, Concern = reply.Concern, Tension = reply.Tension, Events = events};

      private static ComposedOpeningResult Fell(string why, ComposedOpeningReply reply = null)
         => new() {Delivered = false, Reply = reply, Why = why};
   }
}
