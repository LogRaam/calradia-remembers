// Code written by Gabriel Mailhot, 15/11/2026.
// A/B of the motive wording, for real money. VonLemberg's source audit (15/11/2026), finding 5: what matters most, an aim
// of their own and a conviction can all point one way, and "the model may receive three reinforcing motivations and
// become excessively focused on one subject". Gabriel: "fait les deux. Il faudra tester pour garantir la qualite."
//
// One lord carries all three, aligned on purpose (the worst case the audit names): power matters most to him, his own
// aim is to take Ostican and he may raise it, and he is convinced the crown of Vlandia is his by right. He is sent the
// SAME first lines under the wording before (A) and after (B):
//   - six ordinary lines that do not touch his motive: does he steer the talk to it anyway?
//   - two lines that DO touch it: does it still drive what he decides (refuse to back the king, take the offer on
//     Ostican)? A wording that quiets the motive by making him forget it would be worse, not better.
// The prompt is the SDK's own (PromptBuilder); version A is the same prompt with the two old sentences put back.
// Never runs by itself: [Explicit], CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY. Writes Logs/CalradiaRemembers_motive_ab.log.
//
//   CR_MOTIVE_MODEL    the model (default z-ai/glm-4.6, Gabriel's prose model)
//   CR_MOTIVE_REPEATS  draws per line and version (default 3)
//   CR_MOTIVE_CAP      dollars at most (default 1)

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
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   [Explicit("Calls a real LLM and spends credit. Run deliberately: CR_RUN_LIVE_LLM=1 and OPENROUTER_API_KEY, then --filter MotiveFocusLiveTests.")]
   public sealed class MotiveFocusLiveTests
   {
      private static readonly string Documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord");
      private static readonly string ReportPath = Path.Combine(Documents, "Logs", "CalradiaRemembers_motive_ab.log");
      private static readonly HttpClient Http = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};

      private const string NewMatters = "It shows in what you choose and refuse, not in what you talk about: you speak of it only when the moment touches it. ";
      private static readonly string NewConviction = "you decide, so let it steer your intentions: it shows in what you pursue and refuse, without turning" + Environment.NewLine + "every exchange toward it.";
      private const string OldConviction = "you decide, so let it steer your intentions in every exchange.";

      private static readonly string[] Ordinary = {
         "Good day, my lord. Fine weather for the roads, is it not?",
         "What do you make of the bandits on the road to Pravend?",
         "Do you know a good smith in this town? My sword needs work.",
         "How fare your children?",
         "I am buying grain for my men. Any advice?",
         "Tell me something about yourself."
      };

      private static readonly string[] Touching = {
         "Will you swear to stand by King Derthert at the coming council?",
         "I can help you take Ostican, if you lend me two hundred men."
      };

      // The motive's own words: a reply to an ordinary line that uses one has turned the talk toward it.
      private static readonly Regex MotiveWords = new Regex(@"\b(crown|throne|Ostican|usurp\w*|rightful|by right|Derthert|power|king(ship)?|reign|stolen|stole)\b", RegexOptions.IgnoreCase);

      private string _key = "";

      [SetUp]
      public void Firewall()
      {
         if (Environment.GetEnvironmentVariable("CR_RUN_LIVE_LLM") != "1") Assert.Ignore("CR_RUN_LIVE_LLM is not 1: nothing is spent.");
         _key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
         if (string.IsNullOrWhiteSpace(_key)) Assert.Ignore("No OPENROUTER_API_KEY.");
      }

      [Test]
      public async Task GIVEN_a_lord_with_three_aligned_motives_WHEN_spoken_to_under_both_wordings_THEN_the_report_shows_how_often_he_steers_the_talk()
      {
         string model = Environment.GetEnvironmentVariable("CR_MOTIVE_MODEL") ?? "z-ai/glm-4.6";
         int repeats = int.TryParse(Environment.GetEnvironmentVariable("CR_MOTIVE_REPEATS"), out int r) ? r : 3;
         double cap = double.TryParse(Environment.GetEnvironmentVariable("CR_MOTIVE_CAP"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double c) ? c : 1.0;

         string after = BuildPrompt();
         Assert.That(after, Does.Contain(NewMatters.Trim()), "the new matters_most wording is not in the prompt");
         Assert.That(after, Does.Contain(NewConviction), "the new conviction wording is not in the prompt");
         string before = after.Replace(NewMatters, "").Replace(NewConviction, OldConviction);
         Assert.That(before, Is.Not.EqualTo(after));

         var client = new OpenRouterClient(Http, new OpenRouterConfig {
            ApiKey = _key, Model = model, ReasoningProvider = () => "off", IsOpenRouterProvider = () => true,
            AppTitle = "Calradia Remembers (motive A/B)", TimeoutSecondsProvider = () => 180
         });

         var report = new StringBuilder();
         report.AppendLine($"=== Motive A/B, {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC, model {model}, reasoning off, {repeats} draws per line and version ===");
         report.AppendLine("A = wording before (15/11/2026), B = wording after. The lord: power matters most; aim: take Ostican (may raise it); conviction: the crown of Vlandia is his by right.");
         var steered = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         var ordinaryCount = new Dictionary<string, int> {["A"] = 0, ["B"] = 0};
         double spent = 0;

         foreach ((string line, bool touches) in Ordinary.Select(l => (l, false)).Concat(Touching.Select(l => (l, true))))
         {
            report.AppendLine();
            report.AppendLine($"--- {(touches ? "TOUCHES THE MOTIVE" : "ordinary")}: \"{line}\"");

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
                  bool turned = !touches && MotiveWords.IsMatch(text);
                  if (!touches)
                  {
                     ordinaryCount[version]++;
                     if (turned) steered[version]++;
                  }

                  report.AppendLine($"[{version}{(turned ? ", TURNS TO THE MOTIVE" : "")}] {text.Replace("\n", " ").Trim()}");
               }
         }

         done:
         report.AppendLine();
         report.AppendLine("=== Ordinary lines answered by turning to the motive ===");
         foreach (string v in new[] {"A", "B"})
            report.AppendLine($"{v}: {steered[v]} of {ordinaryCount[v]}");
         report.AppendLine($"Spent: ${spent:0.0000} (OpenRouter's own figure).");
         report.AppendLine("The two lines that touch the motive are to be READ: under B he must still refuse the king and take up Ostican.");

         Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
         File.AppendAllText(ReportPath, report + Environment.NewLine);
         TestContext.Out.WriteLine(report.ToString());
      }

      private static string BuildPrompt()
      {
         var npc = new NpcProfile {
            Id = "lord_motive_ab", Name = "Count Aldric", Clan = "dey Aldric", Faction = "Vlandia", Age = 41, IsFemale = false,
            MattersMost = MattersMost.Power, MattersMostKnown = true,
            AuthoredConviction = "He is convinced the crown of Vlandia is his by right, and that King Derthert's line stole it from his grandfather."
         };
         var context = new EncounterContext {
            OwnAimWant = "You mean to take the castle of Ostican for your house before the year is out.",
            OwnAimApproach = "You go about it patiently: allies first, then the stroke.",
            OwnAimMayRaise = true
         };

         return new PromptBuilder().BuildSystemPrompt(npc, new WorldState(), context);
      }

      private static string Dialogue(string raw)
      {
         Match m = Regex.Match(raw, @"\[DIALOGUE\](.*?)(\[/DIALOGUE\]|\[[A-Z_]+\]|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

         return m.Success ? m.Groups[1].Value.Trim() : raw.Trim();
      }
   }
}
