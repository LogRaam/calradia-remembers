// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 6 of the group-scene plan (docs/design/group-chat/integration-plan-2026-11-28.md), the Dramaturge mode's two cheap
// calls on the interpreter client: the inner-life update after each line, and the keeper's reading of each reply with its one
// rewrite. Gabriel, 28/11/2026: "le mode dramaturge avec DRAMA+PROSE+INTERPRET+NSFW". NOT A/B-validated.
//
// The clients and the rewrite are parameters: the chat passes the live interpreter client and a rewrite that calls the prose
// model again (rolling the session back as the prose fallback does); cr.test dramaturge_mode and the unit tests pass stand-ins.
// Each cheap call has a short limit, and on failure or timeout the turn carries on without it: the reply is shown as it is,
// the state stays as it was. Never throws and never touches the game.

#region

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>How one inner-life update ended.</summary>
   public sealed class DramaturgeMindResult
   {
      /// <summary>The state now: the new one, or the old one when the reader failed.</summary>
      public DramaturgeInnerState State { get; init; }

      /// <summary>True when the reader answered and the state was read from it.</summary>
      public bool Updated { get; init; }

      /// <summary>One line for the log.</summary>
      public string Why { get; init; } = "";
   }

   /// <summary>How the keeper's reading of one reply ended.</summary>
   public sealed class DramaturgeKeeperResult
   {
      /// <summary>The text shown: the reply as it came, or the rewrite.</summary>
      public string Text { get; init; }

      /// <summary>True when the rewrite is the text shown.</summary>
      public bool Rewritten { get; init; }

      /// <summary>The first reading, or null when the keeper failed or timed out.</summary>
      public KeeperVerdict First { get; init; }

      /// <summary>The reading of the rewrite, or null when there was none.</summary>
      public KeeperVerdict Second { get; init; }

      /// <summary>The lines for the log, in order.</summary>
      public IReadOnlyList<string> Why { get; init; } = new List<string>();
   }

   /// <summary>Runs the inner-life reader and the keeper. Never throws.</summary>
   public static class DramaturgeTurnComposer
   {
      /// <summary>
      ///   Updates the inner life after the last line of <paramref name="sceneText" /> on <paramref name="reader" />, within
      ///   <paramref name="limit" />. Keeps <paramref name="before" /> on any failure.
      /// </summary>
      public static async Task<DramaturgeMindResult> UpdateMindAsync(ILlmClient reader, DramaturgeSheet sheet, DramaturgeInnerState before, string sceneText,
                                                                     TimeSpan limit, CancellationToken ct)
      {
         DramaturgeInnerState kept = before ?? new DramaturgeInnerState();
         if (reader == null || sheet == null) return new DramaturgeMindResult {State = kept, Why = "no reader"};

         string content = await AskAsync(reader, DramaturgeInnerLife.System(sheet), DramaturgeInnerLife.User(sheet, kept, sceneText), 300, 0.4f, "inner life", limit, ct).ConfigureAwait(false);
         if (content == null) return new DramaturgeMindResult {State = kept, Why = "the inner-life reader failed or timed out; the state stays as it was"};

         DramaturgeInnerState now = DramaturgeInnerLife.Apply(kept, content);
         if (ReferenceEquals(now, kept)) return new DramaturgeMindResult {State = kept, Why = "the inner-life reader answered nothing usable; the state stays as it was"};

         return new DramaturgeMindResult {State = now, Updated = true, Why = DramaturgeInnerLife.Describe(now)};
      }

      /// <summary>
      ///   Reads <paramref name="reply" /> on <paramref name="keeper" />. On a problem, asks <paramref name="rewrite" /> once with
      ///   the reason (it returns the rewrite's text, or null when it failed or must not be shown), reads the rewrite once more,
      ///   and shows it whatever that second reading says. Establishes what the text shown made true in <paramref name="record" />.
      /// </summary>
      public static async Task<DramaturgeKeeperResult> KeepAsync(ILlmClient keeper, DramaturgeSheet sheet, DramaturgeInnerState state, DramaturgeRecord record,
                                                                 string sceneText, string reply, Func<string, Task<string>> rewrite,
                                                                 TimeSpan limit, CancellationToken ct)
      {
         var why = new List<string>();
         if (keeper == null || sheet == null || string.IsNullOrWhiteSpace(reply))
            return new DramaturgeKeeperResult {Text = reply, Why = why};

         KeeperVerdict first = await ReadAsync(keeper, sheet, state, record, sceneText, reply, limit, ct).ConfigureAwait(false);
         if (first == null)
         {
            why.Add("the keeper failed or timed out; the reply is shown as it came");

            return new DramaturgeKeeperResult {Text = reply, Why = why};
         }

         if (!DramaturgeRewritePolicy.ShouldRewrite(first))
         {
            why.Add(first.HadVerdict ? "the keeper passed the reply" : "the keeper gave no verdict; the reply passes");
            record?.Establish(first);

            return new DramaturgeKeeperResult {Text = reply, First = first, Why = why};
         }

         string reason = DramaturgeKeeper.RewriteReason(first);
         why.Add($"the keeper found a {DramaturgeKeeper.Name(first.Problem)}: {first.Detail}; written again");
         string second = null;
         try
         {
            second = rewrite == null ? null : await rewrite(reason).ConfigureAwait(false);
         }
         catch (Exception ex)
         {
            why.Add($"the rewrite threw {ex.GetType().Name}");
         }

         if (string.IsNullOrWhiteSpace(second))
         {
            why.Add("the rewrite did not come (or could not be shown); the first reply is shown as it came");

            return new DramaturgeKeeperResult {Text = reply, First = first, Why = why};
         }

         KeeperVerdict again = await ReadAsync(keeper, sheet, state, record, sceneText, second, limit, ct).ConfigureAwait(false);
         if (again == null) why.Add("the rewrite is shown (its reading failed or timed out)");
         else if (DramaturgeRewritePolicy.ShouldRewrite(again)) why.Add($"the rewrite still has a {DramaturgeKeeper.Name(again.Problem)}: {again.Detail}; it is shown all the same");
         else why.Add("the rewrite passed and is shown");
         record?.Establish(again);

         return new DramaturgeKeeperResult {Text = second, Rewritten = true, First = first, Second = again, Why = why};
      }

      /// <summary>
      ///   Stage 7: the dramaturge's reading of the scene, on <paramref name="dramaturge" /> (the DRAMATURGE client, the prose
      ///   model unless Gabriel set another) within <paramref name="limit" />. Null on any failure, timeout or cancel: the scene
      ///   goes on as it is.
      /// </summary>
      public static async Task<DramaturgeReading> ReadSceneAsync(ILlmClient dramaturge, DramaturgeSheet sheet, DramaturgeInnerState state, string sceneText,
                                                                 string recordText, IReadOnlyList<KeyValuePair<int, string>> waiting, int beatsPlayed,
                                                                 TimeSpan limit, CancellationToken ct)
      {
         if (dramaturge == null || sheet == null) return null;

         // The simulator's budget: a prose model that reasons spends much of a small one before its four lines.
         string content = await AskAsync(dramaturge, DramaturgeDirector.System, DramaturgeDirector.User(sheet, state, sceneText, recordText, waiting, beatsPlayed),
                                         1500, 0.3f, "dramaturge", limit, ct).ConfigureAwait(false);

         return content == null ? null : DramaturgeDirector.Parse(content);
      }

      private static async Task<KeeperVerdict> ReadAsync(ILlmClient keeper, DramaturgeSheet sheet, DramaturgeInnerState state, DramaturgeRecord record,
                                                         string sceneText, string reply, TimeSpan limit, CancellationToken ct)
      {
         // The verdict comes after the reasoning, so the reply is longer: cut short, it would lose the verdict and pass the line.
         string content = await AskAsync(keeper, DramaturgeKeeper.System(sheet.NpcName), DramaturgeKeeper.User(sheet, state, record, sceneText, reply),
                                         600, 0.1f, "keeper", limit, ct).ConfigureAwait(false);

         return content == null ? null : DramaturgeKeeper.Parse(content);
      }

      // One cheap call within its limit: the reply's content, or null on any failure, timeout or cancel.
      private static async Task<string> AskAsync(ILlmClient client, string system, string user, int maxTokens, float creativity, string subject,
                                                 TimeSpan limit, CancellationToken ct)
      {
         try
         {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
               cts.CancelAfter(limit);
               Task<LlmResponse> call = client.CompleteAsync(new LlmRequest {
                  SystemPrompt = system,
                  Messages = new[] {new LlmMessage(MessageRole.User, user)},
                  Parameters = new LlmParameters {MaxTokens = maxTokens, Creativity = creativity, AllowTruncationRetry = false},
                  Subject = subject
               }, cts.Token);
               // A client that ignores its token still cannot hold the turn past the limit.
               Task done = await Task.WhenAny(call, Task.Delay(limit, ct)).ConfigureAwait(false);
               if (done != call) return null;

               LlmResponse r = await call.ConfigureAwait(false);

               return r != null && r.IsSuccess ? r.Content ?? "" : null;
            }
         }
         catch (Exception)
         {
            return null;
         }
      }
   }
}
