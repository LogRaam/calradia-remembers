// Code written by Gabriel Mailhot, 26/11/2026.
// A group conversation simulated end to end, to read. Gabriel (26/11/2026): "j'aimerais simuler une conversation a 4 sur
// plusieurs beats. P-e 5-6 et voir comment la conversation evolue. Et j'aimerais aussi pouvoir lire les repliques pour
// 'vivre' la conversation." The player's six lines are scripted; after each, the REAL floor judge (SDK FloorJudge, a cheap
// model) reads every listener, the REAL referee (TurnArbiter) picks who speaks, and the player's prose model writes the
// chosen character's line and the reactions the judge named (Gabriel: the prose model writes the gestures). The characters'
// prompts here are short sheets, not the game's full prompt: this measures the DYNAMICS of the floor, not CR's prose.
//
// Not a test of anything: [Explicit], never run by the suite. It spends a few cents. Opt-in through the SDK live lane's
// git-ignored settings (LLM Roleplay Service/NpcMemoryService.LiveLlmTests/livellm.settings.json, "enabled": true, an
// OpenRouter key). It writes the transcript to a markdown file and prints its path and contents.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CalradiaRemembers.Logic.Conversation;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   [Explicit("Simulates a group conversation with real models and spends a few cents. Run deliberately.")]
   public sealed class GroupConversationSimulation
   {
      private sealed class Character
      {
         public string Name = "";
         public string Persona = "";
         public string Secret = "";
         public int Standing;
         public bool Bold;
         public bool Timid;
         public string[] Aliases = Array.Empty<string>();
         public List<string> Ties = new();
         public FloorJudgePresence Presence = new();
      }

      private sealed class SceneLine
      {
         public string Speaker = "";
         public string To = "";
         public string Text = "";
         public bool IsNarration;
         public bool IsPlayer;
      }

      private const string Player = "Arwa";
      private const int PlayerStanding = 2;

      private static readonly string[] PlayerBeats = {
         "Garios|The camp stores were raided last night. Two loaves and a whole ham. Who keeps watch on them?",
         "everyone|Someone in this camp is a thief. I want a name before supper.",
         "Ira|Your hired blades were near the stores at midnight, weren't they, Ira?",
         "Bryn|Bryn, you had the night watch. What did you see?",
         "Garios|Your squire has grease on his sleeve, Garios. Hams are greasy.",
         "everyone|Well? Do I hang someone at dawn, or will the guilty one speak?"
      };

      private static List<Character> Cast()
      {
         var garios = new Character {
            Name = "Garios", Standing = 3, Aliases = new[] {"Garios", "my lord"},
            Persona = "A proud Vlandian lord of an old house, quick to take offence, jealous of his name and of those who serve him. He commands the camp's foot.",
            Presence = new FloorJudgePresence {Name = "Garios", Station = "a lord of an old Vlandian house", Power = "a great house, 300 men under arms"}
         };
         var ira = new Character {
            Name = "Ira", Standing = 1, Bold = true, Aliases = new[] {"Ira", "captain", "hired", "sellsword", "mercenar"},
            Persona = "A blunt mercenary captain who fights for pay, scorns noble airs and never lets a slight pass. Her company camps beside the stores.",
            Presence = new FloorJudgePresence {Name = "Ira", Station = "a mercenary captain", Power = "sixty hired blades", IsFemale = true}
         };
         var bryn = new Character {
            Name = "Bryn", Standing = 0, Timid = true, Aliases = new[] {"Bryn", "squire"},
            Persona = "A timid squire of sixteen in Garios's service, who keeps quiet before his betters and fears his lord's temper.",
            Secret = "Last night, hungry after two days on half rations, you stole two loaves and a ham from the camp stores while on watch. Nobody knows. You are terrified of being found out, and of your lord's shame.",
            Presence = new FloorJudgePresence {Name = "Bryn", Station = "a squire in Garios's service", Power = "none"}
         };
         garios.Ties.AddRange(new[] {"Arwa: the commander you ride with, useful but beneath your blood", "Ira: a sellsword you despise", "Bryn: your own squire, whose conduct reflects on you"});
         ira.Ties.AddRange(new[] {"Arwa: your employer, who pays well", "Garios: a lord who looks down on you; you return the favour", "Bryn: a lord's boy, beneath notice"});
         bryn.Ties.AddRange(new[] {"Arwa: a commander you admire and fear", "Garios: your lord, whom you must never shame", "Ira: a frightening mercenary"});

         return new List<Character> {garios, ira, bryn};
      }

      [Test]
      public Task GIVEN_four_people_and_six_player_lines_WHEN_the_floor_is_run_THEN_the_transcript_is_written_to_read()
         => RunAsync(false, PlayerBeats.Length);

      // Gabriel (26/11/2026): "lance uniquement la premiere phrase de Arwa. Ensuite, laisse les modeles se parler entre eux
      // pendant 6 beats.. Arwa compris." Only Arwa's first line is scripted; whenever the floor returns to the player, a model
      // plays Arwa, from a sheet like the others'.
      [Test]
      public Task GIVEN_only_the_first_player_line_WHEN_a_model_plays_the_player_for_six_more_beats_THEN_the_transcript_is_written_to_read()
         => RunAsync(true, 7);

      private static Character PlayerCharacter() => new() {
         Name = Player, Standing = PlayerStanding,
         Persona = "Arwa, a commander of the realm who leads this camp. She is investigating last night's theft from the stores: firm and fair, she wants the truth before supper and has no taste for needless cruelty.",
         Ties = new List<string> {"Garios: a proud lord under your command, useful and touchy", "Ira: the captain of the mercenaries you pay", "Bryn: Garios's young squire, who had the night watch"}
      };

      private async Task RunAsync(bool aiPlayer, int beats)
      {
         JsonElement settings = LoadSettingsOrIgnore(out string key);
         string proseModel = Str(settings, "model", "x-ai/grok-4.20");
         string judgeModel = Str(settings, "floorJudgeModel", "deepseek/deepseek-v4.1-flash");
         string judgeReasoning = Str(settings, "floorJudgeReasoning", "off");
         string proseReasoning = Str(settings, "reasoningEffort", "");

         using var http = new HttpClient {Timeout = TimeSpan.FromSeconds(90)};
         var judge = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = judgeModel, ReasoningProvider = () => judgeReasoning});
         var prose = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = proseModel, ReasoningProvider = () => string.IsNullOrWhiteSpace(proseReasoning) ? null : proseReasoning});

         List<Character> cast = Cast();
         var room = new List<FloorJudgePresence> {new() {Name = Player, Station = "a commander of the realm", Power = "a rising house, 120 men", IsFemale = true}};
         room.AddRange(cast.Select(c => c.Presence));
         var scene = new List<SceneLine>();
         var debug = new StringBuilder();
         var random = new Random(26112026);
         double cost = 0;
         int judgeCalls = 0, proseCalls = 0;
         string cutOff = null;

         Character arwa = PlayerCharacter();
         for (var beat = 0; beat < beats; beat++)
         {
            if (beat == 0 || !aiPlayer)
            {
               string[] parts = PlayerBeats[beat].Split('|');
               scene.Add(new SceneLine {Speaker = Player, To = parts[0], Text = parts[1], IsPlayer = true});
            }
            else
            {
               (string to, string text) = ParseLine(await Prose(prose, LinePrompt(arwa, cast, room, scene, null), 220));
               proseCalls++;
               scene.Add(new SceneLine {Speaker = Player, To = to, Text = text, IsPlayer = true});
            }

            debug.AppendLine($"\n**Beat {beat + 1}**");
            var npcLines = 0;

            while (true)
            {
               SceneLine last = scene.Last(l => !l.IsNarration);
               int lastStanding = last.IsPlayer ? PlayerStanding : cast.First(c => c.Name == last.Speaker).Standing;

               // Every listener is judged at once, as in play.
               List<Character> listeners = cast.Where(c => c.Name != last.Speaker).ToList();
               FloorJudgeVerdict[] verdicts = await Task.WhenAll(listeners.Select(async c =>
               {
                  LlmResponse r = await judge.CompleteAsync(new LlmRequest {
                     SystemPrompt = FloorJudge.StablePrompt(new FloorJudgeListener {Name = c.Name, Persona = (c.Persona + " " + c.Secret).Trim(), Ties = c.Ties, Room = room}),
                     Messages = new[] {new LlmMessage(MessageRole.User, FloorJudge.TurnPrompt(c.Name, JudgeScene(scene)))},
                     Parameters = new LlmParameters {MaxTokens = 60, Creativity = 0.3f, AllowTruncationRetry = false}
                  });
                  lock (debug) { cost += r.Usage?.Cost ?? 0; judgeCalls++; }

                  return FloorJudge.Parse(r.Content);
               }));

               // Who the line was put to by name, read by the game's own detector (a vocative, "*turns to Bryn*"): a speaker
               // who declares "to: Garios" and then turns to Bryn has asked Bryn (second run of 26/11/2026 hung on it).
               string calledOn = Logic.AddressedSpeakerDetector.Detect(last.Text, listeners
                  .Select(c => new Logic.AddressedSpeakerDetector.Participant(c.Name, new HashSet<string>(new[] {c.Name}, StringComparer.OrdinalIgnoreCase))).ToList());
               var bids = new List<FloorBid>();
               for (var i = 0; i < listeners.Count; i++)
               {
                  Character c = listeners[i];
                  bool askedByName = string.Equals(calledOn, c.Name, StringComparison.OrdinalIgnoreCase);
                  bool addressed = askedByName || string.Equals(last.To, c.Name, StringComparison.OrdinalIgnoreCase);
                  bool named = c.Aliases.Any(a => last.Text.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
                  bids.Add(new FloorBid {
                     Id = c.Name, Intent = verdicts[i].Intent, Reaction = verdicts[i].Reaction, Addressed = addressed,
                     AskedByName = askedByName, Targeted = !addressed && named,
                     Standing = c.Standing, TurnsSilent = TurnsSilent(scene, c.Name), WasCutOff = cutOff == c.Name,
                     LastSpeakerStanding = lastStanding, Bold = c.Bold, Timid = c.Timid, TieBreak = random.Next(1000)
                  });
               }
               bids.Add(new FloorBid {Id = last.Speaker == Player ? "_player" : last.Speaker, SpokeLast = true});

               FloorDecision d = TurnArbiter.Decide(new FloorRound {Bids = bids, NpcLinesSincePlayer = npcLines, PlayerSpokeLast = last.IsPlayer});
               debug.AppendLine($"- after {last.Speaker}: " + string.Join("; ", bids.Where(b => !b.SpokeLast).Select(b =>
                  $"{b.Id} {b.Intent}{(TurnArbiter.EffectiveIntent(b) != b.Intent ? "->" + TurnArbiter.EffectiveIntent(b) : "")}{(b.Addressed ? " [to]" : "")}{(b.Targeted ? " [about]" : "")}{(b.Reaction != null ? " (" + b.Reaction + ")" : "")}"))
                  + $" => {(d.Outcome == FloorOutcome.Speak ? d.SpeakerId + (d.CutOffId != null ? " cuts off " + d.CutOffId : "") : d.Outcome.ToString())}");
               cutOff = d.CutOffId;

               List<string> shown = d.ReactorIds.Where(id => id != d.SpeakerId).Take(2).ToList();
               if (shown.Count > 0)
               {
                  string cues = string.Join("; ", shown.Select(id => $"{id}: {bids.First(b => b.Id == id).Reaction}"));
                  string narration = await Prose(prose, NarrationPrompt(room, scene, cues), 90);
                  proseCalls++;
                  // The first line only: the prose model sometimes trails a copy or a stray "\confidence{70}".
                  string first = (narration ?? "").Replace("\r", "").Split('\n').Select(Clean).FirstOrDefault(l => l.Length > 0) ?? "";
                  if (first.Length > 0) scene.Add(new SceneLine {Speaker = "", Text = first, IsNarration = true});
               }

               if (d.Outcome != FloorOutcome.Speak) break;

               Character speaker = cast.First(c => c.Name == d.SpeakerId);
               string reply = await Prose(prose, LinePrompt(speaker, cast, room, scene, d.CutOffId), 220);
               proseCalls++;
               (string to, string text) = ParseLine(reply);
               scene.Add(new SceneLine {Speaker = speaker.Name, To = to, Text = text});
               npcLines++;
            }
         }

         string transcript = Render(scene, judgeModel, proseModel, judgeCalls, proseCalls, cost, debug.ToString(), aiPlayer);
         string path = Path.Combine(Path.GetTempPath(), $"cr_group_conversation_{DateTime.Now:yyyyMMdd_HHmmss}.md");
         File.WriteAllText(path, transcript);
         TestContext.Out.WriteLine($"[GROUP SIM] written to {path}");
         TestContext.Out.WriteLine(transcript);
      }

      #region private

      private static List<FloorJudgeLine> JudgeScene(List<SceneLine> scene)
         => scene.Where(l => !l.IsNarration).Select(l => new FloorJudgeLine {Speaker = l.Speaker, To = l.To, Text = l.Text}).ToList();

      private static int TurnsSilent(List<SceneLine> scene, string name)
      {
         var n = 0;
         for (int i = scene.Count - 1; i >= 0; i--)
         {
            if (scene[i].IsNarration) continue;
            if (scene[i].Speaker == name) return n;
            n++;
         }

         return n;
      }

      private static string SceneText(List<SceneLine> scene)
         => string.Join("\n", scene.Select(l => l.IsNarration ? l.Text : $"{l.Speaker}{(string.IsNullOrWhiteSpace(l.To) ? "" : " (to " + l.To + ")")}: {l.Text}"));

      private static (string System, string User) LinePrompt(Character me, List<Character> cast, List<FloorJudgePresence> room, List<SceneLine> scene, string cutOff)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"You are {me.Name}, in a war camp in a medieval world. {me.Persona}");
         if (!string.IsNullOrWhiteSpace(me.Secret)) sb.AppendLine($"Known only to you: {me.Secret}");
         sb.AppendLine("Who is here: " + string.Join("; ", room.Select(p => $"{p.Name} ({(p.IsFemale ? "a woman" : "a man")}, {p.Station})")));
         foreach (string tie in me.Ties) sb.AppendLine("- " + tie);
         sb.AppendLine();
         sb.AppendLine("Write ONLY your next line in this group conversation, in character: one to three sentences of speech, an *action* in");
         sb.AppendLine("asterisks allowed. Speak only for yourself. You never lie outright: when you will not tell the truth, evade, deflect or fall silent.");
         sb.AppendLine("Answer the moment as it stands now: you may turn to anyone present, not only the last speaker.");
         sb.AppendLine("Never repeat what you have already said or demanded: move the scene forward, change your approach, concede, or act.");
         if (cutOff != null) sb.AppendLine($"You are cutting in before {cutOff} could answer.");
         sb.AppendLine("Format: first line `to: <a name, or everyone>`, then your line.");

         return (sb.ToString(), "THE SCENE SO FAR:\n" + SceneText(scene) + $"\n\nYour line, {me.Name}:");
      }

      private static (string System, string User) NarrationPrompt(List<FloorJudgePresence> room, List<SceneLine> scene, string cues)
      {
         string system = "You write ONE short line of narration for a scene in a medieval world, present tense, in asterisks, no dialogue. "
                       + "Show each named reaction as a small, specific gesture of that person, different from any gesture already in the scene. "
                       + "Show only what is listed: no other character, no thoughts, no explanation of what the reaction means. "
                       + "Who is here (use the right pronouns): " + string.Join("; ", room.Select(p => $"{p.Name}, {(p.IsFemale ? "a woman (she/her)" : "a man (he/him)")}")) + ".";

         return (system, "THE SCENE SO FAR:\n" + SceneText(scene) + $"\n\nREACTIONS TO SHOW: {cues}\nThe narration line:");
      }

      private static async Task<string> Prose(OpenRouterClient client, (string System, string User) prompt, int maxTokens)
      {
         LlmResponse r = await client.CompleteAsync(new LlmRequest {
            SystemPrompt = prompt.System, Messages = new[] {new LlmMessage(MessageRole.User, prompt.User)},
            Parameters = new LlmParameters {MaxTokens = maxTokens, Creativity = 0.8f}
         });

         return r.IsSuccess ? r.Content?.Trim() ?? "" : $"(call failed: {r.ErrorMessage})";
      }

      private static (string To, string Text) ParseLine(string reply)
      {
         string[] lines = (reply ?? "").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToArray();
         if (lines.Length > 0 && lines[0].TrimStart('`', ' ').StartsWith("to:", StringComparison.OrdinalIgnoreCase))
            return (lines[0].TrimStart('`', ' ').Substring(3).Trim(' ', '`', '<', '>'), Clean(string.Join(" ", lines.Skip(1))));

         return ("", Clean(string.Join(" ", lines)));
      }

      // Models write em dashes; the repositories refuse them (git hooks), and the transcript may be kept as a design record.
      private static string Clean(string s) => (s ?? "").Replace("\u2014", ", ").Replace("\u2013", "-").Trim().Trim('`').Trim();

      private static string Render(List<SceneLine> scene, string judgeModel, string proseModel, int judgeCalls, int proseCalls, double cost, string debug, bool aiPlayer)
      {
         var sb = new StringBuilder();
         sb.AppendLine("# A group conversation, simulated");
         sb.AppendLine($"Judge: {judgeModel} ({judgeCalls} calls). Prose: {proseModel} ({proseCalls} calls). Judge cost: ${cost:0.0000}.");
         sb.AppendLine(aiPlayer
            ? "Arwa (the player: her first line scripted, then played by the prose model whenever the floor returns to her), Garios (a proud lord), Ira (a bold mercenary captain), Bryn (a timid squire, the thief)."
            : "Arwa (the player, scripted), Garios (a proud lord), Ira (a bold mercenary captain), Bryn (a timid squire, the thief).");
         sb.AppendLine();
         sb.AppendLine("## The scene");
         foreach (SceneLine l in scene)
         {
            if (l.IsNarration) sb.AppendLine($"> {l.Text}");
            else sb.AppendLine($"**{l.Speaker}**{(string.IsNullOrWhiteSpace(l.To) ? "" : " *(to " + l.To + ")*")}: {l.Text}");
            sb.AppendLine();
         }

         sb.AppendLine("## Why each turn went as it did (judge -> referee)");
         sb.AppendLine(debug);

         return sb.ToString();
      }

      private static JsonElement LoadSettingsOrIgnore(out string key)
      {
         key = "";
         string dir = AppContext.BaseDirectory;
         string file = null;
         for (var i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
         {
            string candidate = Path.Combine(dir, "LLM Roleplay Service", "NpcMemoryService.LiveLlmTests", "livellm.settings.json");
            if (File.Exists(candidate)) { file = candidate; break; }
         }

         if (file == null) Assert.Ignore("No live settings (LLM Roleplay Service/NpcMemoryService.LiveLlmTests/livellm.settings.json).");
         JsonElement root = JsonDocument.Parse(File.ReadAllText(file!), new JsonDocumentOptions {CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true}).RootElement;
         if (!(root.TryGetProperty("enabled", out JsonElement on) && on.ValueKind == JsonValueKind.True)) Assert.Ignore("Live settings are not enabled.");

         key = Str(root, "apiKey", "");
         if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
         if (string.IsNullOrWhiteSpace(key)) Assert.Ignore("No OpenRouter key.");

         return root;
      }

      private static string Str(JsonElement root, string name, string fallback)
         => root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()! : fallback;

      #endregion
   }
}
