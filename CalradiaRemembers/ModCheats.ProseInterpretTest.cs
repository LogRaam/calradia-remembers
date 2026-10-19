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
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.prose_interpret_test [proseModelId]
      ///   Runs the REAL two-call PROSE + INTERPRETER loop on the scripted scene-1 lord so Gabriel can compare how
      ///   faithfully the MCM-configured ACTION INTERPRETER (e.g. Grok) reads tags out of FRESH prose from different
      ///   PROSE models. Each scripted turn makes two calls: (a) a PROSE call, forced to the argument model id via
      ///   <see cref="LlmServices.TestModelOverride" />, through the same seam the roleplay bench uses; (b) an
      ///   INTERPRET call, with the override cleared so it resolves the MCM model, using
      ///   <see cref="ActionInterpreterPromptBuilder" /> + the same one-shot pattern as cr.interpret_test. Expected
      ///   use: set Grok in MCM, run "cr.prose_interpret_test deepseek/deepseek-v4-pro" then
      ///   "cr.prose_interpret_test z-ai/glm-5.2", and read the log. This changes NOTHING in the live chat pipeline
      ///   and is deliberately NOT part of cr.testall. The optional argument is the prose model id (default
      ///   "z-ai/glm-5.2").
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("prose_interpret_test", "cr")]
      public static string RunProseInterpretTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key and model in MCM first.";

         string proseModelId = args != null && args.Count > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0].Trim()
            : "z-ai/glm-5.2";

         return ProseInterpretTestRunner.Run(behavior, proseModelId);
      }
   }

   /// <summary>
   ///   The runner behind cr.prose_interpret_test. Stages the scripted scene-1 lord (reusing the roleplay bench's
   ///   selection, seeding, and script), drives each turn through the PROSE model then the INTERPRET model, and
   ///   writes a readable per-turn report. Kept out of the thin console-command method so the two-call flow and
   ///   the report formatting read as one piece.
   /// </summary>
   internal static class ProseInterpretTestRunner
   {
      /// <summary>The bench's own log file, written next to the main CalradiaRemembers.log so runs can be diffed across prose models.</summary>
      private static readonly string ReportPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_prose_interpret_test.log");

      /// <summary>
      ///   Runs the scene-1 scenario through the PROSE model then the INTERPRET model, one pair of calls per
      ///   scripted turn, and writes the full report. Forces a male "Aldric" player for the whole run and restores
      ///   the player-name/gender/model overrides (and the seeded profile) in the finally. Returns a one-line
      ///   pointer to the log file.
      /// </summary>
      public static string Run(NpcMemoryCampaignBehavior behavior, string proseModelId)
      {
         Hero hero = RoleplayBench.SelectScene1Hero();

         if (hero == null)
            return "cr.prose_interpret_test: no male lord available to stage the scene-1 scenario. Load a campaign with lords present.";

         string id = hero.StringId;
         NpcProfile original = behavior.Store.Get(id);

         // Force a MALE player named "Aldric" (the name the seeded context facts and the scripted lines assume),
         // matching the roleplay bench, so the staged identity reads coherently. Restored in the finally.
         bool? priorGender = LlmServices.TestPlayerGenderOverride;
         string priorName = LlmServices.TestPlayerNameOverride;
         string priorModel = LlmServices.TestModelOverride;
         LlmServices.TestPlayerGenderOverride = false;
         LlmServices.TestPlayerNameOverride = RoleplayBench.MalePlayerName;

         var parser = new SectionResponseParser();
         var report = new StringBuilder();

         try
         {
            NpcProfile seed = RoleplayBench.BuildScene1Profile(hero);
            behavior.Store.Set(seed);

            // Header display only (turn 1's facts, conversationInProgress:false); RunTurn below rebuilds the facts
            // PER TURN with the real flag (turn index > 1), since the scene does not stand still across turns 2+
            // any more.
            string contextFacts = BuildContextFacts(behavior, hero, seed, false);
            var session = new ChatSession();

            // Name the ACTION INTERPRETER role's OWN model (MCM "Actions Interpreter model", e.g. grok), which the
            // interpret call below resolves through LlmServices.InterpreterClient, NOT the active Roleplay/Prose
            // model. Clear the prose override first so nothing from a prior turn leaks into this header read.
            LlmServices.TestModelOverride = null;
            behavior.LlmServices.Invalidate();
            string interpreterModel = behavior.LlmServices.ResolveInterpreterModel() ?? "unknown";

            report.AppendLine();
            report.AppendLine("========================================================================");
            report.AppendLine($"  cr.prose_interpret_test run - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"  PROSE model: {proseModelId} | INTERPRETER model (MCM): {interpreterModel}");
            report.AppendLine($"  NPC: {HeroLabelSafe(hero)} | seeded regard +12 (scene-1 lord).");
            report.AppendLine("  Per turn: (a) PROSE from the prose model, (b) tags INTERPRETED by the MCM model.");
            report.AppendLine("  The prose model's own [ACTION]/[EVENT] are IGNORED; only the interpreter supplies tags.");
            report.AppendLine("  Live chat pipeline UNCHANGED. Not part of cr.testall.");
            report.AppendLine("========================================================================");
            report.AppendLine();
            report.AppendLine("CONTEXT FACTS, turn 1 (turn 2+ additionally states the conversation is already under way):");
            report.AppendLine(Indent(contextFacts));

            var turn = 0;
            foreach (string line in RoleplayBench.Scene1Lines)
            {
               turn++;
               report.Append(RunTurn(behavior, hero, seed, session, parser, proseModelId, line, turn));
            }
         }
         catch (Exception ex)
         {
            report.AppendLine();
            report.AppendLine($"[BENCH ABORTED] {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            if (original != null) behavior.Store.Set(original);
            else behavior.Store.Remove(id);

            LlmServices.TestPlayerGenderOverride = priorGender;
            LlmServices.TestPlayerNameOverride = priorName;
            LlmServices.TestModelOverride = priorModel;
            behavior.LlmServices.Invalidate();
         }

         string text = report.ToString();
         WriteReportFile(text);
         foreach (string logLine in text.Split('\n'))
            ModLog.Info($"[prose_interpret_test] {logLine.TrimEnd()}");

         return $"cr.prose_interpret_test done (prose model {proseModelId}). Prose-vs-interpreted tags: {ReportPath}";
      }

      #region private

      /// <summary>
      ///   One scripted turn: forces the prose model and drives the turn to get the NPC's prose, then clears the
      ///   override and runs the interpreter over that prose. A failure on either call is recorded and the turn
      ///   still returns, so one bad call never crashes the bench. The shared <paramref name="session" /> carries
      ///   the scene forward across turns. The context facts are rebuilt HERE, per turn (not once for the whole
      ///   run): conversationInProgress is turnNumber &gt; 1, exactly the "opener is false, every later turn is
      ///   true" rule the live chat and cr.rp_bench also follow, so turn 1 alone can legitimately read first_meeting.
      /// </summary>
      private static string RunTurn(NpcMemoryCampaignBehavior behavior, Hero hero, NpcProfile profile,
                                    ChatSession session, SectionResponseParser parser,
                                    string proseModelId, string playerLine, int turnNumber)
      {
         string contextFacts = BuildContextFacts(behavior, hero, profile, turnNumber > 1);

         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine($"  --- Turn {turnNumber} ---");
         sb.AppendLine($"  YOU: {playerLine}");

         // (a) PROSE call: point this single call at the prose model, rebuild the chat service so it takes effect.
         LlmServices.TestModelOverride = proseModelId;
         behavior.LlmServices.Invalidate();
         string prose = DriveProse(behavior, hero, profile, session, playerLine, out string proseError);

         if (proseError != null)
         {
            sb.AppendLine($"  PROSE ({proseModelId}): prose call failed: {proseError}");

            return sb.ToString();
         }

         sb.AppendLine($"  PROSE ({proseModelId}):");
         sb.AppendLine(Indent(string.IsNullOrWhiteSpace(prose) ? "(empty)" : prose.Trim()));

         if (string.IsNullOrWhiteSpace(prose))
         {
            sb.AppendLine("  INTERPRETED: skipped (prose was empty).");

            return sb.ToString();
         }

         // (b) INTERPRET call: route through the dedicated interpreter client (the ACTION INTERPRETER role's own
         // model, e.g. grok), NOT the active prose model. Clear the prose override so it does not leak, then run
         // the one-shot interpretation over the fresh prose.
         LlmServices.TestModelOverride = null;
         behavior.LlmServices.Invalidate();

         string prompt = ActionInterpreterPromptBuilder.Build(prose, contextFacts);
         string raw = CallInterpreter(behavior, prompt, out string interpretError);

         if (interpretError != null)
         {
            sb.AppendLine($"  INTERPRETED: interpret call failed: {interpretError}");

            return sb.ToString();
         }

         ParsedResponse parsed = parser.Parse(raw ?? string.Empty);

         sb.AppendLine("  INTERPRETED (MCM interpreter):");
         if (parsed.Actions == null || parsed.Actions.Count == 0)
            sb.AppendLine("    [ACTION] none");
         else
            foreach (GameAction a in parsed.Actions)
               sb.AppendLine($"    [ACTION] {a.Type}{FormatParams(a)}");

         if (parsed.NewEventData != null)
            sb.AppendLine($"    [EVENT] {parsed.NewEventData.Type}: {parsed.NewEventData.Summary}");
         else
            sb.AppendLine("    [EVENT] none");

         return sb.ToString();
      }

      /// <summary>
      ///   Drives ONE scripted player turn through the true prompt pipeline (EncounterContextBuilder.Build ->
      ///   ChatService.ChatAsync -> parse), the same seam RoleplayBench.AskTurn uses, accumulating the shared
      ///   <paramref name="session" />, and returns the NPC's prose (its [NARRATION] then [DIALOGUE], combined).
      ///   Any throw or timeout is caught and reported via <paramref name="error" />, so one bad turn never
      ///   crashes the bench. The prose model's own emitted [ACTION]/[EVENT] are intentionally ignored here: the
      ///   interpreter supplies the tags.
      /// </summary>
      private static string DriveProse(NpcMemoryCampaignBehavior behavior, Hero npc, NpcProfile profile, ChatSession session, string playerLine, out string error)
      {
         error = null;

         try
         {
            LlmServices services = behavior.LlmServices;

            if (services is not {IsAvailable: true})
            {
               error = "LLM not configured";

               return null;
            }

            EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, playerLine);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};

            int budgetSeconds = Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 5;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(budgetSeconds));
            CancellationToken ct = cts.Token;

            Task<NpcChatResult> task = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, playerLine, ctx, ct), ct);
            task.Wait(ct);
            NpcChatResult result = task.GetAwaiter().GetResult();

            if (result is not {IsSuccess: true})
            {
               error = result?.ErrorMessage ?? "no result";

               return null;
            }

            ParsedResponse r = result.Response ?? new ParsedResponse {Dialogue = ""};

            return CombineProse(r.Dialogue, r.Narration);
         }
         catch (OperationCanceledException)
         {
            error = "timed out";
         }
         catch (AggregateException ex)
         {
            Exception inner = ex.InnerException ?? ex;
            error = $"{inner.GetType().Name}: {inner.Message}";
         }
         catch (Exception ex)
         {
            error = $"{ex.GetType().Name}: {ex.Message}";
         }

         return null;
      }

      /// <summary>
      ///   Calls the configured model one-shot as the interpreter, reusing the same seam cr.interpret_test uses
      ///   (<see cref="LlmServices.LlmClient" />.CompleteAsync), on a background task with a CancellationTokenSource
      ///   budget and error handling mirroring RoleplayBench.AskTurn. Returns the raw content, or null with
      ///   <paramref name="callError" /> set on any failure/timeout.
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

            // The DEDICATED interpreter client: the ACTION INTERPRETER role's own model, base URL, key, and its
            // reasoning-Off pin, exactly what the live chat's ActionInterpreterComposer uses. The main LlmClient
            // would resolve the active Roleplay/Prose model instead (the bug this fixes: the log named GLM, the
            // prose model, as the interpreter when grok was configured).
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

      /// <summary>
      ///   Assembles the SETTING/PLAYER/YOU digest the interpreter receives by building a REAL
      ///   <see cref="EncounterContext" /> for the seeded scene-1 hero (the same <see cref="EncounterContextBuilder.Build" />
      ///   seam <see cref="DriveProse" /> uses each turn) and projecting it through the shared
      ///   <see cref="ActionInterpreterContextBuilder.Project(EncounterContext, NpcProfile, string, bool)" />. Rebuilt
      ///   PER TURN (the staged scene itself does not move, only <paramref name="conversationInProgress" /> does): the
      ///   spike exercises the SAME projector the full two-call mode will use, not a hand-written string. The real
      ///   player label (matching <c>PromptBuilder.PlayerName</c>) is threaded so the interpreter names the player by
      ///   "Aldric", never an epithet.
      /// </summary>
      private static string BuildContextFacts(NpcMemoryCampaignBehavior behavior, Hero hero, NpcProfile seed, bool conversationInProgress)
      {
         EncounterContext context = EncounterContextBuilder.Build(hero, seed, behavior.GameStateBridge);

         return ActionInterpreterContextBuilder.Project(context, seed, ResolvePlayerLabel(), conversationInProgress);
      }

      /// <summary>The prompt-safe player label, resolved exactly as <c>LlmServices</c> feeds <c>PromptBuilder.PlayerName</c> (test override, else the live hero name).</summary>
      private static string ResolvePlayerLabel()
         => string.IsNullOrWhiteSpace(LlmServices.TestPlayerNameOverride)
            ? Hero.MainHero?.Name?.ToString() ?? ""
            : LlmServices.TestPlayerNameOverride;

      /// <summary>Combines the NPC's narration (physical action) then dialogue into one prose block for the interpreter to read.</summary>
      private static string CombineProse(string dialogue, string narration)
      {
         var sb = new StringBuilder();
         if (!string.IsNullOrWhiteSpace(narration)) sb.AppendLine(narration.Trim());
         if (!string.IsNullOrWhiteSpace(dialogue)) sb.Append(dialogue.Trim());

         return sb.ToString().Trim();
      }

      /// <summary>The prompt-safe hero label (name / house / culture), falling back to the bare name if the pure labeler is unavailable.</summary>
      private static string HeroLabelSafe(Hero hero)
      {
         try { return HeroLabel.Describe(new HeroLabel.HeroNaming {Name = hero?.Name?.ToString(), ClanName = hero?.Clan?.Name?.ToString(), CultureName = hero?.Culture?.Name?.ToString()}); }
         catch { return hero?.Name?.ToString() ?? "NPC"; }
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
            ModLog.Error($"cr.prose_interpret_test could not write {ReportPath}: {ex.Message}");
         }
      }

      #endregion
   }
}
