// Code written by Gabriel Mailhot, 24/10/2026.
// The cost bench, replayed outside the game. Gabriel (24/10/2026), asked whether the bench could run without the game:
// "oui ça pourrait valoir la peine car ça te donnerait la possibilité de vérifier des choses par toi-même. On pourrait
// même en faire un skill." The interpreter already ran outside it; the prose could not, because a character's prompt is
// built from the save (profile, memory, regard, the world, the verbs the mod allows). So cr.cost_bench CAPTURES what it
// sends, request by request, exactly as the game sent it, and this replays the capture against any model through the
// same client: the same prompt to the character, the same cache key, the same reasoning rules, the bill read the same
// way, the same tables. One thing differs, and it is handled here: where a request carries a reply the captured model
// wrote earlier in the same conversation (the scene, the opening replies, the warm-up of the middle and of the long
// talk), the replayed model's own reply takes its place, so every model answers over its own words.

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Everything one cr.cost_bench run sent for its prose and standard rows, request by request.</summary>
   public sealed class CostCaptureFile
   {
      /// <summary>The mod's version as SubModule.xml stamped it.</summary>
      public string Version { get; set; } = "";

      /// <summary>When the capture was taken (UTC, ISO 8601): a prompt changed since then makes it stale.</summary>
      public string CapturedAtUtc { get; set; } = "";

      /// <summary>The character spoken to.</summary>
      public string Speaker { get; set; } = "";

      /// <summary>One entry per prose or standard row played.</summary>
      public List<CaptureRow> Rows { get; set; } = new List<CaptureRow>();

      /// <summary>The file as written to disk.</summary>
      public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented, new StringEnumConverter());

      /// <summary>A capture read back, or null when the text is not one.</summary>
      public static CostCaptureFile FromJson(string json)
      {
         try { return JsonConvert.DeserializeObject<CostCaptureFile>(json ?? "", new StringEnumConverter()); }
         catch (JsonException) { return null; }
      }
   }

   /// <summary>One played row: its setup, and each call it made in order.</summary>
   public sealed class CaptureRow
   {
      /// <summary>The row's name.</summary>
      public string Name { get; set; } = "";

      /// <summary>The model that played it.</summary>
      public string Model { get; set; } = "";

      /// <summary>Prose (Enhanced) or Standard.</summary>
      public CostPass Pass { get; set; }

      /// <summary>Whether the prompt was the Compact one: a Compact prompt cannot be derived from a full one.</summary>
      public bool Compact { get; set; }

      /// <summary>The reasoning effort it was played at.</summary>
      public string Reasoning { get; set; } = "";

      /// <summary>The calls, in the order they were made.</summary>
      public List<CaptureStep> Steps { get; set; } = new List<CaptureStep>();
   }

   /// <summary>One call: the request as it went, and the reply the conversation kept.</summary>
   public sealed class CaptureStep
   {
      /// <summary>Opener, Prose or Letter.</summary>
      public CostPhase Phase { get; set; }

      /// <summary>The player's line it answered, from 1; 0 for the scene opener and the letter.</summary>
      public int Turn { get; set; }

      /// <summary>The cacheable part of the system prompt (empty when the request had no split).</summary>
      public string StablePrefix { get; set; } = "";

      /// <summary>The rest of the system prompt, sent fresh each turn.</summary>
      public string DynamicTail { get; set; } = "";

      /// <summary>The messages after the system prompt.</summary>
      public List<CaptureMessage> Messages { get; set; } = new List<CaptureMessage>();

      /// <summary>The reply budget.</summary>
      public int MaxTokens { get; set; }

      /// <summary>The temperature.</summary>
      public float Creativity { get; set; }

      /// <summary>The anti-repetition penalty.</summary>
      public float PresencePenalty { get; set; }

      /// <summary>Whether a cut reply was retried with a bigger budget (it never reaches the wire, so it is kept here).</summary>
      public bool AllowTruncationRetry { get; set; }

      /// <summary>A per-call reasoning override, when the call carried one.</summary>
      public string ReasoningOverride { get; set; } = "";

      /// <summary>The body as it went on the wire, to prove a replay sends the same bytes.</summary>
      public string PayloadJson { get; set; } = "";

      /// <summary>The reply as the conversation kept it (the dialogue, on one line).</summary>
      public string Reply { get; set; } = "";
   }

   /// <summary>One message of a captured request.</summary>
   public sealed class CaptureMessage
   {
      /// <summary>True for the player's side.</summary>
      public bool IsPlayer { get; set; }

      /// <summary>The text as sent.</summary>
      public string Content { get; set; } = "";

      /// <summary>
      ///   The step whose reply this message carries, or -1: a character's line the bench wrote itself (the synthetic
      ///   history), or the player's.
      /// </summary>
      public int LiveStep { get; set; } = -1;
   }

   /// <summary>The pure part of a replay: which messages were the captured model's own words, and what replaces them.</summary>
   public static class CostReplayPolicy
   {
      /// <summary>
      ///   The messages of a request, each character's line marked with the earlier step whose reply it carries. The latest
      ///   step is tried first, so a reply quoted inside a later one is not mistaken for it.
      /// </summary>
      public static List<CaptureMessage> Tag(IEnumerable<LlmMessage> messages, IReadOnlyList<string> earlierReplies)
      {
         var tagged = new List<CaptureMessage>();
         foreach (LlmMessage m in messages ?? Enumerable.Empty<LlmMessage>())
         {
            if (m == null) continue;

            var message = new CaptureMessage {IsPlayer = m.Role == MessageRole.User, Content = m.Content ?? ""};
            if (!message.IsPlayer && earlierReplies != null)
               for (int k = earlierReplies.Count - 1; k >= 0; k--)
                  if (!string.IsNullOrWhiteSpace(earlierReplies[k]) && message.Content.Contains(earlierReplies[k]))
                  {
                     message.LiveStep = k;

                     break;
                  }

            tagged.Add(message);
         }

         return tagged;
      }

      /// <summary>
      ///   The messages of step <paramref name="stepIndex" /> for a replay: each line the captured model wrote earlier is
      ///   swapped for the replayed model's reply at that step. Where the replay has no reply there (it failed), the
      ///   captured one stays, so the conversation still reads.
      /// </summary>
      public static List<LlmMessage> Rebuild(CaptureRow row, int stepIndex, IReadOnlyList<string> replayReplies)
      {
         var messages = new List<LlmMessage>();
         CaptureStep step = row?.Steps != null && stepIndex >= 0 && stepIndex < row.Steps.Count ? row.Steps[stepIndex] : null;
         if (step == null) return messages;

         foreach (CaptureMessage m in step.Messages)
         {
            string content = m.Content ?? "";
            if (m.LiveStep >= 0 && m.LiveStep < row.Steps.Count && replayReplies != null && m.LiveStep < replayReplies.Count
                && !string.IsNullOrWhiteSpace(replayReplies[m.LiveStep]) && !string.IsNullOrEmpty(row.Steps[m.LiveStep].Reply))
               content = content.Replace(row.Steps[m.LiveStep].Reply, replayReplies[m.LiveStep]);

            messages.Add(new LlmMessage(m.IsPlayer ? MessageRole.User : MessageRole.Assistant, content));
         }

         return messages;
      }

      /// <summary>
      ///   A raw reply as the conversation keeps it: the dialogue the chat's parser reads out of it, on one line (the bench
      ///   records the same).
      /// </summary>
      public static string Clean(string raw)
      {
         string dialogue = new SectionResponseParser().Parse(raw ?? "")?.Dialogue ?? "";

         return dialogue.Replace("\r", " ").Replace("\n", " ");
      }

      /// <summary>The request a step makes again: the captured system prompt and settings, over the rebuilt messages.</summary>
      public static LlmRequest RequestFor(CaptureStep step, IReadOnlyList<LlmMessage> messages)
         => new LlmRequest {
            SystemPrompt = (step.StablePrefix ?? "") + (step.DynamicTail ?? ""),
            StableSystemPrompt = string.IsNullOrEmpty(step.StablePrefix) ? null : step.StablePrefix,
            Messages = messages,
            Parameters = new LlmParameters {
               MaxTokens = step.MaxTokens, Creativity = step.Creativity, PresencePenalty = step.PresencePenalty,
               AllowTruncationRetry = step.AllowTruncationRetry,
               ReasoningOverride = string.IsNullOrWhiteSpace(step.ReasoningOverride) ? null : step.ReasoningOverride
            }
         };
   }

   /// <summary>Records every billed attempt under the row, phase and turn being played. One row at a time.</summary>
   public sealed class CostRecorder : ILlmUsageObserver
   {
      private readonly object _gate = new object();
      private readonly Stopwatch _clock = new Stopwatch();
      private CostScenario _row = new CostScenario();
      private CostPhase _phase;
      private int _turn;
      private InterpretDifficulty _difficulty;

      /// <summary>Every billed attempt so far.</summary>
      public List<CostCall> Calls { get; } = new List<CostCall>();

      /// <summary>Dollars billed so far.</summary>
      public double Spent { get; private set; }

      /// <summary>The row being played.</summary>
      public CostScenario Row => _row;

      /// <summary>The call being made.</summary>
      public CostPhase Phase => _phase;

      /// <summary>The turn being answered.</summary>
      public int Turn => _turn;

      /// <summary>
      ///   Names the call about to be made. The clock restarts here and at each answered attempt, so an attempt's seconds are
      ///   what the player waited for it, the bench's own bookkeeping excluded.
      /// </summary>
      public void Begin(CostScenario row, CostPhase phase, int turn, InterpretDifficulty difficulty = InterpretDifficulty.NoAction)
      {
         lock (_gate)
         {
            _row = row;
            _phase = phase;
            _turn = turn;
            _difficulty = difficulty;
            _clock.Restart();
         }
      }

      /// <inheritdoc />
      public void OnAttempt(string model, LlmResponse response)
      {
         LlmUsage u = response?.Usage;
         if (u == null) return; // a failed attempt is not billed

         lock (_gate)
         {
            Calls.Add(new CostCall {
               ScenarioName = _row.Name, Pass = _row.Pass, Phase = _phase, Turn = _turn, Difficulty = _difficulty, Model = model ?? "",
               PromptTokens = u.PromptTokens, CachedTokens = u.CachedPromptTokens ?? 0, CompletionTokens = u.CompletionTokens,
               ReasoningTokens = u.ReasoningTokens ?? 0, Cost = u.Cost, Seconds = _clock.Elapsed.TotalSeconds
            });
            Spent += u.Cost ?? 0;
            _clock.Restart();
         }
      }
   }

   /// <summary>Plays a captured conversation again through a client, and reads the interpreter's cases.</summary>
   public static class CostReplayRunner
   {
      /// <summary>
      ///   Reads an interpret row's cases through <paramref name="interpreter" /> (built for the row's model, reasoning off as
      ///   the game sends it), one at a time, adding each outcome. Returns a note, empty when every case was read.
      /// </summary>
      public static async Task<string> ReadAsync(CostScenario row, ILlmClient interpreter, CostRecorder recorder, List<InterpretOutcome> outcomes,
                                                 double cap, CancellationToken ct = default)
      {
         foreach ((InterpretDifficulty difficulty, ActionBenchCase test) in InterpretCases.ForRow(row, ActionBenchCatalog.All))
         {
            if (CostBenchReport.CapReached(recorder.Spent, cap)) return "stopped early: cap reached";

            recorder.Begin(row, CostPhase.Interpreter, 0, difficulty);
            ActionBenchResult result = null;
            try { result = (await ActionBenchRunner.RunAsync(interpreter, new[] {test}, ct: ct).ConfigureAwait(false)).Results.FirstOrDefault(); }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { }

            (bool right, bool invented) = InterpretCases.Score(difficulty, result);
            outcomes.Add(new InterpretOutcome {ScenarioName = row.Name, Difficulty = difficulty, Right = right, Invented = invented});
         }

         return "";
      }

      /// <summary>
      ///   Replays <paramref name="captured" /> as <paramref name="row" /> through <paramref name="client" /> (built for the
      ///   row's model and reasoning), writing the conversation into <paramref name="transcripts" />. Returns the row's
      ///   summary, priced from <paramref name="recorder" />. The mod's prose fallback does not run here: a cut reply stays cut,
      ///   and the note says so.
      /// </summary>
      public static async Task<ProseRowResult> ReplayAsync(CaptureRow captured, CostScenario row, ILlmClient client, CostRecorder recorder,
                                                           StringBuilder transcripts, double cap, CancellationToken ct = default)
      {
         var replayReplies = new List<string>();
         var turnReplies = new List<string>();
         var notes = new List<string>();
         transcripts?.AppendLine($"--- [{row.Pass.ToString().ToLowerInvariant()}] {row.Name}: {row.Model}; {row.Setup} (replayed from {captured.Name}) ---");

         for (var i = 0; i < captured.Steps.Count; i++)
         {
            CaptureStep step = captured.Steps[i];
            if (CostBenchReport.CapReached(recorder.Spent, cap))
            {
               notes.Add("cap reached");

               break;
            }

            recorder.Begin(row, step.Phase, step.Turn);
            LlmResponse response;
            try { response = await client.CompleteAsync(CostReplayPolicy.RequestFor(step, CostReplayPolicy.Rebuild(captured, i, replayReplies)), ct).ConfigureAwait(false); }
            catch (Exception ex) { response = new LlmResponse {Content = "", IsSuccess = false, ErrorMessage = $"{ex.GetType().Name}: {ex.Message}"}; }

            string reply = response is {IsSuccess: true} ? CostReplayPolicy.Clean(response.Content) : "";
            replayReplies.Add(reply);
            if (step.Phase == CostPhase.Prose) turnReplies.Add(reply);
            if (response is {IsSuccess: true} && string.Equals(response.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
               notes.Add($"{step.Phase} {step.Turn}: cut short (the mod's fallback does not run outside the game)");

            string label = step.Phase == CostPhase.Opener ? "[scene]" : step.Phase == CostPhase.Letter ? "[letter]" : $"< ({step.Turn})";
            string player = step.Phase == CostPhase.Prose ? step.Messages.LastOrDefault(m => m.IsPlayer)?.Content : null;
            if (player != null) transcripts?.AppendLine($"> ({step.Turn}) {player}");
            transcripts?.AppendLine(response is {IsSuccess: true} ? $"{label} {reply}" : $"{label} ! {response?.ErrorMessage ?? "no reply"}");
         }

         transcripts?.AppendLine();

         return CostBenchReport.SummarizeProse(row, recorder.Calls, turnReplies, string.Join("; ", notes));
      }
   }
}
