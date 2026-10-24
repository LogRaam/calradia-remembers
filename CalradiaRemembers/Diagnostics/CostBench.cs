// Code written by Gabriel Mailhot, 24/10/2026.
// cr.cost_bench: what the mod costs to play, measured through the real pipeline and priced from OpenRouter's own bill.
// fearente (Nexus, 24/10/2026) spent $3.19 in a week the page priced at about $1; Gabriel (24/10/2026) asked for a
// script that tests several models and MCM setups and a table in the description naming the version, then for the
// PROSE and INTERPRET roles measured apart, by the length of the conversation, with quality beside cost.
// Four passes (Logic/Diagnostics/CostBenchReport holds the file, the windows, the signals and the tables):
//   PROSE      each model plays the same twenty-line conversation in Enhanced with no interpreter, the conversation
//              folded exactly as the chat folds it (ConversationCompressionPolicy, ConversationHistoryCompressor on the
//              Background client), then writes one letter. Fearente's own case is lines 2 and 3.
//   STANDARD   the same conversation in one call that writes and tags.
//   INTERPRET  each model reads the same scripted replies (cr.action_bench's catalog, by difficulty, plus ordinary
//              replies in which nothing is done) and is scored against what each reply really did.
//   FOLD       each Background model folds the same recap once.
// Nothing is saved: the speaker's profile is a copy, the letter is never sent, every forced setting is restored.
// Real-LLM, in-game, spends the player's credit: deliberately NOT part of cr.testall (like cr.rp_bench).

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
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Diagnostics;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using MCM.Common;
using Newtonsoft.Json;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.Compression;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Plays the cost passes through the real pipeline and writes the measured tables.</summary>
   public static class CostBench
   {
      /// <summary>
      ///   The ordinary conversation every prose and standard row plays: twenty lines, so the chat folds it twice (before
      ///   turns 9 and 15 at the full prompt). Lines 2 and 3 are fearente's ships and his reassurance; line 18 asks for
      ///   something agreed before the first fold, so a long conversation's recap is put to the test.
      /// </summary>
      internal static readonly string[] Lines = {
         "Well met. I have just ridden in from Diatma.",
         "I bought two dromons there, fine ships. I mean to take them along the coast before the autumn storms.",
         "Do not worry, the crews are good men and the coin was mine to spend.",
         "What news from your lands these days?",
         "And how fares your family?",
         "I passed a burned village on the road. Do you know who did it?",
         "The roads are full of deserters. Have your men had trouble with them?",
         "I may need your help with the levies this season. Would you stand with me?",
         "What would you ask in return?",
         "That seems fair. Tell me, what did you make of our liege's last council?",
         "Some say the war in the east will not last the winter. Do you believe it?",
         "If it comes to battle, where would you want to stand?",
         "I have a mind to hold a feast when the campaign is done. Would you come?",
         "Who else should I invite, in your view?",
         "Is there anyone at court you would rather not sit beside?",
         "I will keep that in mind. Have you heard any word of the caravans from the south?",
         "Grain prices are climbing in the towns. Should I worry?",
         "Remind me, what did we agree about the levies earlier?",
         "Good. Then I will send word when the ships are ready.",
         "Farewell, and keep well until we meet again."
      };

      /// <summary>Readings per difficulty for each interpret row.</summary>
      private const int CasesPerDifficulty = 6;

      /// <summary>Dollars a run spends at most when no cap is typed: enough for the default rows, never a surprise.</summary>
      private const double DefaultCap = 5.0;

      // Ordinary replies in which nothing is done: the interpreter must emit no deed (a change of regard is allowed).
      private const string OrdinaryContext = "The player is speaking with a lord of the realm in his hall. No bargain, gift or request is pending.";

      private static readonly string[] OrdinaryReplies = {
         "*He nods slowly and pours two cups of wine.* Diatma, you say? A long ride. Sit, and tell me how the roads were.",
         "The harvest was thin this year, but the granaries will hold till spring. My steward frets more than he needs to.",
         "*She smiles despite herself.* My sons are well, thank you. The youngest has taken to the sword like his father.",
         "I have heard nothing of that village. Bandits, most likely, or deserters. I will ask my men to keep their eyes open.",
         "A feast? Perhaps. Send word when the campaign is done and I will see what my duties allow.",
         "The caravans from the south have been late all season. Some blame the weather, some blame the raiders."
      };

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

         IReadOnlyList<CostScenario> rows = LoadScenarios(out string fileNote);
         if (rows.Count == 0) return $"cr.cost_bench: no row to run. {fileNote}";

         if (mode != "run")
            return CostBenchReport.Plan(rows, Lines.Length, CasesPerDifficulty, cap) + fileNote
                   + $"\nType 'cr.cost_bench run {cap.ToString(System.Globalization.CultureInfo.InvariantCulture)}' to spend at most that much.";

         return Run(behavior, rows, cap) + (string.IsNullOrEmpty(fileNote) ? "" : " " + fileNote);
      }

      private static IReadOnlyList<CostScenario> LoadScenarios(out string note)
      {
         note = "";
         try
         {
            // A file in the older one-row-per-combination format (no [section]) is set aside and the new one written.
            if (File.Exists(ScenarioPath) && !File.ReadAllText(ScenarioPath).Contains("[prose]"))
            {
               File.Copy(ScenarioPath, ScenarioPath + ".old", true);
               File.Delete(ScenarioPath);
               note = "(Your older scenario file was kept as .old; the new format is in sections.)";
            }

            if (!File.Exists(ScenarioPath))
            {
               Directory.CreateDirectory(Path.GetDirectoryName(ScenarioPath)!);
               File.WriteAllText(ScenarioPath, CostScenarioFile.Template());
               note += $" (Scenario file written at {ScenarioPath}: edit it to change the models.)";
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

      private static string Run(NpcMemoryCampaignBehavior behavior, IReadOnlyList<CostScenario> rows, double cap)
      {
         LlmServices services = behavior?.LlmServices;
         if (services is not {IsAvailable: true}) return "cr.cost_bench: the LLM is not configured.";
         if (LlmServices.KindForRole(ServiceRole.Prose) != ProviderKind.OpenRouter || LlmServices.KindForRole(ServiceRole.Interpret) != ProviderKind.OpenRouter
             || LlmServices.KindForRole(ServiceRole.Housekeeping) != ProviderKind.OpenRouter)
            return "cr.cost_bench: prices come from OpenRouter's own bill, so the PROSE, Interpret and Background roles must all be on OpenRouter.";

         Hero speaker = PickSpeaker();
         if (speaker == null) return "cr.cost_bench: no adult character to talk to.";

         var recorder = new Recorder();
         var prose = new List<ProseRowResult>();
         var interpret = new List<InterpretRowResult>();
         var folds = new List<FoldRowResult>();
         var outcomes = new List<InterpretOutcome>();
         var transcripts = new StringBuilder();
         ConversationTranscript reference = null;
         string version = ModVersion();
         ILlmUsageObserver priorObserver = services.UsageObserver;
         services.UsageObserver = recorder;

         try
         {
            foreach (CostScenario row in rows.Where(r => r.Pass == CostPass.Prose || r.Pass == CostPass.Standard))
            {
               if (CostBenchReport.CapReached(recorder.Spent, cap))
               {
                  prose.Add(CostBenchReport.SummarizeProse(row, recorder.Calls, null, $"not run: the ${CostBenchReport.Money(cap)} cap was reached"));
                  continue;
               }

               string note = PlayConversation(behavior, services, row, speaker, recorder, transcripts, cap, out CostScenario played,
                  out List<string> replies, out ConversationTranscript transcript);
               reference ??= transcript;
               prose.Add(CostBenchReport.SummarizeProse(played, recorder.Calls, replies, note));
            }

            foreach (CostScenario row in rows.Where(r => r.Pass == CostPass.Interpret))
            {
               string note = CostBenchReport.CapReached(recorder.Spent, cap)
                  ? $"not run: the ${CostBenchReport.Money(cap)} cap was reached"
                  : ReadCases(services, row, recorder, outcomes, cap);
               interpret.Add(CostBenchReport.SummarizeInterpret(row, recorder.Calls, outcomes, note));
            }

            foreach (CostScenario row in rows.Where(r => r.Pass == CostPass.Fold))
               folds.Add(CostBenchReport.CapReached(recorder.Spent, cap)
                  ? new FoldRowResult {Row = row, Note = "not run: cap reached"}
                  : FoldReference(services, row, reference, recorder));
         }
         finally { services.UsageObserver = priorObserver; }

         string report = BuildReport(prose, interpret, folds, recorder, transcripts, version, speaker);
         try { File.AppendAllText(ReportPath, report); }
         catch (Exception ex) { ModLog.Error($"[COST] report not written: {ex.Message}"); }

         ModLog.Info($"[COST] cr.cost_bench: {rows.Count} row(s), ${CostBenchReport.Money(recorder.Spent)} spent, report {ReportPath}");

         return $"cr.cost_bench: {rows.Count} row(s) measured with {speaker.Name}, ${CostBenchReport.Money(recorder.Spent)} spent. Tables and transcripts in {ReportPath}";
      }

      // ── PROSE and STANDARD ─────────────────────────────────────────────

      private static string PlayConversation(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario row, Hero speaker, Recorder recorder,
                                             StringBuilder transcripts, double cap, out CostScenario played, out List<string> replies,
                                             out ConversationTranscript transcript)
      {
         bool enhanced = row.Pass == CostPass.Prose;
         using var forced = new ForcedSetup(services, row, enhanced);
         // The row always names the reasoning effort and fallback it was measured with: for a thinking model it is the price.
         played = new CostScenario {
            Name = row.Name, Pass = row.Pass, Model = row.Model, Compact = row.Compact, Reasoning = forced.Reasoning,
            FallbackModel = enhanced ? forced.FallbackModel : ""
         };

         NpcProfile profile = CopyOf(behavior.Store.Get(speaker.StringId) ?? HeroProfileMapper.FromHero(speaker));
         string playerName = Hero.MainHero?.Name?.ToString() ?? "You";
         transcript = new ConversationTranscript();
         replies = new List<string>();
         string note = "";

         transcripts.AppendLine($"--- [{row.Pass.ToString().ToLowerInvariant()}] {played.Name}: {played.Model}; {played.Setup} ---");
         for (var turn = 1; turn <= Lines.Length; turn++)
         {
            if (CostBenchReport.CapReached(recorder.Spent, cap)) { note = $"stopped at turn {turn}: cap reached"; break; }

            // The chat folds before it builds a turn (ChatViewModel.EnsureTranscriptCompressedAsync); so does the bench.
            ConversationFoldPlan plan = ConversationCompressionPolicy.Plan(new ConversationCompressionPolicy.TranscriptFold {
               TotalLines = transcript.Count, SummarizedThrough = transcript.SummarizedThrough, Threshold = ConversationCompressionPolicy.ThresholdFor(row.Compact)
            });
            if (plan.ShouldFold)
            {
               recorder.Begin(played, CostPhase.Fold, turn);
               string updated = Fold(services, transcript.RunningSummary, transcript.RenderRange(plan.FoldFrom, plan.FoldTo, playerName), recorder);
               if (!string.IsNullOrWhiteSpace(updated)) transcript.ApplySummary(updated, plan.FoldTo);
               transcripts.AppendLine($"[fold before turn {turn}: {(string.IsNullOrWhiteSpace(updated) ? "failed" : "lines " + plan.FoldFrom + "-" + plan.FoldTo)}]");
            }

            string line = Lines[turn - 1];
            transcripts.AppendLine($"> {line}");
            string error = PlayTurn(behavior, services, played, enhanced, speaker, profile, SessionFrom(transcript, speaker.StringId), line, turn, recorder,
               out string reply);
            replies.Add(reply);
            transcripts.AppendLine(error == null ? $"< {reply}" : $"! {error}");
            if (error != null) note = $"turn {turn}: {error}";

            transcript.Record(new TranscriptLine {IsPlayer = true, SpeakerName = playerName, Text = line});
            transcript.Record(new TranscriptLine {IsPlayer = false, SpeakerName = speaker.Name?.ToString(), SpeakerHeroId = speaker.StringId, Text = reply});
         }

         if (!CostBenchReport.CapReached(recorder.Spent, cap)) WriteLetter(services, played, profile, recorder, transcripts);
         transcripts.AppendLine();

         return note;
      }

      // The chat's own replay of the transcript onto a session (ChatViewModel.BuildSessionFor).
      private static ChatSession SessionFrom(ConversationTranscript transcript, string speakerId)
      {
         var session = new ChatSession();
         foreach (SessionTurn turn in transcript.ComposeSessionFor(speakerId))
            switch (turn.Role)
            {
               case SessionTurnRole.Player:
                  session.AddPlayerMessage(turn.Text);

                  break;
               case SessionTurnRole.Witness when turn.SpeakerName == ConversationTranscript.SummaryLabel:
                  session.AddRunningSummary(turn.SpeakerName, turn.Text);

                  break;
               case SessionTurnRole.Witness:
                  session.AddWitnessStatement(turn.SpeakerName, turn.Text);

                  break;
               default:
                  session.AddNpcMessage(turn.Text);

                  break;
            }

         return session;
      }

      private static string PlayTurn(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario row, bool enhanced, Hero speaker,
                                     NpcProfile profile, ChatSession session, string line, int turn, Recorder recorder, out string reply)
      {
         reply = "";
         try
         {
            EncounterContext ctx = EncounterContextBuilder.Build(speaker, profile, behavior.GameStateBridge, line);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
            CancellationToken ct = cts.Token;

            // Enhanced: the prose prompt without the tag teaching, exactly what the chat sends before the interpreter reads it.
            recorder.Begin(row, CostPhase.Prose, turn);
            NpcChatResult result = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, line, ctx, ct, !enhanced), ct)
                                       .GetAwaiter().GetResult();
            if (result is not {IsSuccess: true}) return result?.ErrorMessage ?? "no reply";

            ParsedResponse response = result.Response ?? new ParsedResponse {Dialogue = ""};
            if (enhanced)
            {
               string fallbackModel = services.ResolveProseFallbackModel();
               if (TruncationFallbackPolicy.ShouldFallBack(result.FinishReason, !string.IsNullOrWhiteSpace(fallbackModel), !string.IsNullOrWhiteSpace(response.Dialogue)))
               {
                  recorder.Begin(row, CostPhase.Fallback, turn);
                  NpcChatResult fb = ProseFallbackComposer.RunIntegratedFallbackAsync(profile, world, session, line, response.Dialogue, ctx, services, fallbackModel, ct)
                                                          .GetAwaiter().GetResult();
                  if (fb is {IsSuccess: true}) response = fb.Response ?? response;
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

      // One fold on the Background client (the chat's ConversationHistoryCompressor), its bill recorded: the background
      // client carries no observer in play, so it is lent one for this call only.
      private static string Fold(LlmServices services, string existing, string newLines, Recorder recorder)
      {
         ILlmClient client = services.HousekeepingClient;
         var open = client as OpenRouterClient;
         ILlmUsageObserver prior = open?.UsageObserver;
         if (open != null) open.UsageObserver = recorder;
         try
         {
            var compressor = new ConversationHistoryCompressor(client, ModSettings.ReplyLanguage);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(60, ModSettings.LlmResponseTimeoutSeconds)));

            return Task.Run(() => compressor.CompressAsync(existing, newLines, cts.Token)).GetAwaiter().GetResult();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COST] fold failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
         finally
         {
            if (open != null) open.UsageObserver = prior;
         }
      }

      private static void WriteLetter(LlmServices services, CostScenario row, NpcProfile profile, Recorder recorder, StringBuilder transcripts)
      {
         recorder.Begin(row, CostPhase.Letter, 0);
         using var done = new ManualResetEventSlim(false);
         string body = null;
         // Started with no synchronization context, so none of the letter's awaits can wait for this thread, which blocks
         // below until it is written.
         SynchronizationContext priorContext = SynchronizationContext.Current;
         SynchronizationContext.SetSynchronizationContext(null);
         try
         {
            LetterGenerationService.GenerateForDiagnostic(profile, LetterReason.RealmTidings, "News of the realm worth sharing with a friend.", services,
               (where, content) =>
               {
                  body = content;
                  done.Set();
               });
         }
         finally { SynchronizationContext.SetSynchronizationContext(priorContext); }

         bool written = done.Wait(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
         string text = (body ?? "").Replace("\r", " ").Replace("\n", " ");
         transcripts.AppendLine(written ? $"[letter] {(text.Length > 400 ? text.Substring(0, 400) + "..." : text)}" : "[letter] not written in time");
      }

      // ── INTERPRET ─────────────────────────────────────────────────────

      private static string ReadCases(LlmServices services, CostScenario row, Recorder recorder, List<InterpretOutcome> outcomes, double cap)
      {
         using var forced = new ForcedInterpreter(row.Model);
         ILlmClient client = services.InterpreterClient;
         var cases = new List<(InterpretDifficulty Difficulty, ActionBenchCase Case)>();
         cases.AddRange(OrdinaryReplies.Take(CasesPerDifficulty).Select((p, i) =>
            (InterpretDifficulty.NoAction, ActionBenchCase.ExpectNone($"ordinary_{i + 1}", "none", OrdinaryContext, p, "none"))));
         foreach (InterpretDifficulty d in new[] {InterpretDifficulty.Simple, InterpretDifficulty.Oblique, InterpretDifficulty.FalseLead})
            cases.AddRange(InterpretCases.Pick(ActionBenchCatalog.All, d, CasesPerDifficulty).Select(c => (d, c)));

         foreach ((InterpretDifficulty difficulty, ActionBenchCase test) in cases)
         {
            if (CostBenchReport.CapReached(recorder.Spent, cap)) return "stopped early: cap reached";

            recorder.Begin(row, CostPhase.Interpreter, 0, difficulty);
            try
            {
               ActionBenchReport report = Task.Run(() => ActionBenchRunner.RunAsync(client, new[] {test})).GetAwaiter().GetResult();
               ActionBenchResult r = report.Results.FirstOrDefault();
               if (r == null || !r.CallSucceeded)
               {
                  outcomes.Add(new InterpretOutcome {ScenarioName = row.Name, Difficulty = difficulty});
                  continue;
               }

               bool right, invented;
               if (difficulty == InterpretDifficulty.NoAction)
               {
                  right = InterpretCases.NoActionReadRight((r.Emitted ?? new List<GameAction>()).Select(a => a.Type));
                  invented = !right;
               }
               else (right, invented) = InterpretCases.Read(r.Verdict);

               outcomes.Add(new InterpretOutcome {ScenarioName = row.Name, Difficulty = difficulty, Right = right, Invented = invented});
            }
            catch (Exception ex)
            {
               outcomes.Add(new InterpretOutcome {ScenarioName = row.Name, Difficulty = difficulty});
               ModLog.Error($"[COST] interpret case {test.Id} failed: {ex.GetType().Name}: {ex.Message}");
            }
         }

         return "";
      }

      // ── FOLD ──────────────────────────────────────────────────────────

      private static FoldRowResult FoldReference(LlmServices services, CostScenario row, ConversationTranscript reference, Recorder recorder)
      {
         if (reference == null || reference.Count < 6) return new FoldRowResult {Row = row, Note = "no prose conversation to fold"};

         using var forced = new ForcedBackground(row.Model);
         recorder.Begin(row, CostPhase.Fold, 0);
         double before = recorder.Spent;
         string playerName = Hero.MainHero?.Name?.ToString() ?? "You";
         // The same recap for every Background model: the first fourteen lines of the first prose conversation.
         string updated = Fold(services, "", reference.RenderRange(0, Math.Min(14, reference.Count), playerName), recorder);

         return new FoldRowResult {Row = row, Cost = string.IsNullOrWhiteSpace(updated) ? (double?) null : recorder.Spent - before};
      }

      // ── Report ────────────────────────────────────────────────────────

      private static string BuildReport(List<ProseRowResult> prose, List<InterpretRowResult> interpret, List<FoldRowResult> folds, Recorder recorder,
                                        StringBuilder transcripts, string version, Hero speaker)
      {
         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine("========================================================================");
         sb.AppendLine($"  cr.cost_bench run - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Calradia Remembers {version}");
         sb.AppendLine($"  Speaker: {speaker.Name} ({speaker.StringId}); {Lines.Length} lines per conversation, one letter per prose row.");
         sb.AppendLine($"  Spent: ${CostBenchReport.Money(recorder.Spent)}");
         sb.AppendLine("========================================================================");
         sb.AppendLine();
         sb.AppendLine(CostBenchReport.Markdown(prose, interpret, folds, version, DateTime.Now));
         sb.AppendLine("For the Nexus description:");
         sb.AppendLine(CostBenchReport.NexusCode(prose, interpret, version, DateTime.Now));
         sb.AppendLine("Every billed call:");
         foreach (CostCall c in recorder.Calls)
            sb.AppendLine($"  [{c.Pass}] {c.ScenarioName} | turn {c.Turn} | {c.Phase}{(c.Phase == CostPhase.Interpreter ? " " + c.Difficulty : "")} | {c.Model} | in {c.PromptTokens} (cached {c.CachedTokens}) | out {c.CompletionTokens} (thinking {c.ReasoningTokens}) | {(c.Cost == null ? "unpriced" : "$" + CostBenchReport.Money(c.Cost.Value))}");
         sb.AppendLine();
         sb.AppendLine("Transcripts (read them: fearente's ships are lines 2 and 3, the recap is tested at line 18):");
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

      /// <summary>Records every billed attempt under the row, phase and turn the bench is playing. Calls are sequential.</summary>
      private sealed class Recorder : ILlmUsageObserver
      {
         private readonly object _gate = new object();
         private CostScenario _row = new CostScenario();
         private CostPhase _phase;
         private int _turn;
         private InterpretDifficulty _difficulty;

         public List<CostCall> Calls { get; } = new List<CostCall>();
         public double Spent { get; private set; }

         public void Begin(CostScenario row, CostPhase phase, int turn, InterpretDifficulty difficulty = InterpretDifficulty.NoAction)
         {
            lock (_gate)
            {
               _row = row;
               _phase = phase;
               _turn = turn;
               _difficulty = difficulty;
            }
         }

         public void OnAttempt(string model, LlmResponse response)
         {
            LlmUsage u = response?.Usage;
            if (u == null) return; // a failed attempt is not billed

            lock (_gate)
            {
               Calls.Add(new CostCall {
                  ScenarioName = _row.Name, Pass = _row.Pass, Phase = _phase, Turn = _turn, Difficulty = _difficulty, Model = model ?? "",
                  PromptTokens = u.PromptTokens, CachedTokens = u.CachedPromptTokens ?? 0, CompletionTokens = u.CompletionTokens,
                  ReasoningTokens = u.ReasoningTokens ?? 0, Cost = u.Cost
               });
               Spent += u.Cost ?? 0;
            }
         }
      }

      /// <summary>A prose or standard row's model and MCM choices, forced for its run and put back after, whatever happens.</summary>
      private sealed class ForcedSetup : IDisposable
      {
         private readonly LlmServices _services;
         private readonly string _priorProseOverride;
         private readonly bool _priorCompact;
         private readonly int _priorReasoning;
         private readonly Dropdown<string> _reasoning;
         private readonly ManualModelMode _priorMode;
         private readonly string _priorNsfw;

         public ForcedSetup(LlmServices services, CostScenario row, bool enhanced)
         {
            _services = services;
            CrSettings settings = CrSettings.Instance;
            _priorProseOverride = LlmServices.TestModelOverride;
            _priorCompact = settings?.LeanPrompt ?? false;
            _reasoning = settings?.ReasoningEffort;
            _priorReasoning = _reasoning?.SelectedIndex ?? -1;
            _priorMode = LlmServices.ActiveModelMode;
            _priorNsfw = settings?.CncModel;

            // The in-chat switch decides the composition and the main role everywhere else: Enhanced or Roleplay (Standard),
            // never the player's NSFW state.
            LlmServices.ActiveModelMode = enhanced ? ManualModelMode.Enhanced : ManualModelMode.Roleplay;
            LlmServices.TestModelOverride = row.Model;
            if (settings != null)
            {
               settings.LeanPrompt = row.Compact;
               if (!string.IsNullOrWhiteSpace(row.FallbackModel)) settings.CncModel = row.FallbackModel;
               if (_reasoning != null && !string.IsNullOrWhiteSpace(row.Reasoning)
                   && _reasoning.Any(v => string.Equals(v, row.Reasoning, StringComparison.OrdinalIgnoreCase)))
                  _reasoning.SelectedValue = _reasoning.First(v => string.Equals(v, row.Reasoning, StringComparison.OrdinalIgnoreCase));
            }

            services.Invalidate();
         }

         /// <summary>The PROSE reasoning effort this run is measured at.</summary>
         public string Reasoning => _reasoning?.SelectedValue ?? "";

         /// <summary>The NSFW (fallback) model this run is measured with.</summary>
         public string FallbackModel => CrSettings.Instance?.CncModel ?? "";

         public void Dispose()
         {
            LlmServices.ActiveModelMode = _priorMode;
            LlmServices.TestModelOverride = _priorProseOverride;
            CrSettings settings = CrSettings.Instance;
            if (settings != null)
            {
               settings.LeanPrompt = _priorCompact;
               settings.CncModel = _priorNsfw;
               if (_reasoning != null && _priorReasoning >= 0) _reasoning.SelectedIndex = _priorReasoning;
            }

            _services.Invalidate();
         }
      }

      /// <summary>An interpret row's model, forced for its readings and put back after.</summary>
      private sealed class ForcedInterpreter : IDisposable
      {
         private readonly string _prior;

         public ForcedInterpreter(string model)
         {
            _prior = CrSettings.Instance?.ActionInterpreterModel;
            if (CrSettings.Instance != null) CrSettings.Instance.ActionInterpreterModel = model;
         }

         public void Dispose()
         {
            if (CrSettings.Instance != null) CrSettings.Instance.ActionInterpreterModel = _prior;
         }
      }

      /// <summary>A fold row's Background model, forced for its fold and put back after.</summary>
      private sealed class ForcedBackground : IDisposable
      {
         private readonly string _prior;

         public ForcedBackground(string model)
         {
            _prior = CrSettings.Instance?.HousekeepingModel;
            if (CrSettings.Instance != null) CrSettings.Instance.HousekeepingModel = model;
         }

         public void Dispose()
         {
            if (CrSettings.Instance != null) CrSettings.Instance.HousekeepingModel = _prior;
         }
      }
   }
}
