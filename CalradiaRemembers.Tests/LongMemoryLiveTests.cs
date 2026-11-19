// Code written by Gabriel Mailhot, 17/11/2026.
// A/B of Long Memory, for real money. Goeth (Discord, 17/11/2026): "I had a 150-line night in a tavern with a companion, full
// of teasing, questions traded back and forth, and promises. The next time we talked she remembered it in two lines. Her
// attitude shifted, and she lost track of who had asked for what." Gabriel (17/11/2026): "fais la mesure avec et sans".
//
// A companion, Ira, spent a long night with the player at a tavern. In it: an inside joke (the goat that ate the captain's
// boots), her promise (to teach the player the longsword before the snows), the player's promise (a red cloak from Pravend),
// and a confidence (she is afraid of horses). Twelve days later the player speaks to her again.
//   A = what CR sends today: her history holds the night as a two-line summary.
//   B = the same prompt plus the passages LongMemoryRecall (the shipped rule, run on the night as the story archive keeps it)
//       finds for the player's line, rendered by the SDK's own PromptBuilder.
// Five lines: four pick up a thread of that night, one ordinary (does B drag old words in where nothing calls for them?).
// Scored by the facts each reply gets right (keywords), read by hand in the report. First runs (17/11/2026, glm-4.6, 3
// draws): read by hand, A 0 of 12 threads right with 6 inventions or denials ("What horses?", a promise of salt flats), B
// 12 of 12 once the recall stemmed words (before: "promised" missed "promise" and the goat story came back instead). Never runs by itself: [Explicit],
// CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY. Writes Logs/CalradiaRemembers_long_memory_ab.log.
//
//   CR_LONGMEM_MODEL    the model (default z-ai/glm-4.6, Gabriel's prose model)
//   CR_LONGMEM_REPEATS  draws per line and version (default 3)
//   CR_LONGMEM_CAP      dollars at most (default 1)

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Logic.StoryArchive;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   [Explicit("Calls a real LLM and spends credit. Run deliberately: CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY, then --filter LongMemoryLiveTests.")]
   public sealed class LongMemoryLiveTests
   {
      private static readonly string ReportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                               "Mount and Blade II Bannerlord", "Logs", "CalradiaRemembers_long_memory_ab.log");
      private static readonly HttpClient Http = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};

      private const string Ira = "comp_ira";
      private const double NightDay = 880, Today = 892;

      // (player line, what a reply that remembers would carry, label)
      private static readonly (string Line, Regex Fact, string Label)[] Probes = {
         ("Remember the goat? I still laugh about it.", new Regex(@"\bboots?\b", RegexOptions.IgnoreCase), "the goat ate the captain's boots"),
         ("So, about what you promised me that night...", new Regex(@"long\s?sword|\bsword\b|\bblade\b", RegexOptions.IgnoreCase), "her promise: the longsword lessons"),
         ("And did I promise you something too?", new Regex(@"\bcloak\b", RegexOptions.IgnoreCase), "his promise: a red cloak"),
         ("Your secret is safe with me, you know. About the horses.", new Regex(@"\bafraid|fear|scared|frighten|your word|nobody knows|told no one", RegexOptions.IgnoreCase), "her confidence: she fears horses"),
         ("Fine morning. Shall we break camp soon?", null, "ordinary line")
      };

      // Words of the night: an ordinary reply that uses them has dragged the old talk in.
      private static readonly Regex NightWords = new Regex(@"\b(goat|cloak|longsword|tavern|Dancing Goat|Pravend)\b", RegexOptions.IgnoreCase);

      private string _key = "";

      [SetUp]
      public void Firewall()
      {
         if (Environment.GetEnvironmentVariable("CR_RUN_LIVE_LLM") != "1") Assert.Ignore("CR_RUN_LIVE_LLM is not 1: nothing is spent.");
         _key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
         if (string.IsNullOrWhiteSpace(_key)) Assert.Ignore("No OPENROUTER_API_KEY.");
      }

      [Test]
      public async Task GIVEN_a_long_night_kept_as_a_summary_WHEN_the_player_picks_up_its_threads_with_and_without_long_memory_THEN_the_report_counts_what_she_remembers()
      {
         string model = Environment.GetEnvironmentVariable("CR_LONGMEM_MODEL") ?? "z-ai/glm-4.6";
         int repeats = int.TryParse(Environment.GetEnvironmentVariable("CR_LONGMEM_REPEATS"), out int r) ? r : 3;
         double cap = double.TryParse(Environment.GetEnvironmentVariable("CR_LONGMEM_CAP"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double c) ? c : 1.0;

         List<StoryArchiveRecord> night = Night();
         var client = new OpenRouterClient(Http, new OpenRouterConfig {
            ApiKey = _key, Model = model, ReasoningProvider = () => "off", IsOpenRouterProvider = () => true,
            AppTitle = "Calradia Remembers (long memory A/B)", TimeoutSecondsProvider = () => 180
         });

         var report = new StringBuilder();
         report.AppendLine($"=== Long Memory A/B, {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC, model {model}, reasoning off, {repeats} draws per line and version ===");
         report.AppendLine("A = today's prompt (the night as a two-line summary). B = A + the passages LongMemoryRecall finds (budget 4000).");
         var right = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var asked = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var dragged = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var ordinary = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         double spent = 0;
         string refused = null;

         foreach ((string line, Regex fact, string label) in Probes)
         {
            IReadOnlyList<LongMemoryPassage> passages = LongMemoryRecall.Find(new LongMemoryRecallFacts {
               Records = night, CharacterId = Ira, Query = line, CurrentEpisodeId = "today", BudgetChars = LongMemoryRecall.DefaultBudgetChars
            });
            string a = BuildPrompt(null), b = BuildPrompt(passages);
            report.AppendLine();
            report.AppendLine($"--- \"{line}\" ({label}); recalled: {passages.Count} passage(s), {passages.Sum(p => p.Lines.Count)} line(s), prompt +{b.Length - a.Length} chars");
            foreach (LongMemoryPassage p in passages)
               report.AppendLine("    recalled: " + string.Join(" / ", p.Lines.Select(l => (l.IsPlayer ? "P: " : "I: ") + l.Text)));

            for (var i = 0; i < repeats; i++)
               foreach ((string version, string system) in new[] {("A", a), ("B", b)})
               {
                  if (spent >= cap)
                  {
                     report.AppendLine($"(cap of ${cap:0.00} reached: stopped)");
                     goto done;
                  }

                  LlmResponse reply = await client.CompleteAsync(new LlmRequest {
                     SystemPrompt = system,
                     Messages = new List<LlmMessage> {new LlmMessage(MessageRole.User, line)},
                     Parameters = new LlmParameters {MaxTokens = 700, Creativity = 0.8f}
                  }, CancellationToken.None);

                  spent += reply?.Usage?.Cost ?? 0;
                  // 19/11/2026: the key's own $20 limit refused every call, and the run "passed" on blank replies at $0.
                  if (string.IsNullOrWhiteSpace(reply?.Content))
                  {
                     refused = reply?.ErrorMessage ?? "(no reply)";
                     report.AppendLine("(the provider gave no reply, stopped: " + refused + ")");
                     goto done;
                  }
                  string text = Dialogue(reply?.Content ?? reply?.ErrorMessage ?? "(no reply)");
                  string mark;
                  if (fact != null)
                  {
                     asked[version]++;
                     bool ok = fact.IsMatch(text);
                     if (ok) right[version]++;
                     mark = ok ? "REMEMBERS" : "misses";
                  }
                  else
                  {
                     ordinary[version]++;
                     bool drags = NightWords.IsMatch(text);
                     if (drags) dragged[version]++;
                     mark = drags ? "DRAGS THE NIGHT IN" : "stays on the moment";
                  }

                  report.AppendLine($"[{version}, {mark}] {text.Replace("\n", " ").Trim()}");
               }
         }

         done:
         report.AppendLine();
         report.AppendLine("=== Threads of the night picked up correctly (keyword) ===");
         foreach (string v in new[] {"A", "B"}) report.AppendLine($"{v}: {right[v]} of {asked[v]}");
         report.AppendLine("=== Ordinary line answered by dragging the night in ===");
         foreach (string v in new[] {"A", "B"}) report.AppendLine($"{v}: {dragged[v]} of {ordinary[v]}");
         report.AppendLine($"Spent: ${spent:0.0000} (OpenRouter's own figure).");
         report.AppendLine("Read the replies: a keyword can be said to deny a fact, and A may invent a plausible promise that is not the real one.");

         Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
         File.AppendAllText(ReportPath, report + Environment.NewLine);
         TestContext.Out.WriteLine(report.ToString());
         if (refused != null) Assert.Fail("The provider refused the calls: " + refused);
      }

      /// <summary>The night as the story archive keeps it: 40 lines, the four threads buried among small talk.</summary>
      private static List<StoryArchiveRecord> Night()
      {
         string[] lines = {
            "P|Two ales, and none of that watered swill.",
            "I|You say that every time, and every time it is watered.",
            "P|Then tonight I will complain louder.",
            "I|The innkeeper is twice your size.",
            "P|Then you will complain for me.",
            "I|I fight for coin, not for ale.",
            "P|Tell me about the worst captain you ever served.",
            "I|Oh, that one is easy. Captain Harlan, at the Sturgian border.",
            "P|What made him the worst?",
            "I|He was vain. He polished his boots every evening, a whole hour.",
            "P|And?",
            "I|And one night the company goat got loose and ate his boots. Both of them. Laces and all.",
            "P|A goat ate the captain's boots?",
            "I|He marched barefoot for a week. We called him Captain Goat until the day he died.",
            "P|I will never look at a goat the same way.",
            "I|Nobody who served under him ever did.",
            "P|You fight well. Where did you learn?",
            "I|My father. He swore a blade was the only honest thing in Calradia.",
            "P|I can hold a spear, but a longsword feels like a stranger in my hands.",
            "I|Then I will teach you. The longsword, properly, before the first snows. That is a promise.",
            "P|Before the snows. I will hold you to it.",
            "I|You had better. I do not make promises to just anyone.",
            "P|Then I owe you something in return.",
            "I|You owe me nothing. Well, perhaps one thing.",
            "P|Name it.",
            "I|There is a red cloak in a shop window in Pravend. Wool, lined with fox. I have looked at it for two years.",
            "P|Then it is yours. When we pass through Pravend, I will buy you that red cloak. My promise for yours.",
            "I|I will remember that. I remember everything, you know.",
            "P|Why do you always ride at the back of the column?",
            "I|Can I tell you something you will not repeat?",
            "P|On my honour.",
            "I|I am afraid of horses. Since I was a girl. A stallion broke my brother's leg in front of me.",
            "P|You ride every day.",
            "I|I ride every day with my heart in my throat. Nobody in the company knows. Keep it that way.",
            "P|It stays between us.",
            "I|Good. Now, another round, and you are paying.",
            "P|The ale is still watered.",
            "I|Complain louder, then.",
            "P|To the snows, and to the cloak.",
            "I|To the snows. And to goats with good taste in boots."
         };

         var records = new List<StoryArchiveRecord>();
         foreach (string l in lines)
         {
            bool player = l.StartsWith("P|", StringComparison.Ordinal);
            records.Add(new StoryArchiveRecord {
               Id = Guid.NewGuid().ToString("N"), Kind = StoryArchiveKind.Line, EpisodeId = "tavern_night", SceneKind = "conversation", Time = NightDay,
               PlaceName = "the Dancing Goat in Pravend", SpeakerId = player ? "main_hero" : Ira, SpeakerName = player ? "Garret" : "Ira", IsPlayer = player,
               Text = l.Substring(2), Audience = new List<string> {Ira}
            });
         }

         return records;
      }

      private static string BuildPrompt(IReadOnlyList<LongMemoryPassage> passages)
      {
         var npc = new NpcProfile {
            Id = Ira, Name = "Ira", Clan = "Garret's clan", Faction = "Vlandia", Age = 27, IsFemale = true, ReputationWithPlayer = 35,
            Events = new List<NotableEvent> {
               new NotableEvent((int) NightDay - 40, NotableEventType.FirstMeeting, "Met the player in a tavern in Ocs Hall and joined their company as a sellsword."),
               new NotableEvent((int) NightDay, NotableEventType.Other, "Spent a long night drinking with the player at the Dancing Goat in Pravend, trading stories and teasing. We made each other promises.")
            }
         };
         var context = new EncounterContext {
            LeanLevel = LeanPromptLevel.Full,
            RecalledExchanges = passages?.Select(p => new RecalledExchange {
               When = $"{(int) (Today - p.Time)} days ago", Place = p.PlaceName,
               Lines = p.Lines.Select(l => new RecalledLine {Speaker = l.IsPlayer ? "PLAYER" : "YOU", Text = l.Text}).ToList()
            }).ToList()
         };

         return new PromptBuilder().BuildSystemPrompt(npc, new WorldState {CurrentDay = (int) Today}, context);
      }

      // ── A mission over three talks (Gabriel, 17/11/2026: "je peux parler de ma mission a un NPC a plusieurs reprises dans
      // differentes conversations, et la memoire devrait lier ces conversations... On fera un autre test pour mesurer la
      // performance."). Count Aldric sets a task near Varcheg (talk 1, day 860: clear the bandits, 1,000 denars, before the
      // harvest); the player reports back (talk 2, day 875: they moved to the hills; burn their stockade too, 1,500); the terms
      // change (talk 3, day 885: they hold his nephew Bran; bring him back alive, 2,000 and a horse, before the first snows).
      // Today is day 892. A reply is LATEST when it gives the terms of talk 3, STALE when it gives only older ones.
      //   A = today's prompt: the task as a two-line summary.
      //   W = A + recall by words alone (the records carry no thread keys).
      //   T = A + recall with the threads (the records carry "place:town_varcheg" where a line names Varcheg, as
      //       StoryThreadKeys writes it), About and Latest marked as LongMemoryRecaller marks them.
      // Runs (17/11/2026, glm-4.6, 3 draws, 12 asked): first A 0-1 latest, W 3 latest / 4 stale, T 3 latest / 3 stale: the best
      // match for "by when" named no place so no thread started, and the newest talk came as a window two lines short of the new
      // reward (the lord invented one). After seeding threads from the best match's conversation and recalling the newest talk
      // on the thread whole: T 9 latest / 0 stale; A 0 / 2, W 3 / 4. Still missed: "what exactly is the task now?", a line with
      // no word and no name in common (in play the talk under way usually names the matter first). About $0.05 a run.
      // Then (Gabriel: "oui, rappelle le fil le plus recent") a line matching nothing takes the most recent thread: run of
      // 18/11/2026 01:53 UTC cut short by the key's limit: T 3/3 latest on the reward, 3/3 on the deadline, 1 of 2 replies on
      // "the task now" (was 0), 0 stale; the ordinary line got no replies, so its drag with the fallback is still to measure.
      private const string Aldric = "lord_aldric";
      private const string Varcheg = "place:town_varcheg";

      private static readonly (string Line, Regex Latest, Regex Stale, string Label)[] MissionProbes = {
         ("What reward did we settle on for the Varcheg business?", new Regex(@"2[,.]?000|two thousand|\bhorse\b|\bgrey\b", RegexOptions.IgnoreCase),
          new Regex(@"1[,.]?[05]00|one thousand|fifteen hundred", RegexOptions.IgnoreCase), "the reward: 2,000 and the grey (was 1,000, then 1,500)"),
         ("By when do you need it done?", new Regex(@"\bsnows?\b|winter", RegexOptions.IgnoreCase),
          new Regex(@"\bharvest\b", RegexOptions.IgnoreCase), "the deadline: the first snows (was the harvest)"),
         ("Remind me, what exactly is the task now?", new Regex(@"\bBran\b|nephew|hostage", RegexOptions.IgnoreCase),
          new Regex(@"stockade|clear (them|the bandits)", RegexOptions.IgnoreCase), "the task: bring Bran back alive (was clear them, then burn the stockade)"),
         ("Any word on those bandits near Varcheg?", new Regex(@"\bBran\b|nephew|hostage|\bhills?\b", RegexOptions.IgnoreCase),
          null, "where things stand: the hills, the hostage"),
         ("Fine weather for riding today.", null, null, "ordinary line")
      };

      private static readonly Regex MissionWords = new Regex(@"\b(Varcheg|bandits?|Bran|stockade|denars?)\b", RegexOptions.IgnoreCase);

      [Test]
      public async Task GIVEN_a_mission_spoken_of_over_three_talks_WHEN_the_player_asks_how_it_stands_THEN_the_report_counts_the_latest_terms_with_and_without_threads()
      {
         string model = Environment.GetEnvironmentVariable("CR_LONGMEM_MODEL") ?? "z-ai/glm-4.6";
         int repeats = int.TryParse(Environment.GetEnvironmentVariable("CR_LONGMEM_REPEATS"), out int r) ? r : 3;
         double cap = double.TryParse(Environment.GetEnvironmentVariable("CR_LONGMEM_CAP"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double c) ? c : 1.0;

         List<StoryArchiveRecord> threaded = Mission(true), bare = Mission(false);
         var client = new OpenRouterClient(Http, new OpenRouterConfig {
            ApiKey = _key, Model = model, ReasoningProvider = () => "off", IsOpenRouterProvider = () => true,
            AppTitle = "Calradia Remembers (long memory threads A/B)", TimeoutSecondsProvider = () => 180
         });

         var report = new StringBuilder();
         report.AppendLine($"=== Long Memory threads A/W/T, {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC, model {model}, reasoning off, {repeats} draws per line and version ===");
         report.AppendLine("A = summary only. W = + recall by words. T = + recall with threads (About/Latest marked).");
         string[] versions = {"A", "W", "T"};
         Dictionary<string, int> latest = versions.ToDictionary(v => v, v => 0), stale = versions.ToDictionary(v => v, v => 0),
                                 asked = versions.ToDictionary(v => v, v => 0), dragged = versions.ToDictionary(v => v, v => 0),
                                 ordinary = versions.ToDictionary(v => v, v => 0);
         double spent = 0;
         string refused = null;

         foreach ((string line, Regex latestFact, Regex staleFact, string label) in MissionProbes)
         {
            string[] queryThreads = line.IndexOf("Varcheg", StringComparison.OrdinalIgnoreCase) >= 0 ? new[] {Varcheg} : new string[0];
            IReadOnlyList<LongMemoryPassage> words = LongMemoryRecall.Find(new LongMemoryRecallFacts {
               Records = bare, CharacterId = Aldric, Query = line, CurrentEpisodeId = "today", BudgetChars = LongMemoryRecall.DefaultBudgetChars
            });
            IReadOnlyList<LongMemoryPassage> threads = LongMemoryRecall.Find(new LongMemoryRecallFacts {
               Records = threaded, CharacterId = Aldric, Query = line, CurrentEpisodeId = "today", BudgetChars = LongMemoryRecall.DefaultBudgetChars,
               QueryThreads = queryThreads
            });
            var prompts = new Dictionary<string, string> {["A"] = MissionPrompt(null), ["W"] = MissionPrompt(words), ["T"] = MissionPrompt(threads)};

            report.AppendLine();
            report.AppendLine($"--- \"{line}\" ({label})");
            foreach ((string v, IReadOnlyList<LongMemoryPassage> ps) in new[] {("W", words), ("T", threads)})
               foreach (LongMemoryPassage p in ps)
                  report.AppendLine($"    {v} recalled (day {p.Time}{(p.IsLatestOnThread ? ", LATEST" : "")}): " + string.Join(" / ", p.Lines.Select(l => (l.IsPlayer ? "P: " : "A: ") + l.Text)));

            for (var i = 0; i < repeats; i++)
               foreach (string version in versions)
               {
                  if (spent >= cap)
                  {
                     report.AppendLine($"(cap of ${cap:0.00} reached: stopped)");
                     goto done;
                  }

                  LlmResponse reply = await client.CompleteAsync(new LlmRequest {
                     SystemPrompt = prompts[version],
                     Messages = new List<LlmMessage> {new LlmMessage(MessageRole.User, line)},
                     Parameters = new LlmParameters {MaxTokens = 700, Creativity = 0.8f}
                  }, CancellationToken.None);

                  spent += reply?.Usage?.Cost ?? 0;
                  // 19/11/2026: the key's own $20 limit refused every call, and the run "passed" on blank replies at $0.
                  if (string.IsNullOrWhiteSpace(reply?.Content))
                  {
                     refused = reply?.ErrorMessage ?? "(no reply)";
                     report.AppendLine("(the provider gave no reply, stopped: " + refused + ")");
                     goto done;
                  }
                  string text = Dialogue(reply?.Content ?? reply?.ErrorMessage ?? "(no reply)");
                  string mark;
                  if (latestFact != null)
                  {
                     asked[version]++;
                     bool isLatest = latestFact.IsMatch(text);
                     bool isStale = !isLatest && staleFact != null && staleFact.IsMatch(text);
                     if (isLatest) latest[version]++;
                     if (isStale) stale[version]++;
                     mark = isLatest ? "LATEST" : isStale ? "STALE" : "misses";
                  }
                  else
                  {
                     ordinary[version]++;
                     bool drags = MissionWords.IsMatch(text);
                     if (drags) dragged[version]++;
                     mark = drags ? "DRAGS THE MISSION IN" : "stays on the moment";
                  }

                  report.AppendLine($"[{version}, {mark}] {text.Replace("\n", " ").Trim()}");
               }
         }

         done:
         report.AppendLine();
         report.AppendLine("=== The mission as it stands now (keyword): latest / stale / of ===");
         foreach (string v in versions) report.AppendLine($"{v}: {latest[v]} latest, {stale[v]} stale, of {asked[v]}");
         report.AppendLine("=== Ordinary line answered by dragging the mission in ===");
         foreach (string v in versions) report.AppendLine($"{v}: {dragged[v]} of {ordinary[v]}");
         report.AppendLine($"Spent: ${spent:0.0000} (OpenRouter's own figure).");
         report.AppendLine("Read the replies: a reply can name the old terms to correct them, and A may invent terms that were never set.");

         Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
         File.AppendAllText(ReportPath, report + Environment.NewLine);
         TestContext.Out.WriteLine(report.ToString());
         if (refused != null) Assert.Fail("The provider refused the calls: " + refused);
      }

      /// <summary>The three talks as the story archive keeps them, each line of terms buried among small talk.</summary>
      private static List<StoryArchiveRecord> Mission(bool withThreads)
      {
         var talks = new (string Episode, double Day, string Place, string[] Lines)[] {
            ("talk1", 860, "Aldric's hall in Ocs Hall", new[] {
               "P|You sent for me, my lord.",
               "A|I did. Sit. Wine?",
               "P|Gladly.",
               "A|There is a band of brigands camped in the woods near Varcheg. They take my tolls and my patience.",
               "P|How many?",
               "A|Thirty, perhaps forty. Rabble, but well fed on my coin.",
               "A|Clear them out and I will pay you one thousand denars. See it done before the harvest.",
               "P|One thousand, before the harvest. Done.",
               "A|My steward will see you fed before you ride.",
               "P|My thanks.",
               "A|And mind the bridge at Varcheg, it is rotten."
            }),
            ("talk2", 875, "the road outside Ocs Hall", new[] {
               "P|My lord, a word about the Varcheg bandits.",
               "A|You found them?",
               "P|They have left the woods. They moved up into the hills north of Varcheg and built a stockade.",
               "A|A stockade. Bold of them.",
               "A|Then burn it to the ground as well. I will raise your pay to fifteen hundred denars.",
               "P|Fifteen hundred. I will need more men.",
               "A|Take what you need. Just do not let the harvest rot while you dither.",
               "P|Understood.",
               "A|Ride well."
            }),
            ("talk3", 885, "Aldric's hall in Ocs Hall", new[] {
               "A|Come in, shut the door.",
               "P|You look grim, my lord.",
               "A|Those bandits near Varcheg have taken my nephew Bran. Seized him on the road three days ago.",
               "P|Alive?",
               "A|So their note says. Forget the stockade. Bring Bran back to me alive, that is all that matters now.",
               "A|Two thousand denars, and the grey from my own stable. And you have until the first snows, not the harvest.",
               "P|Two thousand and the grey, before the snows. I will bring him home.",
               "A|Do. His mother has not slept.",
               "P|Neither will I, then."
            })
         };

         var records = new List<StoryArchiveRecord>();
         foreach ((string episode, double day, string place, string[] lines) in talks)
            foreach (string l in lines)
            {
               bool player = l.StartsWith("P|", StringComparison.Ordinal);
               string text = l.Substring(2);
               records.Add(new StoryArchiveRecord {
                  Id = Guid.NewGuid().ToString("N"), Kind = StoryArchiveKind.Line, EpisodeId = episode, SceneKind = "conversation", Time = day,
                  PlaceName = place, SpeakerId = player ? "main_hero" : Aldric, SpeakerName = player ? "Garret" : "Count Aldric", IsPlayer = player,
                  Text = text, Audience = new List<string> {Aldric},
                  Threads = withThreads && text.IndexOf("Varcheg", StringComparison.OrdinalIgnoreCase) >= 0 ? new List<string> {Varcheg} : null
               });
            }

         return records;
      }

      private static string MissionPrompt(IReadOnlyList<LongMemoryPassage> passages)
      {
         var npc = new NpcProfile {
            Id = Aldric, Name = "Aldric", Clan = "dey Aldric", Faction = "Vlandia", Age = 48, IsFemale = false, ReputationWithPlayer = 20,
            Events = new List<NotableEvent> {
               new NotableEvent(820, NotableEventType.FirstMeeting, "Met the player at a feast in Ocs Hall."),
               new NotableEvent(860, NotableEventType.Agreement, "Asked the player to deal with the bandits near Varcheg for a reward. We have spoken of it again since.")
            }
         };
         var context = new EncounterContext {
            LeanLevel = LeanPromptLevel.Full,
            RecalledExchanges = passages?.Select(p => new RecalledExchange {
               When = $"{(int) (892 - p.Time)} days ago", Place = p.PlaceName,
               About = p.ThreadKeys != null && p.ThreadKeys.Contains(Varcheg) ? "Varcheg" : null, Latest = p.IsLatestOnThread,
               Lines = p.Lines.Select(l => new RecalledLine {Speaker = l.IsPlayer ? "PLAYER" : "YOU", Text = l.Text}).ToList()
            }).ToList()
         };

         return new PromptBuilder().BuildSystemPrompt(npc, new WorldState {CurrentDay = 892}, context);
      }

      private static string Dialogue(string raw)
      {
         Match m = Regex.Match(raw, @"\[DIALOGUE\](.*?)(\[/DIALOGUE\]|\[[A-Z_]+\]|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

         return m.Success ? m.Groups[1].Value.Trim() : raw.Trim();
      }
   }
}
