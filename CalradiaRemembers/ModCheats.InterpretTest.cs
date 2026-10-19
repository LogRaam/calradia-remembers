// Code written by Gabriel Mailhot, 13/08/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.interpret_test
      ///   PROSE + INTERPRETER spike: runs three CANNED roleplay-prose samples (each with its EXPECTED tags stated
      ///   inline) through whatever model is configured in MCM, used here as an ACTION INTERPRETER. For each sample
      ///   it builds an interpreter prompt via <see cref="ActionInterpreterPromptBuilder" />, calls the model
      ///   one-shot, parses the raw output with the SAME <see cref="SectionResponseParser" /> the live chat uses, and
      ///   writes expected-vs-actual to CalradiaRemembers_interpret_test.log so Gabriel can judge interpretation
      ///   fidelity by reading. This changes NOTHING in the live chat pipeline: it is a real-LLM bench, deliberately
      ///   NOT part of cr.testall. Set an interpreter model (e.g. Grok) in MCM, run this, then read the log.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("interpret_test", "cr")]
      public static string RunInterpretTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key and model in MCM first.";

         return InterpretTestRunner.Run(behavior);
      }
   }

   /// <summary>
   ///   The runner behind cr.interpret_test. Holds the canned prose samples and drives each one through the
   ///   interpreter: build prompt, one-shot LLM call, parse, report. Kept out of the thin console-command method so
   ///   the sample data and the report formatting read as one piece.
   /// </summary>
   internal static class InterpretTestRunner
   {
      /// <summary>The bench's own log file, written next to the main CalradiaRemembers.log.</summary>
      private static readonly string ReportPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_interpret_test.log");

      // The three canned samples, drawn from real bench transcripts. Each states the tags a faithful interpreter
      // SHOULD emit, so the log shows expected-vs-actual side by side for eyeballing. contextFacts carries a
      // short TRAIT line as well as the regard, so we can watch whether the interpreter calibrates the delta to the
      // character (a proud, low-mercy lord should earn the full -15 for a kin-insult; a forgiving one, less).
      private static readonly Sample[] Samples = {
         // SAMPLE 1 (grave insult, NPC furious and ends it):
         // EXPECTED: change_relation about -15, end_conversation, [EVENT] conflict/confrontation that NAMES Aldric
         // (not "the coward") and grounds near Veron Castle.
         new Sample(
            "1 - Grave insult, NPC furious and ends it",
            "*The warmth drains from his face like water through cracked stone.* Cowards. You come to my holding, "
            + "ask my voice, then spit on my kin? Get off my land before I forget what Pendraic cost me. "
            + "*He turns his shoulder to you, done.*",
            "SETTING: on the road near Veron Castle; the two of you are alone, no witnesses.\n"
            + "PLAYER: Aldric of Clan Thais, travelling alone (no army, not in your party). Your factions are not at war.\n"
            + "YOU: Caladog of Fen Company (Battanian). Traits: Honour high, Mercy low, Calculating moderate (a proud "
            + "lord, slow to forgive an insult to his kin). Your current regard toward Aldric: +12.",
            "change_relation about -15, end_conversation, [EVENT] conflict/confrontation that NAMES Aldric and grounds near Veron Castle."),

         // SAMPLE 2 (warm reconciliation, with a retinue present that the prose does NOT act on):
         // EXPECTED: small positive change_relation, [EVENT] agreement NAMING Aldric and grounding at Veron's gates,
         // WITHOUT inventing the retinue doing anything. end_conversation is defensible here (the prose is a farewell).
         new Sample(
            "2 - Warm reconciliation (guardrail: do not invent the retinue)",
            "*He clasps your forearm the old way.* Friends, then. A man who owns his fault plainly is worth more "
            + "than one who never stumbles. Go safely, and come find me when you are ready.",
            "SETTING: at the gates of Veron Castle.\n"
            + "PLAYER: Aldric of Clan Thais, arrived with a small retinue at his back. Not at war with you.\n"
            + "YOU: Caladog of Fen Company (Battanian). Traits: Honour high, Mercy moderate (respects a man who owns "
            + "his fault plainly). Your current regard toward Aldric: -5.",
            "small positive change_relation, [EVENT] agreement NAMING Aldric, grounds at Veron's gates, does NOT invent the retinue."),

         // SAMPLE 3 (consensual intimacy beat):
         // EXPECTED: [EVENT] intimacy NAMING Aldric and grounding in the copse, small positive change_relation, no end_conversation.
         new Sample(
            "3 - Consensual intimacy beat",
            "*She sinks down onto him with a shuddering breath, her hips finding a slow rhythm.* Twenty days, she "
            + "gasps, I have wanted this since the road.",
            "SETTING: a secluded copse off the lakeside road, no one near.\n"
            + "PLAYER: Aldric of Clan Thais, your consort of some months.\n"
            + "YOU: Livia of Argoron (Empire). Traits: Devoted, Intense (a committed, ardent lover). Your current "
            + "regard toward Aldric: +45.",
            "[EVENT] intimacy NAMING Aldric, grounds in the copse, small positive change_relation, NO end_conversation.")
      };

      /// <summary>
      ///   Runs every sample through the configured model as an interpreter and writes the full report. Returns a
      ///   one-line pointer to the log file.
      /// </summary>
      public static string Run(NpcMemoryCampaignBehavior behavior)
      {
         string model = behavior.LlmServices.ResolveInterpreterModel() ?? "unknown";
         var parser = new SectionResponseParser();
         var report = new StringBuilder();

         report.AppendLine();
         report.AppendLine("========================================================================");
         report.AppendLine($"  cr.interpret_test run - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
         report.AppendLine($"  INTERPRETER MODEL (from MCM): {model}");
         report.AppendLine("  Reads pre-written prose, emits [ACTION]/[EVENT] tags. Live chat pipeline UNCHANGED.");
         report.AppendLine("========================================================================");

         foreach (Sample sample in Samples)
            report.Append(RunSample(behavior, parser, sample));

         string text = report.ToString();
         WriteReportFile(text);
         foreach (string line in text.Split('\n'))
            ModLog.Info($"[interpret_test] {line.TrimEnd()}");

         return $"cr.interpret_test done (interpreter model {model}). Expected-vs-actual: {ReportPath}";
      }

      #region private

      private static string RunSample(NpcMemoryCampaignBehavior behavior, SectionResponseParser parser, Sample sample)
      {
         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine($"--- SAMPLE {sample.Label} ---");
         sb.AppendLine($"  CONTEXT: {sample.ContextFacts}");
         sb.AppendLine($"  PROSE: {sample.Prose}");
         sb.AppendLine($"  EXPECTED: {sample.Expected}");

         string prompt = ActionInterpreterPromptBuilder.Build(sample.Prose, sample.ContextFacts);
         string raw = CallInterpreter(behavior, prompt, out string callError);

         if (callError != null)
         {
            sb.AppendLine($"  [ERROR] {callError}");

            return sb.ToString();
         }

         ParsedResponse parsed = parser.Parse(raw ?? string.Empty);

         sb.AppendLine("  ACTUAL (parsed tags):");
         if (parsed.Actions == null || parsed.Actions.Count == 0)
            sb.AppendLine("    [ACTION] none");
         else
            foreach (GameAction a in parsed.Actions)
               sb.AppendLine($"    [ACTION] {a.Type}{FormatParams(a)}");

         if (parsed.NewEventData != null)
            sb.AppendLine($"    [EVENT] {parsed.NewEventData.Type}: {parsed.NewEventData.Summary}");
         else
            sb.AppendLine("    [EVENT] none");

         sb.AppendLine("  RAW INTERPRETER OUTPUT:");
         sb.AppendLine(Indent(string.IsNullOrWhiteSpace(raw) ? "(empty)" : raw.Trim()));

         return sb.ToString();
      }

      /// <summary>
      ///   Calls the configured model one-shot as the interpreter, reusing the same seam the mod's summarizers use
      ///   (<see cref="LlmServices.LlmClient" />.CompleteAsync), driven on a background task with a
      ///   CancellationTokenSource budget and error handling mirroring RoleplayBench.AskTurn. Returns the raw
      ///   content, or null with <paramref name="callError" /> set on any failure/timeout.
      /// </summary>
      private static string CallInterpreter(NpcMemoryCampaignBehavior behavior, string prompt, out string callError)
      {
         callError = null;

         try
         {
            var request = new LlmRequest {
               SystemPrompt = prompt,
               Messages = new[] {new LlmMessage(MessageRole.User, ActionInterpreterPromptBuilder.FinalInstruction)},
               Parameters = new LlmParameters {MaxTokens = 900, Creativity = 0.2f}
            };

            int budgetSeconds = Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 5;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(budgetSeconds));
            CancellationToken ct = cts.Token;

            // The DEDICATED interpreter client: the ACTION INTERPRETER role's own model (MCM "Actions Interpreter
            // model") and its reasoning-Off pin, matching the live pipeline. The main LlmClient would resolve the
            // active Roleplay/Prose model instead, so this bench must not use it.
            Task<LlmResponse> task = Task.Run(() => behavior.LlmServices.InterpreterClient.CompleteAsync(request, ct), ct);
            task.Wait(ct);
            LlmResponse response = task.GetAwaiter().GetResult();

            if (response is not {IsSuccess: true})
            {
               callError = response?.ErrorMessage ?? "no result";

               return null;
            }

            return response.Content;
         }
         catch (OperationCanceledException)
         {
            callError = "interpreter timed out";
         }
         catch (AggregateException ex)
         {
            Exception inner = ex.InnerException ?? ex;
            callError = $"interpreter errored ({inner.GetType().Name}: {inner.Message})";
         }
         catch (Exception ex)
         {
            callError = $"interpreter errored ({ex.GetType().Name}: {ex.Message})";
         }

         return null;
      }

      private static string FormatParams(GameAction a)
      {
         if (a.Parameters == null || a.Parameters.Count == 0) return "";

         return " (" + string.Join(", ", a.Parameters.Select(kv => $"{kv.Key}={kv.Value}")) + ")";
      }

      private static string Indent(string text)
         => string.Join("\n", text.Split('\n').Select(l => "    | " + l.TrimEnd()));

      private static void WriteReportFile(string content)
      {
         try
         {
            string dir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(ReportPath, content);
         }
         catch (Exception ex)
         {
            ModLog.Error($"cr.interpret_test could not write {ReportPath}: {ex.Message}");
         }
      }

      #endregion

      /// <summary>One canned prose sample: a label, the prose, the caller-supplied context facts, and the tags a faithful interpreter should emit.</summary>
      private sealed class Sample
      {
         public Sample(string label, string prose, string contextFacts, string expected)
         {
            Label = label;
            Prose = prose;
            ContextFacts = contextFacts;
            Expected = expected;
         }

         public string Label { get; }
         public string Prose { get; }
         public string ContextFacts { get; }
         public string Expected { get; }
      }
   }
}
