// Code written by Gabriel Mailhot, 24/10/2026.
// cr.cost_bench: what the mod costs to play, measured through the real pipeline and priced from the provider's own bill.
// Gabriel (18/11/2026): any provider, not only OpenRouter (local servers, Player2, NanoGPT). Each row runs on the provider
// its role has in the MCM; a provider that sends no bill is priced from the row's prices or marked free (Logic/Diagnostics/
// CostPricing), and a row with neither is not run.
// fearente (Nexus, 24/10/2026) spent $3.19 in a week the page priced at about $1; Gabriel (24/10/2026) asked for a
// script that tests several models and MCM setups and a table in the description naming the version, then for the
// PROSE and INTERPRET roles measured apart, by the length of the conversation, with quality beside cost.
// Four passes (Logic/Diagnostics/CostBenchReport holds the file, the windows, the signals and the tables):
//   PROSE      each model plays the same twenty-line conversation in Enhanced with no interpreter, the conversation
//              folded exactly as the chat folds it (ConversationCompressionPolicy, ConversationHistoryCompressor on the
//              Background client), then writes one letter. Fearente's own case is lines 2 and 3.
//   STANDARD   the same conversation in one call that writes and tags, then staged deeds (Logic/Diagnostics/
//              StandardActionCases) whose tags are scored like an interpreter's readings (Gabriel, 18/11/2026).
//   INTERPRET  each model reads the same scripted replies (cr.action_bench's catalog, by difficulty, plus ordinary
//              replies in which nothing is done) and is scored against what each reply really did.
//   FOLD       each Background model folds the same recap once.
// Nothing is saved: the speaker's profile is a copy, the letter is never sent, every forced setting is restored.
// Each run also CAPTURES what its prose and standard rows sent, request by request (CalradiaRemembers_cost_capture.json
// beside the report), so the conversation can be replayed outside the game against any model (Logic/Diagnostics/
// CostReplay; Gabriel, 24/10/2026: "ça te donnerait la possibilité de vérifier des choses par toi-même").
// Real-LLM, in-game, spends the player's credit: deliberately NOT part of cr.test all (like cr.rp_bench).

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
using CalradiaRemembers.Logic.Chat;
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
      /// <summary>The opening, played for real: the greeting, then fearente's ships and his reassurance (lines 2 and 3).</summary>
      internal static readonly string[] Opening = {
         "Well met. I have just ridden in from Diatma.",
         "I bought two dromons there, fine ships. I mean to take them along the coast before the autumn storms.",
         "Do not worry, the crews are good men and the coin was mine to spend."
      };

      // The synthetic history (Gabriel, 24/10/2026: three points instead of twenty replies). The same for every model, so
      // each is measured on the same input; replies of an ordinary length for a character in this mod. Gestures in the
      // first person, as the prompt asks of every reply: written without a subject ("*A hard look.*"), DeepSeek V4.1 Flash
      // took them for a narrator and answered "*He snorts*" over them, on those turns only (24/10/2026).
      private static readonly (string Player, string Npc)[] Exchanges = {
         ("What news from your lands these days?",
          "*I set my cup down and weigh the question.* Quiet enough, for once. The harvest came in thin along the river, but the granaries will hold until spring if the stewards stop selling to the caravans. A few raiders crossed the hills last month; my brother's men chased them back. Nothing that would trouble you, I think, though I would not mind a few more spears on the northern road."),
         ("And how fares your family?",
          "Well, thank you for asking. My mother complains of the cold in the hall and my youngest has taken to the sword like he was born holding one. *I smile faintly.* He asked about you, in fact. He wants to know whether the stories about your last battle are true. I told him half of them were, and would not say which half."),
         ("I passed a burned village on the road. Do you know who did it?",
          "*I give you a hard look.* I heard of it. Deserters, most likely, men who left the eastern host with their pay unspent and their swords still sharp. Some say they ride under no banner at all. If you have seen them, tell my captain before you leave; he will want to know which way they went."),
         ("The roads are full of deserters. Have your men had trouble with them?",
          "Twice this season. Once at the ford, once near the mill. We lost a cart of grain and two good men. *I shake my head.* The trouble is not the deserters themselves, it is that nobody pays to hunt them. Every lord waits for another to do it, and the villages pay the price in the meantime."),
         ("I may need your help with the levies this season. Would you stand with me?",
          "*I give you a long, measuring look.* Perhaps. It depends on what you need and what it would cost my people. I cannot strip my villages bare before the winter, not with deserters on the roads. Tell me plainly how many men you want and for how long, and I will tell you plainly what I can give."),
         ("What would you ask in return?",
          "Grain, for one. My villages along the river will go hungry before the thaw if nothing changes. And your word that my men will not be thrown at a wall to spare your own. *I lean back.* Give me that, and I can send you forty spearmen for the levies, no more, and I will want them home before the spring sowing."),
         ("That seems fair. Tell me, what did you make of our liege's last council?",
          "*I laugh, dryly.* I made of it what everyone did: a great deal of shouting and very little decided. The old lords want peace because they are tired, the young ones want war because they are not. Our liege listened to both and promised each of them something. We will see which promise he keeps."),
         ("Some say the war in the east will not last the winter. Do you believe it?",
          "I believe it will last exactly as long as both sides can pay their men. Wars end in the treasury long before they end on the field. *I tap the table.* Watch the price of bread in the eastern towns. When it doubles, someone will start talking about terms."),
         ("If it comes to battle, where would you want to stand?",
          "Wherever my men can see me. I am no great captain, but I learned long ago that men hold a line better when the one who brought them there is standing in it too. Put me on the left, near the river, where I know the ground."),
         ("I have a mind to hold a feast when the campaign is done. Would you come?",
          "*My look softens.* A feast. Now that is a better plan than half of what was said at council. Yes, I would come, if the roads allow it. Send word early; I will need time to find something to wear that is not covered in mud."),
         ("Who else should I invite, in your view?",
          "Invite the ones who fought, first. Then the ones you need for next year. And keep the two lists apart at the table, or you will spend the evening keeping old quarrels from becoming new ones. *I smile.* My brother, of course. He will be offended if you forget him."),
         ("Is there anyone at court you would rather not sit beside?",
          "*I hesitate, then lower my voice.* The old steward of the eastern march. He counts every cup of wine and remembers who drank it. Seat him near the door; he likes to leave early anyway, to tell everyone what he saw."),
         ("I will keep that in mind. Have you heard any word of the caravans from the south?",
          "Late, all of them. Some blame the weather, some blame the raiders, and the merchants blame whoever is listening. One arrived last week with half its mules and a story nobody believed. Prices will climb before they fall, I fear."),
         ("Grain prices are climbing in the towns. Should I worry?",
          "Worry, yes; panic, no. If you hold a town, open a granary before the people ask you to; they remember who fed them in a hard winter longer than who won a battle. *I hold your gaze evenly.* And do not forget what you promised my villages."),
         ("How are your stables? I may need horses before the spring.",
          "Thin, like everything else this year. I could spare a few remounts, not warhorses. The best of the herd went east with my brother. If you need more than a dozen, try the horse fair at the crossroads; the prices are robbery, but the animals are sound."),
         ("Do you trust the new marshal?",
          "*I pause.* I trust that he wants to win. Whether he wants to win for the realm or for himself, I have not decided. He has been generous with promises and careful with his own men. Watch where he places his own banner in the next battle; that will tell you more than any speech.")
      };

      // What the chat's folds would have kept of the first ten exchanges: the recap a long conversation carries.
      private const string Recap =
         "Arriving from Diatma with two newly bought dromons, the player spoke at length with the host, who shared news of a thin harvest, raiders " +
         "in the hills and deserters who burned a village and twice robbed the host's men. Asked to stand with the player for this season's levies, " +
         "the host agreed to send forty spearmen, no more, home before the spring sowing, in return for grain for the host's river villages and the " +
         "player's word that those men would not be spent to spare the player's own. They spoke of the liege's indecisive council, of a war in the east " +
         "that will last as long as the treasuries, and of where the host would stand in battle. The host accepted an invitation to a feast after the " +
         "campaign and advised on the guests, warning against seating the old steward of the eastern march near anyone.";

      /// <summary>
      ///   The middle point: a warm-up line, then the measured one. Never a question the synthetic history already asked: the
      ///   first run reused two, and Gemini answered "You asked me that very question only moments ago" (24/10/2026).
      /// </summary>
      private static readonly string[] MiddleLines = {
         "Tell me about the lands to the north. Is the hunting good this year?",
         "The merchants complain of a new toll on the bridges. What do you make of it?"
      };

      /// <summary>
      ///   The long conversation: a warm-up line no exchange has asked, then the measured one, which asks for what only the
      ///   recap holds.
      /// </summary>
      private static readonly string[] LongLines = {
         "Have you had any word from your brother in the east?",
         "Remind me, what did we agree about the levies earlier?"
      };

      /// <summary>Replies a prose row asks for: the opening, two at the middle, two in the long conversation.</summary>
      private static int RepliesPerRow => Opening.Length + MiddleLines.Length + LongLines.Length;

      /// <summary>Readings per difficulty for each interpret row.</summary>
      private const int CasesPerDifficulty = 6;

      /// <summary>
      ///   Dollars a run spends at most when no cap is typed: enough for the default rows with each interpreter reading the
      ///   whole catalog (about $6 measured on 24/10/2026 prices), never a surprise.
      /// </summary>
      private const double DefaultCap = 10.0;

      private static readonly string LogFolder = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Logs");

      private static readonly string ScenarioPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Configs", "CalradiaRemembers_cost_bench.txt");

      private static readonly string ReportPath = Path.Combine(LogFolder, "CalradiaRemembers_cost_bench.log");

      /// <summary>What the last run's prose and standard rows sent, for a replay outside the game.</summary>
      private static readonly string CapturePath = Path.Combine(LogFolder, "CalradiaRemembers_cost_capture.json");

      // The run's prefix watch, which also captures: set for the length of a run (a console command, one at a time).
      private static PrefixWatch _watch;

      /// <summary>cr.cost_bench [plan|run] [cap in dollars]: plan by default, since a run spends credit.</summary>
      public static string Execute(NpcMemoryCampaignBehavior behavior, IList<string> args)
      {
         string mode = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "plan";
         double cap = args != null && args.Count > 1 && double.TryParse(args[1], System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out double c) ? c : DefaultCap;

         IReadOnlyList<CostScenario> rows = LoadScenarios(out string fileNote);
         if (rows.Count == 0) return $"cr.cost_bench: no row to run. {fileNote}";

         if (mode != "run")
            return CostBenchReport.Plan(rows, RepliesPerRow + 1, CasesPerDifficulty, cap, ProviderOf) + fileNote
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

         Hero speaker = PickSpeaker();
         if (speaker == null) return "cr.cost_bench: no adult character to talk to.";

         var recorder = new CostRecorder();
         var prose = new List<ProseRowResult>();
         var interpret = new List<InterpretRowResult>();
         var folds = new List<FoldRowResult>();
         var outcomes = new List<InterpretOutcome>();
         var tagging = new List<InterpretRowResult>();
         var tagOutcomes = new List<InterpretOutcome>();
         var unscored = new HashSet<CostCall>();
         var transcripts = new StringBuilder();
         ConversationTranscript reference = null;
         string version = ModVersion();
         ILlmUsageObserver priorObserver = services.UsageObserver;
         services.UsageObserver = recorder;
         // Where each prose call's cacheable prefix stops matching the previous one of its row: the first run (24/10/2026)
         // read only 128 cached tokens on most Grok calls although Grok caches a stable prefix reliably (probed the same day).
         var prefixWatch = new PrefixWatch(recorder);
         prefixWatch.Capture.Version = version;
         prefixWatch.Capture.CapturedAtUtc = DateTime.UtcNow.ToString("o");
         prefixWatch.Capture.Speaker = $"{speaker.Name} ({speaker.StringId})";
         ILlmWireObserver priorWire = services.WireObserver;
         services.WireObserver = prefixWatch;
         _watch = prefixWatch;

         try
         {
            foreach (CostScenario row in rows.Where(r => r.Pass == CostPass.Prose || r.Pass == CostPass.Standard))
            {
               if (!Priceable(row))
               {
                  prose.Add(CostBenchReport.SummarizeProse(row, recorder.Calls, null, CostBenchReport.NotPriced(ProviderOf(row.Pass).Provider)));
                  continue;
               }

               if (CostBenchReport.CapReached(recorder.Spent, cap))
               {
                  prose.Add(CostBenchReport.SummarizeProse(row, recorder.Calls, null, $"not run: the ${CostBenchReport.Money(cap)} cap was reached"));
                  continue;
               }

               string note = PlayConversation(behavior, services, row, speaker, recorder, transcripts, cap, out CostScenario played,
                  out List<string> replies, out ConversationTranscript transcript);
               reference ??= transcript;
               prose.Add(CostBenchReport.SummarizeProse(played, recorder.Calls, replies, note));
               if (row.Pass != CostPass.Standard) continue;

               string tagNote = CostBenchReport.CapReached(recorder.Spent, cap)
                  ? $"not run: the ${CostBenchReport.Money(cap)} cap was reached"
                  : TagCases(behavior, services, played, speaker, recorder, tagOutcomes, transcripts, cap, unscored);
               // A skipped case was billed but not scored: its calls stay out, or each reply would carry its cost.
               tagging.Add(CostBenchReport.SummarizeInterpret(played, recorder.Calls.Where(c => c.Phase == CostPhase.Tagging && !unscored.Contains(c)).ToList(),
                  tagOutcomes, tagNote));
            }

            foreach (CostScenario row in rows.Where(r => r.Pass == CostPass.Interpret))
            {
               string note = !Priceable(row) ? CostBenchReport.NotPriced(ProviderOf(row.Pass).Provider)
                  : CostBenchReport.CapReached(recorder.Spent, cap)
                  ? $"not run: the ${CostBenchReport.Money(cap)} cap was reached"
                  : ReadCases(services, row, recorder, outcomes, cap);
               interpret.Add(CostBenchReport.SummarizeInterpret(row, recorder.Calls, outcomes, note));
            }

            foreach (CostScenario row in rows.Where(r => r.Pass == CostPass.Fold))
               folds.Add(!Priceable(row) ? new FoldRowResult {Row = row, Note = CostBenchReport.NotPriced(ProviderOf(row.Pass).Provider)}
                  : CostBenchReport.CapReached(recorder.Spent, cap)
                  ? new FoldRowResult {Row = row, Note = "not run: cap reached"}
                  : FoldReference(services, row, reference, recorder));
         }
         finally
         {
            services.UsageObserver = priorObserver;
            services.WireObserver = priorWire;
            _watch = null;
         }

         if (prefixWatch.Capture.Rows.Count > 0)
            try { File.WriteAllText(CapturePath, prefixWatch.Capture.ToJson()); }
            catch (Exception ex) { ModLog.Error($"[COST] capture not written: {ex.Message}"); }

         string report = BuildReport(prose, interpret, folds, tagging, recorder, transcripts, version, speaker, prefixWatch);
         try { File.AppendAllText(ReportPath, report); }
         catch (Exception ex) { ModLog.Error($"[COST] report not written: {ex.Message}"); }

         ModLog.Info($"[COST] cr.cost_bench: {rows.Count} row(s), ${CostBenchReport.Money(recorder.Spent)} spent, report {ReportPath}");

         return $"cr.cost_bench: {rows.Count} row(s) measured with {speaker.Name}, ${CostBenchReport.Money(recorder.Spent)} spent. Tables and transcripts in {ReportPath}";
      }

      // The provider a row runs on (its role's, as set in the MCM), and whether that provider bills each call itself: only
      // OpenRouter's usage.cost is known to; NanoGPT and a direct endpoint are priced from the row.
      private static (string Provider, bool Bills) ProviderOf(CostPass pass)
      {
         ServiceRole role = pass == CostPass.Interpret ? ServiceRole.Interpret : pass == CostPass.Fold ? ServiceRole.Housekeeping : ServiceRole.Prose;
         ProviderKind kind = LlmServices.KindForRole(role);

         return (kind == ProviderKind.OpenAiCompatible ? "your OpenAI-compatible endpoint" : kind.ToString(), kind == ProviderKind.OpenRouter);
      }

      private static bool Priceable(CostScenario row) => CostPricing.CanCap(row, ProviderOf(row.Pass).Bills);

      // ── PROSE and STANDARD ─────────────────────────────────────────────

      private static string PlayConversation(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario row, Hero speaker, CostRecorder recorder,
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
         replies = new List<string>();
         var notes = new List<string>();
         transcripts.AppendLine($"--- [{row.Pass.ToString().ToLowerInvariant()}] {played.Name}: {played.Model}; {played.Setup} ---");

         // The opening, for real. Gabriel (24/10/2026): the scene the character sets before the player's first word is
         // the call that pays the cold prompt, and it is PROSE only, never interpreted (the chat issues it with no player
         // line, which keeps the interpreter out). Then fearente's reassurance answered by the model itself.
         var opening = new ConversationTranscript();
         if (CrSettings.Instance?.CharactersSetTheScene ?? true)
            SetTheScene(behavior, services, played, speaker, profile, opening, recorder, transcripts, replies, notes, cap);
         for (var i = 0; i < Opening.Length; i++)
            Answer(behavior, services, played, enhanced, speaker, profile, opening, playerName, Opening[i], i + 1, recorder, transcripts, replies, notes, cap);

         // The middle: six exchanges, then a warm-up at turn 7 and the measured reply at turn 8; fourteen lines unfolded.
         var middle = new ConversationTranscript();
         foreach ((string p, string n) in Exchanges.Take(6)) RecordExchange(middle, playerName, speaker, p, n);
         transcripts.AppendLine("[middle: six synthetic exchanges]");
         Answer(behavior, services, played, enhanced, speaker, profile, middle, playerName, MiddleLines[0], CostWindows.MiddleWarmUp, recorder, transcripts, replies, notes, cap);
         Answer(behavior, services, played, enhanced, speaker, profile, middle, playerName, MiddleLines[1], CostWindows.MiddleTurns[0], recorder, transcripts, replies, notes, cap);

         // The long conversation: ten exchanges folded into the recap, six kept verbatim, a warm-up at turn 17, then turn 18
         // asks for what only the recap holds.
         transcript = new ConversationTranscript();
         foreach ((string p, string n) in Exchanges.Take(10)) RecordExchange(transcript, playerName, speaker, p, n);
         transcript.ApplySummary(Recap, transcript.Count);
         foreach ((string p, string n) in Exchanges.Skip(10).Take(6)) RecordExchange(transcript, playerName, speaker, p, n);
         transcripts.AppendLine("[long: ten exchanges folded into a recap, six kept verbatim]");
         Answer(behavior, services, played, enhanced, speaker, profile, transcript, playerName, LongLines[0], CostWindows.LongWarmUp, recorder, transcripts, replies, notes, cap);
         Answer(behavior, services, played, enhanced, speaker, profile, transcript, playerName, LongLines[1], CostWindows.LongTurns[0], recorder, transcripts, replies, notes, cap);

         if (!CostBenchReport.CapReached(recorder.Spent, cap)) WriteLetter(services, played, profile, recorder, transcripts);
         transcripts.AppendLine();

         return string.Join("; ", notes);
      }

      // The chat's scene opener (ChatViewModel.OpenWithScene): the cue as the message, the retry allowed, no interpreter; the
      // scene alone enters the transcript, never the cue.
      private static void SetTheScene(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario row, Hero speaker, NpcProfile profile,
                                      ConversationTranscript transcript, CostRecorder recorder, StringBuilder transcripts, List<string> replies, List<string> notes, double cap)
      {
         if (CostBenchReport.CapReached(recorder.Spent, cap)) return;

         try
         {
            EncounterContext ctx = EncounterContextBuilder.Build(speaker, profile, behavior.GameStateBridge, SceneOpenerPolicy.Cue);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
            recorder.Begin(row, CostPhase.Opener, 0);
            NpcChatResult result = Task.Run(() => services.ChatService.ChatAsync(profile, world, SessionFrom(transcript, speaker.StringId), SceneOpenerPolicy.Cue, ctx,
                                                     cts.Token, allowTruncationRetry: true), cts.Token).GetAwaiter().GetResult();
            string scene = (result?.Response?.Dialogue ?? "").Replace("\r", " ").Replace("\n", " ");
            transcripts.AppendLine(result is {IsSuccess: true} ? $"[scene] {scene}" : $"[scene] ! {result?.ErrorMessage ?? "no reply"}");
            if (result is not {IsSuccess: true}) { notes.Add("scene opener failed"); return; }

            transcript.Record(new TranscriptLine {IsPlayer = false, SpeakerName = speaker.Name?.ToString(), SpeakerHeroId = speaker.StringId, Text = scene});
            _watch?.Kept(scene);
         }
         catch (Exception ex)
         {
            Exception inner = (ex as AggregateException)?.InnerException ?? ex;
            notes.Add($"scene opener: {inner.GetType().Name}");
         }
      }

      private static void RecordExchange(ConversationTranscript transcript, string playerName, Hero speaker, string player, string npc)
      {
         transcript.Record(new TranscriptLine {IsPlayer = true, SpeakerName = playerName, Text = player});
         transcript.Record(new TranscriptLine {IsPlayer = false, SpeakerName = speaker.Name?.ToString(), SpeakerHeroId = speaker.StringId, Text = npc});
      }

      // One reply over the transcript as it stands, recorded into it as the chat records it.
      private static void Answer(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario row, bool enhanced, Hero speaker, NpcProfile profile,
                                 ConversationTranscript transcript, string playerName, string line, int turn, CostRecorder recorder, StringBuilder transcripts,
                                 List<string> replies, List<string> notes, double cap)
      {
         if (CostBenchReport.CapReached(recorder.Spent, cap))
         {
            if (!notes.Contains("cap reached")) notes.Add("cap reached");
            return;
         }

         transcripts.AppendLine($"> ({turn}) {line}");
         string error = PlayTurn(behavior, services, row, enhanced, speaker, profile, SessionFrom(transcript, speaker.StringId), line, turn, recorder, out string reply);
         replies.Add(reply);
         _watch?.Kept(reply);
         transcripts.AppendLine(error == null ? $"< {reply}" : $"! {error}");
         if (error != null) notes.Add($"turn {turn}: {error}");
         RecordExchange(transcript, playerName, speaker, line, reply);
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
                                     NpcProfile profile, ChatSession session, string line, int turn, CostRecorder recorder, out string reply)
      {
         reply = "";
         try
         {
            EncounterContext ctx = EncounterContextBuilder.Build(speaker, profile, behavior.GameStateBridge, line);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
            CancellationToken ct = cts.Token;

            // As the chat issues it: in Enhanced the prose call fails fast on a cut reply (no retry) so the fallback can take
            // over; in Standard the client's own bigger-budget retry stands. The prompt is the same in both.
            recorder.Begin(row, CostPhase.Prose, turn);
            NpcChatResult result = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, line, ctx, ct, allowTruncationRetry: !enhanced), ct)
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
      private static string Fold(LlmServices services, string existing, string newLines, CostRecorder recorder)
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

      private static void WriteLetter(LlmServices services, CostScenario row, NpcProfile profile, CostRecorder recorder, StringBuilder transcripts)
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
         _watch?.Kept(text);
         transcripts.AppendLine(written ? $"[letter] {(text.Length > 400 ? text.Substring(0, 400) + "..." : text)}" : "[letter] not written in time");
      }

      // ── STANDARD, deeds tagged in its own reply ──────────────────────

      // Each staged case on a fresh session: the earlier turns that settle the deed, the gate that teaches its verb opened on
      // the context as the game would open it, then the player's last line. Only read: ChatAsync parses the tags and executes
      // nothing. A case whose verb the prompt does not teach after all is skipped and named, never scored.
      private static string TagCases(NpcMemoryCampaignBehavior behavior, LlmServices services, CostScenario row, Hero speaker, CostRecorder recorder,
                                     List<InterpretOutcome> outcomes, StringBuilder transcripts, double cap, HashSet<CostCall> unscored)
      {
         using var forced = new ForcedSetup(services, row, false);
         NpcProfile own = CopyOf(behavior.Store.Get(speaker.StringId) ?? HeroProfileMapper.FromHero(speaker));
         Hero outsider = PickOutsider();
         NpcProfile outsiderProfile = outsider == null ? null : CopyOf(behavior.Store.Get(outsider.StringId) ?? HeroProfileMapper.FromHero(outsider));
         var skipped = new List<string>();
         transcripts.AppendLine($"--- [standard tags] {row.Name}: {row.Model}; {row.Setup} ---");
         foreach (StandardActionCase test in StandardActionCases.All)
         {
            if (CostBenchReport.CapReached(recorder.Spent, cap)) return "stopped early: cap reached";

            bool outside = StandardActionCases.NeedsOutsider(test.Gate);
            if (outside && outsider == null)
            {
               skipped.Add(test.Id);
               transcripts.AppendLine($"> [{test.Id}] (skipped: no unwed lord of another house to play it)");

               continue;
            }

            Hero who = outside ? outsider : speaker;
            NpcProfile profile = outside ? outsiderProfile : own;
            var session = new ChatSession();
            foreach ((string player, string character) in test.Earlier)
            {
               session.AddPlayerMessage(player);
               session.AddNpcMessage(character);
            }

            recorder.Begin(row, CostPhase.Tagging, 0, test.Difficulty);
            int billedBefore = recorder.Calls.Count;
            string error = null;
            string reply = "";
            IReadOnlyList<GameAction> tags = new List<GameAction>();
            try
            {
               EncounterContext ctx = EncounterContextBuilder.Build(who, profile, behavior.GameStateBridge, test.Line);
               foreach (KeyValuePair<string, object> fact in StandardActionCases.GateFacts(test.Gate))
                  typeof(EncounterContext).GetProperty(fact.Key)?.SetValue(ctx, fact.Value);
               var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
               using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) * 3));
               CancellationToken ct = cts.Token;
               NpcChatResult result = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, test.Line, ctx, ct, allowTruncationRetry: true), ct)
                                          .GetAwaiter().GetResult();
               if (result is {IsSuccess: true})
               {
                  tags = result.Response?.Actions ?? new List<GameAction>();
                  reply = (result.Response?.Dialogue ?? "").Replace("\r", " ").Replace("\n", " ");
               }
               else error = result?.ErrorMessage ?? "no reply";
            }
            catch (Exception ex)
            {
               Exception inner = (ex as AggregateException)?.InnerException ?? ex;
               error = $"{inner.GetType().Name}: {inner.Message}";
            }

            transcripts.AppendLine($"> [{test.Id}]{(outside ? " (" + who.Name + ")" : "")} {test.Line}");
            if (error == null && !StandardActionCases.Taught(test, _watch?.LastPrompt))
            {
               skipped.Add(test.Id);
               foreach (CostCall billed in recorder.Calls.Skip(billedBefore)) unscored.Add(billed);
               transcripts.AppendLine($"  (skipped: the prompt did not teach {test.Verb})");

               continue;
            }

            (bool right, bool invented) = error == null ? StandardActionCases.Score(test, tags) : (false, false);
            string tagged = tags.Count == 0
               ? "nothing"
               : string.Join(" + ", tags.Select(a => a.Type + (a.Parameters == null || a.Parameters.Count == 0
                                                                   ? ""
                                                                   : "(" + string.Join(", ", a.Parameters.Select(p => p.Key + "=" + p.Value)) + ")")));
            string forbidden = test.Scoring.ForbiddenType;
            string expected = test.Scoring.Expected.Count == 0
               ? "nothing" + (forbidden != null && forbidden != "none" ? " (forbidden: " + forbidden + ")" : "")
               : string.Join(" + ", test.Scoring.Expected.Select(e => e.Type));
            outcomes.Add(new InterpretOutcome {
               ScenarioName = row.Name, Difficulty = test.Difficulty, Right = right, Invented = invented,
               Miss = right ? "" : $"{test.Id} expected {expected}, tagged {(error == null ? tagged : "(call failed: " + error + ")")}"
            });
            transcripts.AppendLine(error == null ? $"< {reply}" : $"! {error}");
            transcripts.AppendLine($"  [{(right ? "right" : "WRONG")}] expected {expected}, tagged {tagged}");
         }

         transcripts.AppendLine();

         return skipped.Count == 0 ? "" : $"skipped, verb not taught: {string.Join(", ", skipped)}";
      }

      // ── INTERPRET ─────────────────────────────────────────────────────

      private static string ReadCases(LlmServices services, CostScenario row, CostRecorder recorder, List<InterpretOutcome> outcomes, double cap)
      {
         using var forced = new ForcedInterpreter(row.Model);
         ILlmClient client = services.InterpreterClient;

         // The same readings and the same scoring as the replay outside the game (Logic/Diagnostics/CostReplay).
         return Task.Run(() => CostReplayRunner.ReadAsync(row, client, recorder, outcomes, cap)).GetAwaiter().GetResult();
      }

      // ── FOLD ──────────────────────────────────────────────────────────

      private static FoldRowResult FoldReference(LlmServices services, CostScenario row, ConversationTranscript reference, CostRecorder recorder)
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

      private static string BuildReport(List<ProseRowResult> prose, List<InterpretRowResult> interpret, List<FoldRowResult> folds, List<InterpretRowResult> tagging, CostRecorder recorder,
                                        StringBuilder transcripts, string version, Hero speaker, PrefixWatch prefixWatch)
      {
         var sb = new StringBuilder();
         sb.AppendLine();
         sb.AppendLine("========================================================================");
         sb.AppendLine($"  cr.cost_bench run - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Calradia Remembers {version}");
         sb.AppendLine($"  Speaker: {speaker.Name} ({speaker.StringId}); per prose row {RepliesPerRow} replies (3 played, then the middle and a long conversation over a synthetic history) and one letter.");
         sb.AppendLine($"  Spent: ${CostBenchReport.Money(recorder.Spent)}");
         sb.AppendLine("========================================================================");
         sb.AppendLine();
         sb.AppendLine(CostBenchReport.Markdown(prose, interpret, folds, version, DateTime.Now, tagging));
         sb.AppendLine("For the Nexus description:");
         sb.AppendLine(CostBenchReport.NexusCode(prose, interpret, version, DateTime.Now, tagging));
         sb.AppendLine("Cacheable prefix, prose calls compared with the previous one of their row (a change near the top makes the whole prompt full price):");
         if (prefixWatch.Findings.Count == 0) sb.AppendLine("  unchanged on every call.");
         foreach (string f in prefixWatch.Findings) sb.AppendLine("  " + f);
         sb.AppendLine();
         sb.AppendLine(prefixWatch.Capture.Rows.Count > 0
            ? $"Captured for a replay outside the game: {CapturePath} ({prefixWatch.Capture.Rows.Count} row(s))."
            : "Nothing captured (no prose or standard row ran).");
         sb.AppendLine();
         sb.AppendLine("Every billed call:");
         foreach (CostCall c in recorder.Calls)
            sb.AppendLine($"  [{c.Pass}] {c.ScenarioName} | turn {c.Turn} | {c.Phase}{(c.Phase == CostPhase.Interpreter ? " " + c.Difficulty : "")} | {c.Model} | in {c.PromptTokens} (cached {c.CachedTokens}) | out {c.CompletionTokens} (thinking {c.ReasoningTokens}) | {(c.Cost == null ? "unpriced" : "$" + CostBenchReport.Money(c.Cost.Value))} | {c.Seconds:0.0} s");
         sb.AppendLine();
         sb.AppendLine("Transcripts (read them: fearente's ships are turns 2 and 3, the recap is tested at turn 18):");
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

      // For a wedding or a defection: an adult lord of another house, unwed, of the other sex, free and in the world.
      private static Hero PickOutsider()
      {
         Hero main = Hero.MainHero;
         if (main == null) return null;

         return Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != main && h.IsLord && !h.IsChild && !h.IsPrisoner && h.Spouse == null
                                                        && h.IsFemale != main.IsFemale && h.Clan != null && h.Clan != main.Clan
                                                        && h.Clan.Leader != h && HeroPresence.IsInTheWorld(h));
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

      /// <summary>
      ///   Compares each prose call's cacheable prefix with the previous prose call of its row, and says where and how it
      ///   changed. Every prose call of a row shares the speaker, the world and the setup, so its prefix should never change.
      ///   It also captures each call of a prose or standard row as it went, for a replay outside the game.
      /// </summary>
      private sealed class PrefixWatch : ILlmWireObserver
      {
         private readonly CostRecorder _recorder;
         private readonly Dictionary<string, string> _last = new Dictionary<string, string>();
         private readonly Dictionary<string, CaptureRow> _rows = new Dictionary<string, CaptureRow>();
         private readonly HashSet<string> _seen = new HashSet<string>();
         private CaptureRow _current;

         public PrefixWatch(CostRecorder recorder) => _recorder = recorder;

         public List<string> Findings { get; } = new List<string>();

         /// <summary>The whole system prompt of the last request, to check that a staged case's verb was taught.</summary>
         public string LastPrompt { get; private set; } = "";

         /// <summary>What the run's prose and standard rows sent, call by call.</summary>
         public CostCaptureFile Capture { get; } = new CostCaptureFile();

         public void OnRequest(LlmWireSnapshot snapshot)
         {
            if (snapshot == null) return;

            LastPrompt = snapshot.Request?.SystemPrompt ?? (snapshot.StablePrefix ?? "") + (snapshot.DynamicTail ?? "");
            CostPhase phase = _recorder.Phase;
            if (phase == CostPhase.Prose || phase == CostPhase.Opener || phase == CostPhase.Letter) Record(snapshot, phase);
            if (phase != CostPhase.Prose && phase != CostPhase.Opener) return;

            string key = $"{_recorder.Row.Pass}:{_recorder.Row.Name}";
            string now = snapshot.StablePrefix ?? "";
            if (_last.TryGetValue(key, out string before))
            {
               int at = CostBenchReport.FirstDivergence(before, now);
               if (at >= 0)
                  Findings.Add($"[{_recorder.Row.Pass}] {_recorder.Row.Name} turn {_recorder.Turn}: prefix {now.Length:N0} chars, changed at char {at:N0} "
                               + $"after \"{Clip(now, at - 80, 80)}\": was \"{Clip(before, at, 160)}\", now \"{Clip(now, at, 160)}\"");
            }

            _last[key] = now;
         }

         /// <summary>The reply the conversation kept for the call just captured.</summary>
         public void Kept(string reply)
         {
            CaptureStep last = _current?.Steps.LastOrDefault();
            if (last != null && string.IsNullOrEmpty(last.Reply)) last.Reply = reply ?? "";
         }

         // The first attempt of each call only: a retry repeats the same request with a bigger budget, which the replay's
         // own client does again by itself.
         private void Record(LlmWireSnapshot snapshot, CostPhase phase)
         {
            CostScenario row = _recorder.Row;
            LlmRequest request = snapshot.Request;
            if (request == null || (row.Pass != CostPass.Prose && row.Pass != CostPass.Standard)) return;
            if (!_seen.Add($"{row.Pass}:{row.Name}:{phase}:{_recorder.Turn}")) return;

            string rowKey = $"{row.Pass}:{row.Name}";
            if (!_rows.TryGetValue(rowKey, out CaptureRow captured))
            {
               captured = new CaptureRow {Name = row.Name, Model = row.Model, Pass = row.Pass, Compact = row.Compact, Reasoning = row.Reasoning};
               _rows[rowKey] = captured;
               Capture.Rows.Add(captured);
            }

            _current = captured;
            captured.Steps.Add(new CaptureStep {
               Phase = phase, Turn = _recorder.Turn, StablePrefix = snapshot.StablePrefix ?? "", DynamicTail = snapshot.DynamicTail ?? "",
               Messages = CostReplayPolicy.Tag(request.Messages, captured.Steps.Select(s => s.Reply).ToList()),
               MaxTokens = request.Parameters?.MaxTokens ?? 0, Creativity = request.Parameters?.Creativity ?? 0,
               PresencePenalty = request.Parameters?.PresencePenalty ?? 0, AllowTruncationRetry = request.Parameters?.AllowTruncationRetry ?? true,
               ReasoningOverride = request.Parameters?.ReasoningOverride ?? "", PayloadJson = snapshot.Json ?? ""
            });
         }

         private static string Clip(string s, int from, int length)
         {
            from = Math.Max(0, from);
            if (from >= s.Length) return "";

            return s.Substring(from, Math.Min(length, s.Length - from)).Replace("\r", " ").Replace("\n", " / ");
         }
      }

      /// <summary>A prose or standard row's model and MCM choices, forced for its run and put back after, whatever happens.</summary>
      private sealed class ForcedSetup : IDisposable
      {
         private readonly LlmServices _services;
         private readonly string _priorProseOverride;
         private readonly bool _priorCompact;
         private readonly int _priorReasoning;
         private readonly Dropdown<McmChoice> _reasoning;
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
               if (_reasoning != null && !string.IsNullOrWhiteSpace(row.Reasoning)) _reasoning.Pick(row.Reasoning);
            }

            services.Invalidate();
         }

         /// <summary>The PROSE reasoning effort this run is measured at.</summary>
         public string Reasoning => _reasoning?.Choice() ?? "";

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
