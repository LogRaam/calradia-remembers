// Code written by Gabriel Mailhot, 24/10/2026.
// cr.cost_bench: what the mod costs to play, measured through the real pipeline and priced from OpenRouter's own bill.
// fearente (Nexus, 24/10/2026) spent $3.19 in a week the page priced at about $1; Gabriel (24/10/2026) asked for a
// script that tests several models and MCM setups and a table in the description naming the version it was measured
// on. Each scenario plays the same ordinary conversation (fearente's own case among its lines: a spouse or friendly
// lord told of two ships bought, then reassured) and writes one letter, with the scenario's models and setup forced for
// the run and restored after. Nothing is saved: the speaker's profile is a copy, the letter is never sent.
// Real-LLM, in-game, spends the player's credit: deliberately NOT part of cr.testall (like cr.rp_bench); the sums and
// tables are covered by CostBenchTests, the client's billing by OpenRouterUsageAccountingTests.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Diagnostics;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using MCM.Common;
using Newtonsoft.Json;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Plays the cost scenarios through the real pipeline and writes the measured table.</summary>
   public static class CostBench
   {
      /// <summary>The ordinary conversation every scenario plays: greeting, fearente's ships and reassurance, news, a request, farewell.</summary>
      internal static readonly string[] Lines = {
         "Well met. I have just ridden in from Diatma.",
         "I bought two dromons there, fine ships. I mean to take them along the coast before the autumn storms.",
         "Do not worry, the crews are good men and the coin was mine to spend.",
         "What news from your lands these days?",
         "I may need your help with the levies this season. Would you stand with me?",
         "Good. I must ride on now. Farewell."
      };

      /// <summary>Dollars a run spends at most when no cap is typed: enough for the eight default scenarios, never a surprise.</summary>
      private const double DefaultCap = 3.0;

      private static readonly string LogFolder = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Logs");

      private static readonly string ScenarioPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Configs", "CalradiaRemembers_cost_bench.txt");

      private static readonly string ReportPath = Path.Combine(LogFolder, "CalradiaRemembers_cost_bench.log");

      /// <summary>cr.cost_bench [plan|run] [cap in dollars]: plan by default, since a run spends credit.</summary>
      public static string Execute(NpcMemoryCampaignBehavior behavior, IList<string> args)
      {
         string mode = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "plan";
         double cap = args != null && args.Count > 1 && double.TryParse(args[1], System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out double c) ? c : DefaultCap;

         IReadOnlyList<CostScenario> scenarios = LoadScenarios(out string fileNote);
         if (scenarios.Count == 0) return $"cr.cost_bench: no scenario to run. {fileNote}";

         if (mode != "run")
            return CostBenchReport.Plan(scenarios, Lines.Length, letter: true, cap) + fileNote
                   + $"\nType 'cr.cost_bench run {cap.ToString(System.Globalization.CultureInfo.InvariantCulture)}' to spend at most that much.";

         return Run(behavior, scenarios, cap) + (string.IsNullOrEmpty(fileNote) ? "" : " " + fileNote);
      }

      private static IReadOnlyList<CostScenario> LoadScenarios(out string note)
      {
         note = "";
         try
         {
            if (!File.Exists(ScenarioPath))
            {
               Directory.CreateDirectory(Path.GetDirectoryName(ScenarioPath)!);
               File.WriteAllText(ScenarioPath, CostScenarioFile.Template());
               note = $"(Scenario file written at {ScenarioPath}: edit it to change the models.)";
            }

            IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(File.ReadAllText(ScenarioPath), out List<string> problems);
            if (problems.Count > 0) note += " Skipped: " + string.Join("; ", problems);

            return rows;
         }
         catch (Exception ex)
         {
            note = $"(Scenario file unreadable, {ex.GetType().Name}: {ex.Message}; the defaults run.)";

            return CostScenarioFile.Defaults;
         }
      }

      private static string Run(NpcMemoryCampaignBehavior behavior, IReadOnlyList<CostScenario> scenarios, double cap)
      {
         LlmServices services = behavior?.LlmServices;
         if (services is not {IsAvailable: true}) return "cr.cost_bench: the LLM is not configured.";
         if (LlmServices.KindForRole(ServiceRole.Prose) != ProviderKind.OpenRouter || LlmServices.KindForRole(ServiceRole.Interpret) != ProviderKind.OpenRouter)
            return "cr.cost_bench: prices come from OpenRouter's own bill, so the PROSE and Interpret roles must both be on OpenRouter.";

         Hero speaker = PickSpeaker();
         if (speaker == null) return "cr.cost_bench: no adult character to talk to.";

         var recorder = new Recorder();
         var results = new List<CostScenarioResult>();
         var transcripts = new StringBuilder();
         string version = ModVersion();
         ILlmUsageObserver priorObserver = services.UsageObserver;
         services.UsageObserver = recorder;

         try
         {
            foreach (CostScenario scenario in scenarios)
            {
               if (CostBenchReport.CapReached(recorder.Spent, cap))
               {
                  results.Add(CostBenchReport.Summarize(scenario, recorder.Calls, $"not run: the ${CostBenchReport.Money(cap)} cap was reached"));
                  continue;
               }

               string note = PlayScenario(behavior, services, scenario, speaker, recorder, transcripts, cap);
               results.Add(CostBenchReport.Summarize(scenario, recorder.Calls, note));
            }
         }
         finally { services.UsageObserver = priorObserver; }

         string report = BuildReport(results, recorder, transcripts, version, speaker);
         try { File.AppendAllText(ReportPath, report); }
         catch (Exception ex) { ModLog.Error($"[COST] report not written: {ex.Message}"); }

         ModLog.Info($"[COST] cr.cost_bench: {results.Count} scenario(s), ${CostBenchReport.Money(recorder.Spent)} spent, report {ReportPath}");

         return $"cr.cost_bench: {results.Count} scenario(s) measured with {speaker.Name}, ${CostBenchReport.Money(recorder.Spent)} spent. Table and transcripts in {ReportPath}";
      }

      private static string PlayScenario(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario scenario, Hero speaker,
                                         Recorder recorder, StringBuilder transcripts, double cap)
      {
         using var forced = new ForcedSetup(services, scenario);
         NpcProfile profile = CopyOf(behavior.Store.Get(speaker.StringId) ?? HeroProfileMapper.FromHero(speaker));
         var session = new ChatSession();
         string note = "";

         transcripts.AppendLine($"--- {scenario.Name}: {scenario.Models}; {scenario.Setup} ---");
         for (var turn = 1; turn <= Lines.Length; turn++)
         {
            if (CostBenchReport.CapReached(recorder.Spent, cap)) { note = "stopped early: cap reached"; break; }

            string line = Lines[turn - 1];
            transcripts.AppendLine($"> {line}");
            string error = PlayTurn(behavior, services, scenario, speaker, profile, session, line, turn, recorder, out string reply);
            transcripts.AppendLine(error == null ? $"< {reply}" : $"! {error}");
            if (error != null) note = $"turn {turn}: {error}";
         }

         if (!CostBenchReport.CapReached(recorder.Spent, cap)) WriteLetter(services, scenario, profile, recorder, transcripts);
         transcripts.AppendLine();

         return note;
      }

      private static string PlayTurn(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario scenario, Hero speaker, NpcProfile profile,
                                     ChatSession session, string line, int turn, Recorder recorder, out string reply)
      {
         reply = "";
         try
         {
            EncounterContext ctx = EncounterContextBuilder.Build(speaker, profile, behavior.GameStateBridge, line);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
            CancellationToken ct = cts.Token;
            bool conversationInProgress = !session.IsEmpty;

            recorder.Begin(scenario.Name, CostPhase.Prose, turn);
            NpcChatResult result = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, line, ctx, ct, !scenario.Enhanced), ct)
                                       .GetAwaiter().GetResult();
            if (result is not {IsSuccess: true}) return result?.ErrorMessage ?? "no reply";

            ParsedResponse response = result.Response ?? new ParsedResponse {Dialogue = ""};
            if (scenario.Enhanced)
            {
               string fallbackModel = services.ResolveProseFallbackModel();
               if (TruncationFallbackPolicy.ShouldFallBack(result.FinishReason, !string.IsNullOrWhiteSpace(fallbackModel), !string.IsNullOrWhiteSpace(response.Dialogue)))
               {
                  recorder.Begin(scenario.Name, CostPhase.Fallback, turn);
                  NpcChatResult fb = ProseFallbackComposer.RunIntegratedFallbackAsync(profile, world, session, line, response.Dialogue, ctx, services, fallbackModel, ct)
                                                          .GetAwaiter().GetResult();
                  if (fb is {IsSuccess: true}) response = fb.Response ?? response;
               }
               else
               {
                  recorder.Begin(scenario.Name, CostPhase.Interpreter, turn);
                  response = ActionInterpreterComposer.InterpretAsync(response, ctx, profile, ActionInterpreterComposer.ResolveDefaultPlayerLabel(),
                                                                      services, ct, conversationInProgress).GetAwaiter().GetResult();
               }
            }

            reply = (response.Dialogue ?? "").Replace("\r", " ").Replace("\n", " ");

            return null;
         }
         catch (Exception ex)
         {
            Exception inner = (ex as AggregateException)?.InnerException ?? ex;

            return $"{inner.GetType().Name}: {inner.Message}";
         }
      }

      private static void WriteLetter(LlmServices services, CostScenario scenario, NpcProfile profile, Recorder recorder, StringBuilder transcripts)
      {
         recorder.Begin(scenario.Name, CostPhase.Letter, 0);
         using var done = new ManualResetEventSlim(false);
         string body = null;
         LetterGenerationService.GenerateForDiagnostic(profile, LetterReason.RealmTidings, "News of the realm worth sharing with a friend.", services,
            (where, content) =>
            {
               body = content;
               done.Set();
            });
         bool written = done.Wait(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
         string text = (body ?? "").Replace("\r", " ").Replace("\n", " ");
         transcripts.AppendLine(written ? $"[letter] {(text.Length > 400 ? text.Substring(0, 400) + "..." : text)}" : "[letter] not written in time");
      }

      private static string BuildReport(List<CostScenarioResult> results, Recorder recorder, StringBuilder transcripts, string version, Hero speaker)
      {
         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine("========================================================================");
         sb.AppendLine($"  cr.cost_bench run - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Calradia Remembers {version}");
         sb.AppendLine($"  Speaker: {speaker.Name} ({speaker.StringId}); {Lines.Length} replies and one letter per scenario.");
         sb.AppendLine($"  Spent: ${CostBenchReport.Money(recorder.Spent)}");
         sb.AppendLine("========================================================================");
         sb.AppendLine();
         sb.AppendLine(CostBenchReport.Markdown(results, version, DateTime.Now));
         sb.AppendLine("For the Nexus description:");
         sb.AppendLine(CostBenchReport.NexusCode(results, version, DateTime.Now));
         sb.AppendLine("Every billed call:");
         foreach (CostCall c in recorder.Calls)
            sb.AppendLine($"  {c.ScenarioName} | turn {c.Turn} | {c.Phase} | {c.Model} | in {c.PromptTokens} (cached {c.CachedTokens}) | out {c.CompletionTokens} (thinking {c.ReasoningTokens}) | {(c.Cost == null ? "unpriced" : "$" + CostBenchReport.Money(c.Cost.Value))}");
         sb.AppendLine();
         sb.AppendLine("Transcripts (read them for tone: fearente's ships are lines 2 and 3):");
         sb.Append(transcripts);

         return sb.ToString();
      }

      // The player's spouse when there is one (fearente's case), else a lord of the player's realm, else any adult lord.
      private static Hero PickSpeaker()
      {
         Hero spouse = Hero.MainHero?.Spouse;
         if (spouse != null && HeroPresence.IsInTheWorld(spouse) && !spouse.IsPrisoner) return spouse;

         List<Hero> lords = Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsLord && !h.IsChild && !h.IsPrisoner && HeroPresence.IsInTheWorld(h)).ToList();

         return lords.FirstOrDefault(h => h.MapFaction != null && h.MapFaction == Hero.MainHero?.MapFaction) ?? lords.FirstOrDefault();
      }

      private static NpcProfile CopyOf(NpcProfile profile)
      {
         try { return JsonConvert.DeserializeObject<NpcProfile>(JsonConvert.SerializeObject(profile)) ?? profile; }
         catch { return profile; }
      }

      private static string ModVersion()
      {
         try
         {
            string xml = File.ReadAllText(Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "SubModule.xml"));
            Match m = Regex.Match(xml, "<Version value=\"([^\"]+)\"");

            return m.Success ? m.Groups[1].Value : "(unknown version)";
         }
         catch { return "(unknown version)"; }
      }

      /// <summary>Records every billed attempt under the phase and turn the bench is playing. Calls are sequential.</summary>
      private sealed class Recorder : ILlmUsageObserver
      {
         private readonly object _gate = new object();
         private string _scenario = "";
         private CostPhase _phase;
         private int _turn;

         public List<CostCall> Calls { get; } = new List<CostCall>();
         public double Spent { get; private set; }

         public void Begin(string scenario, CostPhase phase, int turn)
         {
            lock (_gate)
            {
               _scenario = scenario;
               _phase = phase;
               _turn = turn;
            }
         }

         public void OnAttempt(string model, LlmResponse response)
         {
            LlmUsage u = response?.Usage;
            if (u == null) return; // a failed attempt is not billed

            lock (_gate)
            {
               Calls.Add(new CostCall {
                  ScenarioName = _scenario, Phase = _phase, Turn = _turn, Model = model ?? "", PromptTokens = u.PromptTokens,
                  CachedTokens = u.CachedPromptTokens ?? 0, CompletionTokens = u.CompletionTokens, ReasoningTokens = u.ReasoningTokens ?? 0, Cost = u.Cost
               });
               Spent += u.Cost ?? 0;
            }
         }
      }

      /// <summary>The scenario's models and MCM choices, forced for its run and put back after, whatever happens.</summary>
      private sealed class ForcedSetup : IDisposable
      {
         private readonly LlmServices _services;
         private readonly string _priorProseOverride;
         private readonly string _priorInterpreter;
         private readonly bool _priorCompact;
         private readonly int _priorReasoning;
         private readonly Dropdown<string> _reasoning;

         public ForcedSetup(LlmServices services, CostScenario scenario)
         {
            _services = services;
            CrSettings settings = CrSettings.Instance;
            _priorProseOverride = LlmServices.TestModelOverride;
            _priorInterpreter = settings?.ActionInterpreterModel;
            _priorCompact = settings?.LeanPrompt ?? false;
            _reasoning = settings?.ReasoningEffort;
            _priorReasoning = _reasoning?.SelectedIndex ?? -1;

            LlmServices.TestModelOverride = scenario.ProseModel;
            if (settings != null)
            {
               settings.ActionInterpreterModel = scenario.InterpreterModel;
               settings.LeanPrompt = scenario.Compact;
               if (_reasoning != null && !string.IsNullOrWhiteSpace(scenario.Reasoning)
                   && _reasoning.Any(v => string.Equals(v, scenario.Reasoning, StringComparison.OrdinalIgnoreCase)))
                  _reasoning.SelectedValue = _reasoning.First(v => string.Equals(v, scenario.Reasoning, StringComparison.OrdinalIgnoreCase));
            }

            services.Invalidate();
         }

         public void Dispose()
         {
            LlmServices.TestModelOverride = _priorProseOverride;
            CrSettings settings = CrSettings.Instance;
            if (settings != null)
            {
               settings.ActionInterpreterModel = _priorInterpreter;
               settings.LeanPrompt = _priorCompact;
               if (_reasoning != null && _priorReasoning >= 0) _reasoning.SelectedIndex = _priorReasoning;
            }

            _services.Invalidate();
         }
      }
   }
}
