// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 4 of the group-scene plan, "generated depth" (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "go pour tout le système ... je testerai demain"; on the secret (27/11/2026): "si ça ne touche pas à une
// mécanique du jeu ... Le problème arrive lorsque le LLM invente quelque chose que le jeu peut contredire."
//
// The two calls of a generated depth: the generator (the Background model, once per character) and the secret's check (the
// interpreter model, cheap). The check fails closed: a secret is kept only on a plain "ok"; no checker, a failure, a timeout or
// an unreadable verdict drop it, and the rest of the depth stays. The clients are parameters: the engine passes the live ones,
// cr.test generated_depth and the unit tests stand-ins with fixed replies. Never throws and never touches the game.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>How a generation ended.</summary>
   public sealed class GeneratedDepthResult
   {
      /// <summary>The depth to keep for good, or null when nothing usable came (nothing is stored then; a later chat may try again).</summary>
      public GeneratedDepth Depth { get; init; }

      /// <summary>True when a secret was written and then dropped (the word screen or the check).</summary>
      public bool SecretDropped { get; init; }

      /// <summary>One line for the log: what was kept, or why nothing was.</summary>
      public string Why { get; init; } = "";
   }

   /// <summary>Runs the generator and the secret's check over one sheet. Never throws.</summary>
   public static class GeneratedDepthGenerator
   {
      /// <summary>
      ///   Generates a depth from <paramref name="sheet" /> on <paramref name="generator" /> and checks its secret on
      ///   <paramref name="checker" /> (null: the secret is dropped). Each call has its own time limit; <paramref name="ct" /> cuts both.
      /// </summary>
      public static async Task<GeneratedDepthResult> GenerateAsync(GeneratedDepthSheet sheet, ILlmClient generator, ILlmClient checker, int day,
                                                                   TimeSpan generatorLimit, TimeSpan checkLimit, CancellationToken ct)
      {
         if (sheet == null || generator == null) return new GeneratedDepthResult {Why = "nothing to generate with"};

         GeneratedDepthReply reply;
         try
         {
            LlmResponse r;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(generatorLimit);
               r = await generator.CompleteAsync(new LlmRequest {
                  SystemPrompt = GeneratedDepthPrompt.GeneratorSystem(sheet),
                  Messages = new[] {new LlmMessage(MessageRole.User, GeneratedDepthPrompt.GeneratorUser(sheet))},
                  // The simulator ran it at 0.8: the depth is invention within the sheet. Short, no reasoning.
                  Parameters = new LlmParameters {MaxTokens = 600, Creativity = 0.8f, ReasoningOverride = "off", AllowTruncationRetry = false},
                  Subject = "generated depth"
               }, cts.Token).ConfigureAwait(false);
            }

            if (r == null || !r.IsSuccess) return new GeneratedDepthResult {Why = $"the generator failed ({r?.ErrorMessage ?? "no reply"})"};

            reply = GeneratedDepthPrompt.Parse(r.Content, sheet);
         }
         catch (OperationCanceledException)
         {
            return new GeneratedDepthResult {Why = ct.IsCancellationRequested ? "cancelled" : "the generator timed out"};
         }
         catch (Exception ex)
         {
            return new GeneratedDepthResult {Why = $"the generator threw {ex.GetType().Name}"};
         }

         if (!reply.IsUsable) return new GeneratedDepthResult {Why = "the generator gave no usable depth (a mask and a limit at the least)"};
         if (reply.Secret.Length == 0) return Kept(reply, false, day, "no secret was written");
         if (GeneratedDepthPrompt.NamesATrackedFact(reply.Secret)) return Kept(reply, false, day, "the secret names what the game tracks; dropped");
         if (checker == null) return Kept(reply, false, day, "no checker to read the secret; dropped");

         try
         {
            LlmResponse c;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(checkLimit);
               c = await checker.CompleteAsync(new LlmRequest {
                  SystemPrompt = GeneratedDepthPrompt.CheckSystem,
                  Messages = new[] {new LlmMessage(MessageRole.User, GeneratedDepthPrompt.CheckUser(sheet, reply.Secret))},
                  Parameters = new LlmParameters {MaxTokens = 200, Creativity = 0.1f, AllowTruncationRetry = false},
                  Subject = "generated depth check"
               }, cts.Token).ConfigureAwait(false);
            }

            if (c == null || !c.IsSuccess) return Kept(reply, false, day, $"the check failed ({c?.ErrorMessage ?? "no reply"}); the secret is dropped");

            GeneratedDepthVerdict verdict = GeneratedDepthPrompt.ReadVerdict(c.Content);
            return verdict.Passes ? Kept(reply, true, day, "the secret passed the check") : Kept(reply, false, day, $"the check found {verdict.Reason}; the secret is dropped");
         }
         catch (OperationCanceledException)
         {
            return Kept(reply, false, day, "the check timed out; the secret is dropped");
         }
         catch (Exception ex)
         {
            return Kept(reply, false, day, $"the check threw {ex.GetType().Name}; the secret is dropped");
         }
      }

      private static GeneratedDepthResult Kept(GeneratedDepthReply reply, bool keepSecret, int day, string why)
         => new() {
            Depth = GeneratedDepthPrompt.ToDepth(reply, keepSecret, day),
            SecretDropped = !keepSecret && reply.Secret.Length > 0,
            Why = why
         };
   }
}
