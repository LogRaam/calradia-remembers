// Code written by Gabriel Mailhot, 24/10/2026.

// The cost bench outside the game, for real money: the runner behind the cost-bench skill (Gabriel, 24/10/2026: "ça te
// donnerait la possibilité de vérifier des choses par toi-même. On pourrait même en faire un skill."). Never runs by
// itself: [Explicit], and it stops unless CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY are set (Gabriel runs NCrunch).
//
//   Replay   replays the last cr.cost_bench capture (Logs/CalradiaRemembers_cost_capture.json) for each [prose] and
//            [standard] row of the scenario file, against that row's model and reasoning, after a free check that the
//            replay still sends the game's bytes. A row needs a capture of its own kind (Enhanced or Standard, full or
//            Compact prompt).
//   Interpret reads the ordinary replies and the whole action catalog (or a sample) for each [interpret] row, as the
//            game's interpreter client sends them (reasoning off).
// Both append their tables, every billed call and the transcripts to Logs/CalradiaRemembers_cost_replay.log.
//
//   CR_REPLAY_SCENARIOS  the scenario file (default: Configs/CalradiaRemembers_cost_bench.txt, the game's own)
//   CR_REPLAY_CAPTURE    the capture (default: Logs/CalradiaRemembers_cost_capture.json)
//   CR_REPLAY_CAP        dollars at most per test (default 3)

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Logic.Diagnostics;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   [Explicit("Calls a real LLM and spends credit. Run deliberately: CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY, then --filter CostReplayLiveTests.")]
   public sealed class CostReplayLiveTests
   {
      private static readonly string Documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord");
      private static readonly string ReportPath = Path.Combine(Documents, "Logs", "CalradiaRemembers_cost_replay.log");
      private static readonly HttpClient Http = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};

      private string _key = "";

      [SetUp]
      public void Firewall()
      {
         if (Environment.GetEnvironmentVariable("CR_RUN_LIVE_LLM") != "1") Assert.Ignore("CR_RUN_LIVE_LLM is not 1: nothing is spent.");

         _key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
         if (string.IsNullOrWhiteSpace(_key)) Assert.Ignore("No OPENROUTER_API_KEY.");
      }

      [Test]
      public async Task Replay()
      {
         string capturePath = Env("CR_REPLAY_CAPTURE", Path.Combine(Documents, "Logs", "CalradiaRemembers_cost_capture.json"));
         CostCaptureFile capture = File.Exists(capturePath) ? CostCaptureFile.FromJson(File.ReadAllText(capturePath)) : null;
         if (capture == null || capture.Rows.Count == 0) Assert.Fail($"No capture at {capturePath}: run cr.cost_bench run in the game once.");

         double cap = Cap();
         var recorder = new CostRecorder();
         var results = new List<ProseRowResult>();
         var transcripts = new StringBuilder();
         var log = new StringBuilder();
         log.AppendLine($"Capture: {capturePath}, {capture.Version}, taken {capture.CapturedAtUtc} with {capture.Speaker}.");

         foreach (CaptureRow captured in capture.Rows) log.AppendLine("Fidelity: " + await Fidelity(captured));

         foreach (CostScenario row in Rows().Where(r => r.Pass == CostPass.Prose || r.Pass == CostPass.Standard))
         {
            CaptureRow captured = capture.Rows.FirstOrDefault(c => c.Pass == row.Pass && c.Compact == row.Compact);
            if (captured == null)
            {
               results.Add(CostBenchReport.SummarizeProse(row, recorder.Calls, null, $"not replayed: no {(row.Compact ? "Compact" : "full")} {row.Pass} row in the capture"));
               continue;
            }

            if (CostBenchReport.CapReached(recorder.Spent, cap))
            {
               results.Add(CostBenchReport.SummarizeProse(row, recorder.Calls, null, "not replayed: cap reached"));
               continue;
            }

            OpenRouterClient client = Client(row.Model, ReasoningKeyword(row.Reasoning), recorder);
            results.Add(await CostReplayRunner.ReplayAsync(captured, row, client, recorder, transcripts, cap));
         }

         Write("replay", capture.Version, results, new List<InterpretRowResult>(), recorder, log, transcripts);
      }

      [Test]
      public async Task Interpret()
      {
         double cap = Cap();
         List<CostScenario> rows = Rows().Where(r => r.Pass == CostPass.Interpret).ToList();
         if (rows.Count == 0) Assert.Fail("No [interpret] row in the scenario file.");

         // One recorder per model so the rows can read side by side: each model's own cache, each row's own clock.
         var runs = rows.Select(r => (Row: r, Recorder: new CostRecorder(), Outcomes: new List<InterpretOutcome>())).ToList();
         await Task.WhenAll(runs.Select(run => Task.Run(() => CostReplayRunner.ReadAsync(run.Row, Client(run.Row.Model, "off", run.Recorder), run.Recorder,
            run.Outcomes, cap / runs.Count))));

         var all = new CostRecorder();
         var results = new List<InterpretRowResult>();
         foreach (var run in runs)
         {
            all.Calls.AddRange(run.Recorder.Calls);
            results.Add(CostBenchReport.SummarizeInterpret(run.Row, run.Recorder.Calls, run.Outcomes));
         }

         Write("interpret", "(outside the game)", new List<ProseRowResult>(), results, all, new StringBuilder(), new StringBuilder());
      }

      // Free: the captured row replayed with its own model and its own replies, through a handler that answers with them.
      // Every body must be the game's; a difference means the SDK changed since the capture, or the replay drifted.
      private static async Task<string> Fidelity(CaptureRow captured)
      {
         var replies = new Queue<string>(captured.Steps.Select(s => s.Reply));
         var handler = new EchoHandler(() => replies.Count > 0 ? replies.Dequeue() : "");
         var client = new OpenRouterClient(new HttpClient(handler), Config(captured.Model, ReasoningKeyword(captured.Reasoning), "dry-run"));
         await CostReplayRunner.ReplayAsync(captured, new CostScenario {Name = captured.Name, Pass = captured.Pass, Model = captured.Model}, client,
            new CostRecorder(), null, 0);

         int same = captured.Steps.Where((s, i) => i < handler.Bodies.Count && handler.Bodies[i] == s.PayloadJson).Count();
         string first = captured.Steps.Select((s, i) => (s, i)).Where(x => x.i >= handler.Bodies.Count || handler.Bodies[x.i] != x.s.PayloadJson)
                                .Select(x => $"; first difference at {x.s.Phase} {x.s.Turn}, char {CostBenchReport.FirstDivergence(x.s.PayloadJson, x.i < handler.Bodies.Count ? handler.Bodies[x.i] : "")}")
                                .FirstOrDefault() ?? "";

         return $"{captured.Name} ({captured.Pass}{(captured.Compact ? ", Compact" : "")}): {same}/{captured.Steps.Count} bodies identical to the game's{first}";
      }

      private void Write(string what, string version, List<ProseRowResult> prose, List<InterpretRowResult> interpret, CostRecorder recorder,
                         StringBuilder log, StringBuilder transcripts)
      {
         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine("========================================================================");
         sb.AppendLine($"  cost replay ({what}) - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - outside the game, from a capture of {version}");
         sb.AppendLine($"  Spent: ${CostBenchReport.Money(recorder.Calls.Sum(c => c.Cost ?? 0))}");
         sb.AppendLine("========================================================================");
         sb.Append(log);
         sb.AppendLine();
         sb.AppendLine(CostBenchReport.Markdown(prose, interpret, new List<FoldRowResult>(), version, DateTime.Now));
         sb.AppendLine("Every billed call:");
         foreach (CostCall c in recorder.Calls)
            sb.AppendLine($"  [{c.Pass}] {c.ScenarioName} | turn {c.Turn} | {c.Phase}{(c.Phase == CostPhase.Interpreter ? " " + c.Difficulty : "")} | {c.Model} | in {c.PromptTokens} (cached {c.CachedTokens}) | out {c.CompletionTokens} (thinking {c.ReasoningTokens}) | {(c.Cost == null ? "unpriced" : "$" + CostBenchReport.Money(c.Cost.Value))} | {c.Seconds:0.0} s");
         sb.AppendLine();
         if (transcripts.Length > 0)
         {
            sb.AppendLine("Transcripts:");
            sb.Append(transcripts);
         }

         File.AppendAllText(ReportPath, sb.ToString());
         TestContext.Out.WriteLine(sb.ToString());
      }

      private OpenRouterClient Client(string model, string reasoning, CostRecorder recorder)
         => new OpenRouterClient(Http, Config(model, reasoning, _key)) {UsageObserver = recorder};

      // The game's OpenRouter client, as LlmServices builds it for a role (the app headers aside, which never reach the body).
      private static OpenRouterConfig Config(string model, string reasoning, string key)
         => new OpenRouterConfig {
            ApiKey = key, Model = model, ReasoningProvider = () => reasoning, IsOpenRouterProvider = () => true,
            AppTitle = "Calradia Remembers (cost replay)", TimeoutSecondsProvider = () => 180
         };

      // The MCM drop-down's words as the client reads them: "Default" leaves the model its own.
      private static string ReasoningKeyword(string setting)
         => string.IsNullOrWhiteSpace(setting) || string.Equals(setting, "Default", StringComparison.OrdinalIgnoreCase) ? null : setting.Trim().ToLowerInvariant();

      private static IReadOnlyList<CostScenario> Rows()
      {
         string path = Env("CR_REPLAY_SCENARIOS", Path.Combine(Documents, "Configs", "CalradiaRemembers_cost_bench.txt"));
         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(File.ReadAllText(path), out List<string> problems);
         foreach (string p in problems) TestContext.Out.WriteLine($"Scenario file: {p}");

         return rows;
      }

      private static double Cap() => double.TryParse(Env("CR_REPLAY_CAP", "3"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cap) ? cap : 3;

      private static string Env(string name, string fallback)
         => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)) ? fallback : Environment.GetEnvironmentVariable(name)!;

      private sealed class EchoHandler : HttpMessageHandler
      {
         private readonly Func<string> _reply;

         public EchoHandler(Func<string> reply) => _reply = reply;

         public List<string> Bodies { get; } = new List<string>();

         protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
         {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK) {
               Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":" + Newtonsoft.Json.JsonConvert.ToString(_reply())
                                           + "},\"finish_reason\":\"stop\"}]}")
            };
         }
      }
   }
}
