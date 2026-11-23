// Code written by Gabriel Mailhot, 22/11/2026.
// A/B of the perception rule, for real money. whiskeypatt (Discord 21/11/2026): "i rejoined the camp, tired, after
// killing 31 men. then went to greet lord A \"Hello A\" .. he in his reply mentioned 'so 31 men you killed was it?'".
// Gabriel (22/11/2026) kept the asterisks and had the prompt say the character perceives only what is seen or heard.
// The same first lines are sent under the full prompt without the rule (A) and with it (B):
//   - four lines carrying narration the lord cannot know (a past deed, a private thought, a grief, a secret act):
//     does he quote or answer it?
//   - four visible acts (a wrist taken, a sword on the table, blood on a sleeve, a letter held out): does he still
//     engage with them? A rule that silences what shows would break the captive and intimacy scenes.
// Never runs by itself: [Explicit], CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY. Writes Logs/CalradiaRemembers_perception_ab.log.
//
//   CR_PERCEPTION_MODEL    the model (default z-ai/glm-4.6, Gabriel's prose model)
//   CR_PERCEPTION_REPEATS  draws per line and version (default 3)
//   CR_PERCEPTION_CAP      dollars at most (default 1)

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   [Explicit("Calls a real LLM and spends credit. Run deliberately: CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY, then --filter NarrationPerceptionLiveTests.")]
   public sealed class NarrationPerceptionLiveTests
   {
      private static readonly string Documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord");
      private static readonly string ReportPath = Path.Combine(Documents, "Logs", "CalradiaRemembers_perception_ab.log");
      private static readonly HttpClient Http = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};

      private const string RuleStart = "WHAT YOU PERCEIVE: only what can be SEEN or HEARD in this room now.";
      private const string RuleEnd = "those words are what they say; the rest around them is narration.";
      private static readonly string QuotedNew = "they SAY, unless the message also holds words in quotation marks: then only the quoted words are" + Environment.NewLine + "spoken, and the rest is narration. Keep the two apart.";
      private const string QuotedOld = "they SAY; keep the two apart.";

      // A line, whether the lord may know its narration, and the words that show he took it up.
      private static readonly (string Line, bool Visible, Regex Mark)[] Lines = {
         ("I rejoined the camp, tired, after killing 31 men. Then I went to greet you. \"Hello, my lord.\"", false, new Regex(@"\b(31|thirty[- ]one|killed|killing|slew|slain|slaughter\w*)\b", RegexOptions.IgnoreCase)),
         ("*I wonder whether you are lying to me about the grain.* \"How goes the harvest, my lord?\"", false, new Regex(@"\b(lying|lie|lies|liar|honest\w*|truth\w*|suspect\w*|doubt\w*|trust)\b", RegexOptions.IgnoreCase)),
         ("*I think of my brother, drowned off the coast last spring.* \"Good evening, my lord.\"", false, new Regex(@"\b(brother|drown\w*|coast|grief|griev\w*|mourn\w*|loss)\b", RegexOptions.IgnoreCase)),
         ("*Earlier today I secretly paid a man to spy on your castle.* \"May I sit?\"", false, new Regex(@"\b(spy|spies|spying|paid|informer|watch\w*)\b", RegexOptions.IgnoreCase)),
         ("*I take you by the wrist.*", true, new Regex(@"\b(wrist|hand|grip|grasp|arm|touch\w*|let go|release|fingers|hold)\b", RegexOptions.IgnoreCase)),
         ("*I draw my sword and lay it on the table between us.*", true, new Regex(@"\b(sword|blade|steel|table|weapon|hilt)\b", RegexOptions.IgnoreCase)),
         ("*Blood still drips from my sleeve as I sit down.* \"Good evening.\"", true, new Regex(@"\b(blood\w*|wound\w*|hurt|sleeve|injur\w*|bleed\w*|arm)\b", RegexOptions.IgnoreCase)),
         ("*I hold out a sealed letter.*", true, new Regex(@"\b(letter|seal\w*|parchment|message)\b", RegexOptions.IgnoreCase))
      };

      private string _key = "";

      [SetUp]
      public void Firewall()
      {
         if (Environment.GetEnvironmentVariable("CR_RUN_LIVE_LLM") != "1") Assert.Ignore("CR_RUN_LIVE_LLM is not 1: nothing is spent.");
         _key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
         if (string.IsNullOrWhiteSpace(_key)) Assert.Ignore("No OPENROUTER_API_KEY.");
      }

      [Test]
      public async Task GIVEN_narration_the_lord_cannot_see_and_acts_he_can_WHEN_sent_under_both_prompts_THEN_the_report_counts_both()
      {
         string model = Environment.GetEnvironmentVariable("CR_PERCEPTION_MODEL") ?? "z-ai/glm-4.6";
         int repeats = int.TryParse(Environment.GetEnvironmentVariable("CR_PERCEPTION_REPEATS"), out int r) ? r : 3;
         double cap = double.TryParse(Environment.GetEnvironmentVariable("CR_PERCEPTION_CAP"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double c) ? c : 1.0;

         string after = BuildPrompt();
         int start = after.IndexOf(RuleStart, StringComparison.Ordinal), end = after.IndexOf(RuleEnd, StringComparison.Ordinal);
         Assert.That(start >= 0 && end > start, "the perception rule is not in the prompt");
         string before = after.Remove(start, end + RuleEnd.Length - start).Replace("said so." + Environment.NewLine + Environment.NewLine, "said so." + Environment.NewLine)
                                .Replace(QuotedNew, QuotedOld);
         Assert.That(after, Does.Contain(QuotedNew), "the amended speech sentence is not in the prompt");

         var client = new OpenRouterClient(Http, new OpenRouterConfig {
            ApiKey = _key, Model = model, ReasoningProvider = () => "off", IsOpenRouterProvider = () => true,
            AppTitle = "Calradia Remembers (perception A/B)", TimeoutSecondsProvider = () => 180
         });

         var report = new StringBuilder();
         report.AppendLine($"=== Perception A/B, {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC, model {model}, reasoning off, {repeats} draws per line and version ===");
         report.AppendLine("A = full prompt without the rule, B = with it. Unseen lines: a MARK means he took up what he cannot know. Visible lines: a MARK means he engaged.");
         var leaked = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var engaged = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var unseenCount = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var visibleCount = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         double spent = 0;

         foreach ((string line, bool visible, Regex mark) in Lines)
         {
            report.AppendLine();
            report.AppendLine($"--- {(visible ? "VISIBLE" : "unseen")}: {line}");

            for (var i = 0; i < repeats; i++)
               foreach ((string version, string system) in new[] {("A", before), ("B", after)})
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
                  string text = Dialogue(reply?.Content ?? reply?.ErrorMessage ?? "(no reply)");
                  bool hit = mark.IsMatch(text);
                  if (visible)
                  {
                     visibleCount[version]++;
                     if (hit) engaged[version]++;
                  }
                  else
                  {
                     unseenCount[version]++;
                     if (hit) leaked[version]++;
                  }

                  report.AppendLine($"[{version}{(hit ? ", MARK" : "")}] {text.Replace("\n", " ").Trim()}");
               }
         }

         done:
         report.AppendLine();
         report.AppendLine("=== Unseen narration taken up (lower is better) ===");
         foreach (string v in new[] {"A", "B"}) report.AppendLine($"{v}: {leaked[v]} of {unseenCount[v]}");
         report.AppendLine("=== Visible acts engaged with (must hold) ===");
         foreach (string v in new[] {"A", "B"}) report.AppendLine($"{v}: {engaged[v]} of {visibleCount[v]}");
         report.AppendLine($"Spent: ${spent:0.0000} (OpenRouter's own figure). Keyword marks are a first cut: READ the replies.");

         Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
         File.AppendAllText(ReportPath, report + Environment.NewLine);
         TestContext.Out.WriteLine(report.ToString());
      }

      private static string BuildPrompt()
      {
         var npc = new NpcProfile {Id = "lord_perception_ab", Name = "Count Aldric", Clan = "dey Aldric", Faction = "Vlandia", Age = 41, IsFemale = false};

         return new PromptBuilder().BuildSystemPrompt(npc, new WorldState(), new EncounterContext());
      }

      private static string Dialogue(string raw)
      {
         Match m = Regex.Match(raw, @"\[DIALOGUE\](.*?)(\[/DIALOGUE\]|\[[A-Z_]+\]|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

         return m.Success ? m.Groups[1].Value.Trim() : raw.Trim();
      }
   }
}
