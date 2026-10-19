// Code written by Gabriel Mailhot, 19/08/2026.
// The in-game runner for the single-call bench (cr.single_call_bench), increment 3 of the single-call experiment. For
// each scenario it makes TWO calls: (1) the SINGLE-CALL composer, forced to the tested model, writes the NPC reply AND
// self-tags it; (2) that SAME authored prose is fed to the dedicated INTERPRETER, and the two tag sets are compared by
// the pure SingleCallAgreement. A high agreement rate is the evidence that one call can retire the two-call pipeline;
// the divergences are where the single call's self-grounding drifts from an independent read. Real-LLM, spends tokens,
// deliberately NOT part of cr.testall. The scoring, corpus, and report are the SDK's pure code; this only supplies the
// live clients (via the LlmServices.TestModelOverride seam) and writes the scoreboard where Gabriel can read it.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Runs the single-call bench in-game: single-call self-tags vs interpreter tags on the same prose.</summary>
   public static class SingleCallBench
   {
      private static readonly string ReportPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_single_call_bench.log");

      /// <summary>
      ///   Runs every scenario <paramref name="passes" /> times, forcing the SINGLE-CALL to <paramref name="modelId" />
      ///   (or the configured scene model when null) and using the configured interpreter for the cross-check. Restores
      ///   the model override in the finally. Returns a one-line pointer to the scoreboard log.
      /// </summary>
      public static string Run(NpcMemoryCampaignBehavior behavior, int passes, string modelId, float singleCallTemp)
      {
         if (behavior == null) return "Calradia Remembers behavior not active. Start a campaign first.";
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key and model in MCM first.";

         if (passes < 1) passes = 1;
         if (singleCallTemp < 0f) singleCallTemp = 0.6f;

         string priorModel = LlmServices.TestModelOverride;
         var parser = new SectionResponseParser();
         var results = new List<SingleCallBenchResult>();

         // Capture the interpreter model with the override CLEARED, so the header names the true cross-check model and
         // not the tested single-call one.
         LlmServices.TestModelOverride = null;
         behavior.LlmServices.Invalidate();
         string interpreterModel = behavior.LlmServices.ResolveInterpreterModel() ?? "unknown";
         string singleCallModel = string.IsNullOrWhiteSpace(modelId) ? (behavior.LlmServices.ModelId ?? "scene model") : modelId.Trim();

         try
         {
            for (var pass = 0; pass < passes; pass++)
               foreach (SingleCallBenchScenario scenario in SingleCallBenchCatalog.All)
                  results.Add(RunOne(behavior, parser, scenario, modelId, singleCallTemp));
         }
         catch (Exception ex)
         {
            results.Add(new SingleCallBenchResult("(run aborted)", $"{ex.GetType().Name}: {ex.Message}"));
         }
         finally
         {
            LlmServices.TestModelOverride = priorModel;
            behavior.LlmServices.Invalidate();
         }

         var report = new SingleCallBenchReport(results);

         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine("========================================================================");
         sb.AppendLine($"  cr.single_call_bench run - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
         sb.AppendLine($"  Single-call model: {singleCallModel} @ temp {singleCallTemp:0.0#}  |  Interpreter (cross-check): {interpreterModel} @ temp 0.2");
         sb.AppendLine($"  Scenarios: {SingleCallBenchCatalog.All.Count}, passes each: {passes}. Agreement = self-tags == interpreter-tags on the SAME authored prose.");
         sb.AppendLine("========================================================================");
         sb.Append(report.Format());

         try { File.AppendAllText(ReportPath, sb.ToString()); }
         catch (Exception ex) { ModLog.Error($"[SINGLE-CALL-BENCH] could not write the report log: {ex.GetType().Name}: {ex.Message}"); }

         return $"cr.single_call_bench: {report.ScenariosAgreeing}/{report.TotalScenarios} agree "
              + $"(single-call {singleCallModel} vs interpreter {interpreterModel}). Scoreboard -> {ReportPath}";
      }

      // One scenario, one pass: force the tested model and make the single call (prose + self-tags), then clear the
      // override and run the interpreter over that authored prose. Either call failing is recorded, never scored as
      // agreement. The override toggles here (not once for the whole run) so the two calls hit different models even
      // when the tested single-call model and the interpreter model would otherwise resolve the same way.
      private static SingleCallBenchResult RunOne(NpcMemoryCampaignBehavior behavior, SectionResponseParser parser,
                                                  SingleCallBenchScenario scenario, string modelId, float singleCallTemp)
      {
         // (1) SINGLE-CALL: point at the tested model.
         if (!string.IsNullOrWhiteSpace(modelId)) LlmServices.TestModelOverride = modelId.Trim();
         else LlmServices.TestModelOverride = null;
         behavior.LlmServices.Invalidate();

         string singlePrompt = SingleCallExtractionPromptBuilder.Build(scenario.Facts);
         string userTurn = scenario.PlayerMessage + "\n\n" + SingleCallExtractionPromptBuilder.FinalInstruction;
         var singleRequest = new LlmRequest {
            SystemPrompt = singlePrompt,
            Messages = new[] {new LlmMessage(MessageRole.User, userTurn)},
            Parameters = new LlmParameters {MaxTokens = 1200, Creativity = singleCallTemp}
         };

         LlmResponse singleResponse;
         try { singleResponse = Task.Run(() => behavior.LlmServices.LlmClient.CompleteAsync(singleRequest)).GetAwaiter().GetResult(); }
         catch (Exception ex) { return new SingleCallBenchResult(scenario.Id, $"single-call threw: {ex.GetType().Name}: {ex.Message}"); }

         if (singleResponse == null || !singleResponse.IsSuccess)
            return new SingleCallBenchResult(scenario.Id, $"single-call failed: {singleResponse?.ErrorMessage ?? "no result"}");

         ParsedResponse singleParsed = parser.Parse(singleResponse.Content);
         string prose = singleParsed.Dialogue;
         if (string.IsNullOrWhiteSpace(prose))
            return new SingleCallBenchResult(scenario.Id, "single-call produced no [DIALOGUE] to cross-check");

         List<string> selfTypes = singleParsed.Actions?.Select(a => a.Type).ToList() ?? new List<string>();

         // (2) INTERPRETER cross-check on the SAME authored prose: clear the override so the interpreter client resolves
         // its own (configured) model, not the tested single-call one.
         LlmServices.TestModelOverride = null;
         behavior.LlmServices.Invalidate();

         string interpPrompt = ActionInterpreterPromptBuilder.Build(prose, scenario.Facts);
         var interpRequest = new LlmRequest {
            SystemPrompt = interpPrompt,
            Messages = new[] {new LlmMessage(MessageRole.User, ActionInterpreterPromptBuilder.FinalInstruction)},
            Parameters = new LlmParameters {MaxTokens = 900, Creativity = 0.2f}
         };

         LlmResponse interpResponse;
         try { interpResponse = Task.Run(() => behavior.LlmServices.InterpreterClient.CompleteAsync(interpRequest)).GetAwaiter().GetResult(); }
         catch (Exception ex) { return new SingleCallBenchResult(scenario.Id, $"interpreter threw: {ex.GetType().Name}: {ex.Message}"); }

         if (interpResponse == null || !interpResponse.IsSuccess)
            return new SingleCallBenchResult(scenario.Id, $"interpreter failed: {interpResponse?.ErrorMessage ?? "no result"}");

         List<string> interpTypes = parser.Parse(interpResponse.Content).Actions?.Select(a => a.Type).ToList() ?? new List<string>();

         SingleCallAgreementResult agreement = SingleCallAgreement.Compare(selfTypes, interpTypes);

         return new SingleCallBenchResult(scenario.Id, prose.Trim(), selfTypes, interpTypes, agreement);
      }
   }
}
