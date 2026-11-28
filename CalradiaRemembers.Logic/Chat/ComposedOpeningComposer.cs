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

#region

using System;
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
                  Parameters = new LlmParameters {MaxTokens = 800, Creativity = 0.7f, AllowTruncationRetry = false},
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

         try
         {
            LlmResponse c;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(checkLimit);
               c = await checker.CompleteAsync(new LlmRequest {
                  SystemPrompt = ComposedOpeningPrompt.CheckSystem,
                  Messages = new[] {new LlmMessage(MessageRole.User, ComposedOpeningPrompt.CheckUser(sheet, reply))},
                  Parameters = new LlmParameters {MaxTokens = 300, Creativity = 0.1f, AllowTruncationRetry = false},
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

         return new ComposedOpeningResult {Delivered = true, Reply = reply, Why = "composed and checked: nothing invented"};
      }

      private static ComposedOpeningResult Fell(string why, ComposedOpeningReply reply = null)
         => new() {Delivered = false, Reply = reply, Why = why};
   }
}
