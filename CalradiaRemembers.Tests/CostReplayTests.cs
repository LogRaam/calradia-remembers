// Code written by Gabriel Mailhot, 24/10/2026.

// The cost bench replayed outside the game (Gabriel, 24/10/2026: "ça te donnerait la possibilité de vérifier des choses
// par toi-même. On pourrait même en faire un skill."). cr.cost_bench captures every request its prose rows send; the
// replay sends them again to another model through the same client, swapping in that model's own replies where the
// conversation carried the captured model's words. A replay that does not send what the game sent measures its own
// construction, and its tables would go on the Nexus page as if they were the mod's: these pin that the replay sends the
// game's bytes, that each model answers over its own words, and that the interpreter reads what the game reads.

#region

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CostReplayTests
   {
      private const string Stable = "You are Lucon, Emperor of the Northern Empire.\n";
      private const string Scene = "*I look up from the dispatch.* Arwa.";
      private const string FirstReply = "*I nod.* Diathma is mine; you rode it in peace.";

      // STAKE: a replayed model must answer over its OWN words. Without knowing which lines were the captured model's, the
      // replay would put Grok's scene in DeepSeek's mouth and score DeepSeek's second reply on Grok's first.
      [Test]
      public void GIVEN_a_request_carrying_an_earlier_reply_WHEN_tagged_THEN_that_line_names_its_step_and_the_others_none()
      {
         List<CaptureMessage> tagged = CostReplayPolicy.Tag(new[] {
            new LlmMessage(MessageRole.Assistant, Scene),
            new LlmMessage(MessageRole.User, "Well met."),
            new LlmMessage(MessageRole.Assistant, "*I set my cup down.* Quiet enough."),
            new LlmMessage(MessageRole.User, "And the harvest?")
         }, new[] {Scene});

         tagged.Select(m => m.LiveStep).Should().Equal(0, -1, -1, -1);
         tagged.Select(m => m.IsPlayer).Should().Equal(false, true, false, true);
      }

      // STAKE: the synthetic history is the same for every model by design (Gabriel, 24/10/2026); only the captured model's
      // own replies are swapped, so every model is still compared on the same input.
      [Test]
      public void GIVEN_a_captured_row_WHEN_rebuilt_for_another_model_THEN_its_own_replies_replace_the_captured_ones_and_nothing_else()
      {
         CaptureRow row = Row();

         List<LlmMessage> rebuilt = CostReplayPolicy.Rebuild(row, 2, new[] {"*Scene B.*", "*Reply B.*"});

         rebuilt.Select(m => m.Content).Should().Equal("*Scene B.*", "Well met. I have just ridden in from Diatma.", "*Reply B.*", "I bought two dromons.");
      }

      // STAKE: a model that failed one reply must still be asked the next: the captured words stand in, and the
      // conversation reads, instead of the replay sending an empty line the game never sends.
      [Test]
      public void GIVEN_a_replay_with_no_reply_at_a_step_WHEN_rebuilt_THEN_the_captured_reply_stands_in()
      {
         List<LlmMessage> rebuilt = CostReplayPolicy.Rebuild(Row(), 2, new[] {"*Scene B.*", ""});

         rebuilt[2].Content.Should().Be(FirstReply);
      }

      // STAKE: the conversation keeps the dialogue the chat's parser reads, on one line, as the bench records it; a raw
      // Standard reply with its tags would never be found again in the next request.
      [Test]
      public void GIVEN_a_raw_reply_WHEN_cleaned_THEN_it_reads_as_the_bench_kept_it()
      {
         CostReplayPolicy.Clean("*I nod.*\nWell met.").Should().Be("*I nod.* Well met.");
         CostReplayPolicy.Clean("[DIALOGUE]\n*I nod.* Well met.\n[/DIALOGUE]\n[ACTION]\ntype: none\n[/ACTION]").Should().NotContain("[ACTION]");
      }

      // STAKE: the capture is written by the game and read by the replay in another process; a field lost on the way
      // (which line was live, the phase) would silently change what is replayed.
      [Test]
      public void GIVEN_a_capture_WHEN_written_and_read_back_THEN_it_is_the_same_capture()
      {
         var file = new CostCaptureFile {Version = "v2.7.20", Speaker = "Lucon", Rows = {Row()}};

         CostCaptureFile back = CostCaptureFile.FromJson(file.ToJson());

         back.Rows.Single().Steps.Select(s => s.Phase).Should().Equal(CostPhase.Opener, CostPhase.Prose, CostPhase.Prose);
         back.Rows.Single().Steps[2].Messages.Select(m => m.LiveStep).Should().Equal(0, -1, 1, -1);
         CostCaptureFile.FromJson("not json").Should().BeNull();
      }

      // STAKE (the one that makes the replay worth anything): replayed with the captured model and the captured replies,
      // the replay must send the very bytes the game sent, call by call. If it does not, every table it writes measures the
      // replay rather than the mod.
      [Test]
      public async Task GIVEN_a_conversation_captured_from_the_client_WHEN_replayed_with_the_same_model_and_replies_THEN_every_body_is_the_same()
      {
         CaptureRow captured = await CaptureThroughTheClient();
         var replies = new Queue<string>(captured.Steps.Select(s => s.Reply));
         var handler = new ScriptedHandler(() => replies.Dequeue());

         await CostReplayRunner.ReplayAsync(captured, new CostScenario {Name = "Grok", Pass = CostPass.Prose, Model = "x-ai/grok-4.20"},
            Client(handler), new CostRecorder(), new StringBuilder(), 0);

         handler.Bodies.Should().Equal(captured.Steps.Select(s => s.PayloadJson));
      }

      // STAKE: outside the game the interpreter reads what cr.cost_bench reads in it (the ordinary replies and the whole
      // catalog), or an offline score could not stand beside an in-game one.
      [Test]
      public void GIVEN_an_interpret_row_without_a_sample_WHEN_its_readings_are_listed_THEN_they_are_the_ordinary_replies_and_the_whole_catalog()
      {
         var readings = InterpretCases.ForRow(new CostScenario {Pass = CostPass.Interpret}, ActionBenchCatalog.All);

         readings.Should().HaveCount(InterpretCases.OrdinaryReplies.Count + ActionBenchCatalog.All.Count);
         readings.Count(r => r.Difficulty == InterpretDifficulty.NoAction).Should().Be(InterpretCases.OrdinaryReplies.Count);
      }

      private static CaptureRow Row()
         => new CaptureRow {
            Name = "Grok", Model = "x-ai/grok-4.20", Pass = CostPass.Prose,
            Steps = {
               new CaptureStep {Phase = CostPhase.Opener, Reply = Scene, Messages = {new CaptureMessage {IsPlayer = true, Content = "[cue]"}}},
               new CaptureStep {
                  Phase = CostPhase.Prose, Turn = 1, Reply = FirstReply,
                  Messages = {new CaptureMessage {Content = Scene, LiveStep = 0}, new CaptureMessage {IsPlayer = true, Content = "Well met. I have just ridden in from Diatma."}}
               },
               new CaptureStep {
                  Phase = CostPhase.Prose, Turn = 2, Reply = "*I frown.*",
                  Messages = {
                     new CaptureMessage {Content = Scene, LiveStep = 0},
                     new CaptureMessage {IsPlayer = true, Content = "Well met. I have just ridden in from Diatma."},
                     new CaptureMessage {Content = FirstReply, LiveStep = 1},
                     new CaptureMessage {IsPlayer = true, Content = "I bought two dromons."}
                  }
               }
            }
         };

      // The game's road in miniature: requests through the real client, its wire snapshots captured as the bench captures
      // them, each reply then carried by the next request.
      private static async Task<CaptureRow> CaptureThroughTheClient()
      {
         var row = new CaptureRow {Name = "Grok", Model = "x-ai/grok-4.20", Pass = CostPass.Prose};
         var said = new[] {Scene, FirstReply, "*I frown.* Dromons."};
         var turn = 0;
         var watch = new Watch(row);
         OpenRouterClient client = Client(new ScriptedHandler(() => said[turn]));
         client.WireObserver = watch;
         var history = new List<LlmMessage>();
         foreach (string line in new[] {"[cue]", "Well met.", "I bought two dromons."})
         {
            var messages = new List<LlmMessage>(history) {new LlmMessage(MessageRole.User, line)};
            watch.Phase = turn == 0 ? CostPhase.Opener : CostPhase.Prose;
            watch.Turn = turn;
            await client.CompleteAsync(new LlmRequest {
               SystemPrompt = Stable + $"CURRENT ENCOUNTER: turn {turn}", StableSystemPrompt = Stable, Messages = messages,
               Parameters = new LlmParameters {MaxTokens = 3000, Creativity = 0.7f, PresencePenalty = 0.3f, AllowTruncationRetry = turn == 0}
            });
            row.Steps.Last().Reply = said[turn];
            if (turn > 0) history.Add(new LlmMessage(MessageRole.User, line));
            history.Add(new LlmMessage(MessageRole.Assistant, said[turn]));
            turn++;
         }

         return row;
      }

      private static OpenRouterClient Client(HttpMessageHandler handler)
         => new OpenRouterClient(new HttpClient(handler), new OpenRouterConfig {
            Model = "x-ai/grok-4.20", ApiKey = "test", IsOpenRouterProvider = () => true, ReasoningProvider = () => "low"
         });

      private sealed class Watch : ILlmWireObserver
      {
         private readonly CaptureRow _row;

         public Watch(CaptureRow row) => _row = row;

         public CostPhase Phase { get; set; }
         public int Turn { get; set; }

         public void OnRequest(LlmWireSnapshot snapshot)
            => _row.Steps.Add(new CaptureStep {
               Phase = Phase, Turn = Turn, StablePrefix = snapshot.StablePrefix ?? "", DynamicTail = snapshot.DynamicTail ?? "",
               Messages = CostReplayPolicy.Tag(snapshot.Request!.Messages, _row.Steps.Select(s => s.Reply).ToList()),
               MaxTokens = snapshot.Request.Parameters.MaxTokens, Creativity = snapshot.Request.Parameters.Creativity,
               PresencePenalty = snapshot.Request.Parameters.PresencePenalty, AllowTruncationRetry = snapshot.Request.Parameters.AllowTruncationRetry,
               PayloadJson = snapshot.Json ?? ""
            });
      }

      private sealed class ScriptedHandler : HttpMessageHandler
      {
         private readonly System.Func<string> _reply;

         public ScriptedHandler(System.Func<string> reply) => _reply = reply;

         public List<string> Bodies { get; } = new List<string>();

         protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
         {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string content = Newtonsoft.Json.JsonConvert.ToString(_reply());

            return new HttpResponseMessage(HttpStatusCode.OK) {
               Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":" + content + "},\"finish_reason\":\"stop\"}]}")
            };
         }
      }
   }
}
