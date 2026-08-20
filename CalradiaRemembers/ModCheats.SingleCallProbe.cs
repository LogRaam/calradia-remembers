// Code written by Gabriel Mailhot, 19/08/2026.
// SPIKE probe for the single-call experiment (increment 1). For a handful of hand-built scenarios, it sends ONE call
// that asks the chosen model to write the NPC's reply AND ground+emit the tags together (SingleCallExtractionPromptBuilder),
// then logs the prose, the parsed tags, and the RAW response (so the CHECK lines are visible) for Gabriel to eyeball.
// The question it answers: does the CHECK discipline survive when one model, at a prose-friendly temperature, is also
// being creative, and does it ground its OWN prose honestly? Throwaway tool: real-LLM (spends tokens), NOT in cr.testall,
// changes nothing in the live pipeline. If the eyeball looks good, we build the scored single-call bench (increment 3).

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.LlmClient;
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
      ///   cr.single_call_probe [modelId]
      ///   Runs the single-call composer+extractor prompt over a few scripted scenarios through one model (default:
      ///   the currently configured scene model), forcing the model via <see cref="LlmServices.TestModelOverride" />.
      ///   Each scenario makes ONE call: the model writes the NPC reply AND grounds+emits the tags together. Writes
      ///   prose + parsed tags + the RAW response (CHECK lines included) to CalradiaRemembers_single_call_probe.log.
      ///   Use: "cr.single_call_probe deepseek/deepseek-v4-flash-0731". Spike tool, spends tokens, not in cr.testall.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("single_call_probe", "cr")]
      public static string RunSingleCallProbe(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key and model in MCM first.";

         string modelId = args != null && args.Count > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0].Trim()
            : null;

         return SingleCallProbeRunner.Run(behavior, modelId);
      }
   }

   /// <summary>The runner behind cr.single_call_probe: sends each scenario as one composer+extractor call and reports.</summary>
   internal static class SingleCallProbeRunner
   {
      private static readonly string ReportPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_single_call_probe.log");

      /// <summary>One scripted scenario: who is who, what the player just said, and what a faithful reply should tag.</summary>
      private sealed class Scenario
      {
         public string Id;
         public string Facts;
         public string PlayerMessage;
         public string Expected;
      }

      // Six scenarios spanning the difficulty spectrum the bench surfaced: an easy positive, a direction case, two
      // terminal deeds (one of them the cross-model-hard expel), and two withhold/false-positive traps where a
      // faithful reply must tag NOTHING even though the deed is named. The model GENERATES the prose here, so these
      // also test whether it grounds its own words honestly rather than tagging an intention.
      private static readonly Scenario[] Scenarios = {
         new Scenario {
            Id = "give_gold",
            Facts = "PLAYER: Aldric, a rising captain. YOU: Caladog of Fen Company, a Battanian lord who trusts Aldric (regard +30) and has just been escorted safely out of an ambush by him.",
            PlayerMessage = "Think nothing of it, my lord. Any honest sword would have done the same.",
            Expected = "give_gold (the lord rewards the player) - IF the reply actually hands over coin. If it only thanks him, tag nothing."
         },
         new Scenario {
            Id = "take_gold",
            Facts = "PLAYER: Aldric, who owes YOU a 220-denar debt. YOU: Rhobart, a merchant, collecting it now.",
            PlayerMessage = "*I count out two hundred and twenty denars and set the stack in your hand.* There. We are square.",
            Expected = "take_gold, amount 220 (the player pays the NPC; direction is player -> NPC)."
         },
         new Scenario {
            Id = "execute_prisoner",
            Facts = "PLAYER: Aldric, whose OWN party holds the hero Lord Ansen captive. YOU: Osric, Aldric's sworn companion, standing beside him at the block. Hardcore.",
            PlayerMessage = "*I draw my blade and end Ansen's life with my own hand, before he can finish his plea.* It is done.",
            Expected = "execute_prisoner (the PLAYER kills the captive; NOT execute_player). The beat ends, but the deed is recorded, not a bare end_conversation."
         },
         new Scenario {
            Id = "expel_from_clan",
            Facts = "PLAYER: Aldric, YOUR clan leader. YOU: Reinhard, a member of Aldric's clan whom he has just cast out for good.",
            PlayerMessage = "You have shamed this house one time too many, Reinhard. You are no longer of my clan. Go.",
            Expected = "expel_from_clan (the player casts the NPC out; the reply accepts it). The cross-model-hard case: must NOT collapse to a bare end_conversation."
         },
         new Scenario {
            Id = "turn_nemesis_spared_not_recruited",
            Facts = "PLAYER: Aldric, holding his beaten NEMESIS Unqid captive. YOU: Unqid, a proud enemy who will not bend the knee. Aldric has just spared and freed him.",
            PlayerMessage = "Your life is your own again, Unqid. Walk free. But know the door to my service stands open, if you ever tire of the road.",
            Expected = "NOTHING (or at most change_relation). Spared and freed is NOT turn_nemesis unless he actually swears in - and a proud enemy refuses. A false turn_nemesis here is the FP trap."
         },
         new Scenario {
            Id = "swear_oath_asked_but_deferred",
            Facts = "PLAYER: Aldric, asking YOU to swear to fight at his side by winter. YOU: Lord Ansen, cautious, his granaries empty.",
            PlayerMessage = "Swear to me, Ansen. Your sword at my side when the snows come. Say the words.",
            Expected = "NOTHING. If the reply DEFERS ('ask me when the stores are full'), a deferred oath is no oath - withhold swear_oath. If the reply actually swears, then swear_oath is correct."
         }
      };

      /// <summary>
      ///   Forces <paramref name="modelId" /> (or leaves the configured scene model when null), sends each scenario as
      ///   one composer+extractor call at a prose-friendly temperature, and writes a readable per-scenario report.
      ///   Restores the model override in the finally. Returns a one-line pointer to the log.
      /// </summary>
      public static string Run(NpcMemoryCampaignBehavior behavior, string modelId)
      {
         string priorModel = LlmServices.TestModelOverride;
         if (!string.IsNullOrWhiteSpace(modelId))
         {
            LlmServices.TestModelOverride = modelId;
            behavior.LlmServices.Invalidate();
         }

         string usedModel = behavior.LlmServices.ModelId ?? "unknown";
         var parser = new SectionResponseParser();
         var report = new StringBuilder();

         report.AppendLine();
         report.AppendLine("========================================================================");
         report.AppendLine($"  cr.single_call_probe run - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
         report.AppendLine($"  Model: {usedModel}  (ONE call per scenario: prose + CHECK + tags together)");
         report.AppendLine("========================================================================");

         try
         {
            foreach (Scenario s in Scenarios)
               report.Append(RunScenario(behavior, parser, s));
         }
         catch (Exception ex)
         {
            report.AppendLine();
            report.AppendLine($"[PROBE ABORTED] {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            LlmServices.TestModelOverride = priorModel;
            behavior.LlmServices.Invalidate();
         }

         string text = report.ToString();
         try { File.AppendAllText(ReportPath, text); }
         catch (Exception ex) { ModLog.Error($"[SINGLE-CALL-PROBE] could not write the report log: {ex.GetType().Name}: {ex.Message}"); }

         return $"cr.single_call_probe done (model {usedModel}). Prose + CHECK + tags -> {ReportPath}";
      }

      // One scenario: build the single-call prompt from the facts, send the player line + the closing nudge as the
      // user turn at Creativity 0.6 (prose-friendly, NOT the interpreter's 0.2 - this is exactly the temperature the
      // spike is testing the CHECK against), and log the prose, the parsed tags, and the raw response.
      private static string RunScenario(NpcMemoryCampaignBehavior behavior, SectionResponseParser parser, Scenario s)
      {
         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine($"  --- {s.Id} ---");
         sb.AppendLine($"  EXPECTED: {s.Expected}");
         sb.AppendLine($"  PLAYER: {s.PlayerMessage}");

         string systemPrompt = SingleCallExtractionPromptBuilder.Build(s.Facts);
         string userTurn = s.PlayerMessage + "\n\n" + SingleCallExtractionPromptBuilder.FinalInstruction;

         var request = new LlmRequest {
            SystemPrompt = systemPrompt,
            Messages = new[] {new LlmMessage(MessageRole.User, userTurn)},
            Parameters = new LlmParameters {MaxTokens = 1200, Creativity = 0.6f}
         };

         LlmResponse response;
         try
         {
            response = Task.Run(() => behavior.LlmServices.LlmClient.CompleteAsync(request)).GetAwaiter().GetResult();
         }
         catch (Exception ex)
         {
            sb.AppendLine($"  CALL FAILED: {ex.GetType().Name}: {ex.Message}");

            return sb.ToString();
         }

         if (response == null || !response.IsSuccess)
         {
            sb.AppendLine($"  CALL FAILED: {response?.ErrorMessage ?? "no result"}");

            return sb.ToString();
         }

         ParsedResponse parsed = parser.Parse(response.Content);

         sb.AppendLine("  PROSE (what the player would see):");
         sb.AppendLine(Indent(string.IsNullOrWhiteSpace(parsed.Dialogue) ? "(no [DIALOGUE] parsed)" : parsed.Dialogue.Trim()));
         sb.AppendLine($"  TAGS: {DescribeActions(parsed)}");
         sb.AppendLine("  RAW RESPONSE (CHECK lines visible here):");
         sb.AppendLine(Indent(response.Content?.Trim() ?? "(empty)"));

         return sb.ToString();
      }

      private static string DescribeActions(ParsedResponse parsed)
      {
         var parts = new List<string>();
         if (parsed.Actions != null)
            foreach (GameAction a in parsed.Actions)
               parts.Add(a.Type);

         string actions = parts.Count > 0 ? "[" + string.Join(", ", parts) + "]" : "[none]";
         string ev = parsed.NewEventData == null
            ? ""
            : $"  (+EVENT {parsed.NewEventData.Type})";

         return actions + ev;
      }

      private static string Indent(string text)
      {
         var sb = new StringBuilder();
         foreach (string line in text.Split('\n'))
            sb.Append("      ").AppendLine(line.TrimEnd());

         return sb.ToString();
      }
   }
}
