// Code written by Gabriel Mailhot, 17/08/2026.
// The in-game face of the interpreter extraction bench (cr.action_bench). Runs every ActionBenchCatalog case
// (hand-written prose paired with the action it should or must not produce) through the user's REAL configured
// interpreter client, scores each with the pure ActionBenchScorer, and writes the scoreboard to
// CalradiaRemembers_action_bench.log. It is a refinement tool, not a gate: a miss or false positive points at the
// tell or anti-pattern to tighten in the shared GameActionCatalog. Real-LLM (spends tokens), so deliberately NOT
// part of cr.testall. Far cheaper than testall: short interpreter-only calls, no full scenes.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.LlmClient;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Runs the interpreter extraction bench in-game against the configured interpreter model.</summary>
   public static class ActionBench
   {
      private static readonly string ReportPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_action_bench.log");

      /// <summary>
      ///   Runs the whole corpus through the interpreter and returns a one-line summary pointing at the full
      ///   scoreboard log. The scored comparison is the SDK's pure code; this only supplies the live client and
      ///   writes the result where the user can read and diff it across interpreter models.
      /// </summary>
      public static string Run(NpcMemoryCampaignBehavior behavior, int passes = 1, bool focusOnly = false)
      {
         if (behavior == null) return "Calradia Remembers behavior not active. Start a campaign first.";
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key and model in MCM first.";

         if (passes < 1) passes = 1;

         ILlmClient interpreter = behavior.LlmServices.InterpreterClient;
         string model = behavior.LlmServices.ResolveInterpreterModel() ?? "unknown";

         // Focus = the cheap iteration slice (curated failing/flaky cases + canaries); full = the whole corpus,
         // the pre-release gate. Focus cuts the token bill ~85% but is blind to the cases it omits.
         IReadOnlyList<ActionBenchCase> cases = focusOnly ? ActionBenchCatalog.Focus : ActionBenchCatalog.All;

         ActionBenchReport report;
         try
         {
            report = Task.Run(() => ActionBenchRunner.RunAsync(interpreter, cases, passes)).GetAwaiter().GetResult();
         }
         catch (Exception ex)
         {
            return $"cr.action_bench failed: {ex.GetType().Name}: {ex.Message}";
         }

         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine("========================================================================");
         sb.AppendLine($"  cr.action_bench run - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{(focusOnly ? "  [FOCUS SLICE - not the full corpus]" : "")}");
         sb.AppendLine($"  Interpreter model: {model}");
         sb.AppendLine($"  Cases: {cases.Count}, passes each: {passes} (static prose read by the interpreter, then scored)");
         sb.AppendLine("========================================================================");
         sb.Append(report.Format());

         try { File.AppendAllText(ReportPath, sb.ToString()); }
         catch (Exception ex) { ModLog.Error($"[ACTION-BENCH] could not write the report log: {ex.GetType().Name}: {ex.Message}"); }

         return $"cr.action_bench: {report.CasesPassed}/{report.TotalCases} passed "
              + $"({report.CasesSolid} solid 100%, {report.CasesFlaky} flaky), "
              + $"{report.TotalCases - report.CasesPassed} failing. Full scoreboard -> {ReportPath}";
      }
   }
}
