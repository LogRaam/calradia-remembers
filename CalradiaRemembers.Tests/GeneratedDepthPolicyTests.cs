// Code written by Gabriel Mailhot, 28/11/2026.
// Generated depth (Stage 4 of the group-scene plan; Gabriel, 28/11/2026: "go pour tout le système ... je testerai demain"). Once
// per character, one background call gives a game-data character a voice, a mask, a fear, a limit and a private secret, kept for
// good. Gabriel on the secret (27/11/2026): "Il peut y avoir des secrets personnels complémentaires au jeu.. si ça ne touche pas
// à une mécanique du jeu ... Le problème arrive lorsque le LLM invente quelque chose que le jeu peut contredire."
//
// These tests pin when a depth is generated, what the generator is asked, how its reply is read, and how the secret is judged.
// If this is wrong: a character changes nature between two meetings (generated again), a council or an envoy pays a call for
// nothing, a second fear or a second voice competes with the game's or the player's own, a refusal or a model thinking aloud is
// kept for good, or a secret that the game can contradict (a marriage, a killing, money) reaches the prompt.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GeneratedDepthPolicyTests
   {
      private const string Player = "Arwa";
      private const string Secret = "Once a suitor wanted to court her friend Irana, and she talked him out of it. Irana never knew.";

      private const string Reply = "voice: slow and exact, short sentences, never a flourish\nmask: a governor who has every ledger in hand\n"
                                   + "fear: being thought a fool by the young\nlimit: will not beg a favour twice\nsecret: " + Secret;

      private static readonly GeneratedDepthSheet Sheet = new() {NpcName = "Alympia", Sheet = "You are Alympia, governor of Poros.\n\nYOUR VOICE (how you, and no one else, speak):\nYou weigh every word."};

      private static readonly GeneratedDepthFacts Due = new() {SwitchOn = true, OrdinaryConversation = true, HasCharacter = true};

      private sealed class Canned : ILlmClient
      {
         private readonly Func<LlmRequest, LlmResponse> _answer;
         public readonly List<LlmRequest> Seen = new();

         public Canned(Func<LlmRequest, LlmResponse> answer) => _answer = answer;

         public Canned(string content) : this(_ => new LlmResponse {Content = content, IsSuccess = true}) { }

         public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
         {
            Seen.Add(request);

            return Task.FromResult(_answer(request));
         }
      }

      private static GeneratedDepthResult Run(ILlmClient generator, ILlmClient checker, GeneratedDepthSheet sheet = null)
         => GeneratedDepthGenerator.GenerateAsync(sheet ?? Sheet, generator, checker, 40, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();

      private static GeneratedDepthFacts With(Action<Dictionary<string, bool>> change)
      {
         var f = new Dictionary<string, bool> {["switch"] = true, ["ordinary"] = true, ["character"] = true};
         change(f);

         return new GeneratedDepthFacts {
            SwitchOn = f["switch"], OrdinaryConversation = f["ordinary"], HasCharacter = f["character"], HasDepth = f.ContainsKey("depth"), InFlight = f.ContainsKey("flight"),
            FailedThisSession = f.ContainsKey("failed"), EnvoyAudience = f.ContainsKey("envoy"), CompanionBriefing = f.ContainsKey("briefing")
         };
      }

      // STAKE: the default must not move: with "Deeper characters" off, no call is ever made.
      [Test]
      public void GIVEN_the_switch_off_WHEN_a_character_without_depth_speaks_THEN_nothing_is_generated()
      {
         GeneratedDepthPolicy.ShouldGenerate(Due).Should().BeTrue();
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["switch"] = false)).Should().BeFalse();
      }

      // STAKE: a review of the plan: regenerated, the same character would change nature between two meetings.
      [Test]
      public void GIVEN_a_character_that_already_has_a_depth_WHEN_they_speak_again_THEN_it_is_never_generated_again()
      {
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["depth"] = true)).Should().BeFalse();
      }

      // STAKE: only an ordinary one-to-one talk with a real character pays the call; an envoy, a briefing, a council or the
      // player get none, and a running or already failed generation is not doubled at every chat.
      [Test]
      public void GIVEN_an_envoy_a_briefing_a_council_a_running_or_failed_generation_WHEN_deciding_THEN_nothing_is_generated()
      {
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["envoy"] = true)).Should().BeFalse();
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["briefing"] = true)).Should().BeFalse();
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["ordinary"] = false)).Should().BeFalse();
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["character"] = false)).Should().BeFalse();
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["flight"] = true)).Should().BeFalse();
         GeneratedDepthPolicy.ShouldGenerate(With(f => f["failed"] = true)).Should().BeFalse();
      }

      // STAKE: Gabriel's rule lives in the generator's own prompt: private life the game cannot check, never a tracked fact,
      // his Cindy example, and the sheet the depth must grow out of.
      [Test]
      public void GIVEN_a_sheet_WHEN_the_generator_is_asked_THEN_it_carries_the_secret_rule_and_the_sheet()
      {
         string system = GeneratedDepthPrompt.GeneratorSystem(Sheet);

         system.Should().Contain("PRIVATE LIFE THE GAME CANNOT CHECK").And.Contain("Cindy");
         system.Should().Contain("marriage, betrothal, child, kinship, death, killing, fief, battle, war, alliance, treason, money, troops or prisoners");
         system.Should().Contain("no signature word, no recurring phrase or tic", "the ruling of 15/11/2026: no invented tics");
         GeneratedDepthPrompt.GeneratorUser(Sheet).Should().Contain("THE SHEET OF ALYMPIA").And.Contain("governor of Poros");
      }

      // STAKE: the depth fills what is empty: a character the player gave a voice keeps it, the game's own fear is kept, and
      // neither is asked for nor kept from the reply.
      [Test]
      public void GIVEN_an_authored_voice_and_a_game_fear_WHEN_asked_and_read_THEN_no_voice_and_no_fear_come_back()
      {
         var sheet = new GeneratedDepthSheet {NpcName = "Alympia", Sheet = "x", AskVoice = false, AskFear = false};

         string system = GeneratedDepthPrompt.GeneratorSystem(sheet);
         system.Should().NotContain("voice:").And.NotContain("fear:");

         GeneratedDepthReply reply = GeneratedDepthPrompt.Parse(Reply, sheet);
         reply.Voice.Should().BeEmpty();
         reply.Fear.Should().BeEmpty();
         reply.Mask.Should().Be("a governor who has every ledger in hand");
      }

      // STAKE: what is stored is stored for good: markdown labels still read, a field answered "none" is empty, a long field is
      // cut at a word, and a refusal or the model thinking aloud keeps nothing.
      [Test]
      public void GIVEN_replies_of_every_shape_WHEN_read_THEN_only_a_clean_depth_is_usable()
      {
         GeneratedDepthReply marked = GeneratedDepthPrompt.Parse("**Mask:** a patient judge\n- **Limit**: never strikes a servant\n**secret:** none", Sheet);
         marked.Mask.Should().Be("a patient judge");
         marked.Limit.Should().Be("never strikes a servant");
         marked.Secret.Should().BeEmpty();
         marked.IsUsable.Should().BeTrue();

         GeneratedDepthPrompt.Parse("mask: " + string.Join(" ", Enumerable.Repeat("word", 100)) + "\nlimit: x", Sheet).Mask.Length.Should().BeLessThanOrEqualTo(GeneratedDepth.MaxTraitChars);
         GeneratedDepthPrompt.Parse("I'm sorry, but I can't help with that request.", Sheet).IsUsable.Should().BeFalse();
         GeneratedDepthPrompt.Parse("mask: a patient judge", Sheet).IsUsable.Should().BeFalse("a mask and a limit at the least");
      }

      // STAKE: the plainest misses never reach the prompt, even unchecked; private life between people is not caught.
      [Test]
      public void GIVEN_secrets_WHEN_screened_for_tracked_words_THEN_only_the_tracked_facts_are_caught()
      {
         GeneratedDepthPrompt.NamesATrackedFact("She was married in secret to a sellsword.").Should().BeTrue();
         GeneratedDepthPrompt.NamesATrackedFact("He poisoned his elder brother's cup.").Should().BeTrue();
         GeneratedDepthPrompt.NamesATrackedFact("She hid two hundred denars from her lord.").Should().BeTrue();
         GeneratedDepthPrompt.NamesATrackedFact(Secret).Should().BeFalse("Gabriel's own example of a good secret");
         GeneratedDepthPrompt.NamesATrackedFact("He still keeps the ribbon a girl gave him at a fair, and pretends he lost it.").Should().BeFalse();
      }

      // STAKE: the check fails closed: only a plain "ok" keeps a secret; "ok, but touches", "touches", an empty or unreadable
      // verdict drop it (the composed opening's lesson, 28/11/2026: "Ok, but it invents" once passed on its leading ok).
      [Test]
      public void GIVEN_verdicts_WHEN_read_THEN_only_a_plain_ok_keeps_the_secret()
      {
         GeneratedDepthPrompt.ReadVerdict("ok").Passes.Should().BeTrue();
         GeneratedDepthPrompt.ReadVerdict("**OK.**").Passes.Should().BeTrue();
         GeneratedDepthPrompt.ReadVerdict("ok, it touches nothing").Passes.Should().BeTrue();
         GeneratedDepthPrompt.ReadVerdict("ok, but it touches a betrothal").Passes.Should().BeFalse();
         GeneratedDepthPrompt.ReadVerdict("touches a marriage").Reason.Should().Contain("marriage");
         GeneratedDepthPrompt.ReadVerdict("").Passes.Should().BeFalse();
         GeneratedDepthPrompt.ReadVerdict("Looks fine to me").Passes.Should().BeFalse();
      }

      // STAKE: a usable reply with a secret that passes is kept whole, and the checker read the secret against the sheet.
      [Test]
      public void GIVEN_a_good_reply_and_a_passing_check_WHEN_generated_THEN_the_whole_depth_is_kept()
      {
         var checker = new Canned("ok");
         GeneratedDepthResult r = Run(new Canned(Reply), checker);

         r.Depth.Should().NotBeNull();
         r.Depth.Secret.Should().Be(Secret);
         r.Depth.Voice.Should().Be("slow and exact, short sentences, never a flourish");
         r.Depth.GeneratedOnDay.Should().Be(40);
         r.SecretDropped.Should().BeFalse();
         checker.Seen.Single().Messages[0].Content.Should().Contain("governor of Poros").And.Contain(Secret);
      }

      // STAKE: Gabriel: "le problème arrive lorsque le LLM invente quelque chose que le jeu peut contredire": a flagged secret is
      // dropped, and only the secret: the voice, mask, fear and limit stay.
      [Test]
      public void GIVEN_a_secret_the_check_flags_WHEN_generated_THEN_the_secret_is_dropped_and_the_rest_kept()
      {
         GeneratedDepthResult r = Run(new Canned(Reply), new Canned("touches a betrothal"));

         r.Depth.Secret.Should().BeEmpty();
         r.Depth.Mask.Should().Be("a governor who has every ledger in hand");
         r.SecretDropped.Should().BeTrue();
      }

      // STAKE: a check that did not run is no check: no checker (Standard mode), a failed or thrown check, or a word screen hit
      // drop the secret; the screen spends no call.
      [Test]
      public void GIVEN_no_check_a_failed_check_or_a_tracked_word_WHEN_generated_THEN_the_secret_is_dropped()
      {
         Run(new Canned(Reply), null).Depth.Secret.Should().BeEmpty();
         Run(new Canned(Reply), new Canned(_ => new LlmResponse {Content = null, IsSuccess = false, ErrorMessage = "429"})).Depth.Secret.Should().BeEmpty();
         Run(new Canned(Reply), new Canned(_ => throw new InvalidOperationException())).Depth.Secret.Should().BeEmpty();

         var unasked = new Canned("ok");
         Run(new Canned(Reply.Replace(Secret, "He was married in secret before he swore to his liege.")), unasked).Depth.Secret.Should().BeEmpty();
         unasked.Seen.Should().BeEmpty();
      }

      // STAKE: nothing usable is stored, so a later chat may try again; a broken generator never leaves a half depth for good.
      [Test]
      public void GIVEN_a_failed_refusing_or_thin_generator_WHEN_generated_THEN_nothing_is_kept()
      {
         Run(new Canned(_ => new LlmResponse {Content = null, IsSuccess = false, ErrorMessage = "timeout"}), new Canned("ok")).Depth.Should().BeNull();
         Run(new Canned("I cannot help with that request."), new Canned("ok")).Depth.Should().BeNull();
         Run(new Canned("mask: a patient judge"), new Canned("ok")).Depth.Should().BeNull();
         GeneratedDepthGenerator.GenerateAsync(Sheet, null, null, 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), CancellationToken.None).GetAwaiter().GetResult().Depth.Should().BeNull();
      }

      // STAKE: Stage 2's meeting record tracks told and untold from then on: the secret enters it as still kept from the player,
      // once, without changing the record a reader holds; an heir's record starts fresh as the meeting update does.
      [Test]
      public void GIVEN_a_kept_secret_WHEN_seeded_into_the_meeting_record_THEN_it_is_untold_once_and_the_old_record_is_untouched()
      {
         var existing = new MeetingMemory {AboutPlayer = Player, Unsettled = {"whether Arwa will speak for my brother"}};

         MeetingMemory seeded = GeneratedDepthPolicy.SeedSecret(existing, Secret, Player, 40);

         seeded.UntoldTo(Player).Should().Equal(Secret);
         seeded.Unsettled.Should().Equal("whether Arwa will speak for my brother");
         seeded.Secrets.Single().AnchorId.Should().Be(GeneratedDepthPolicy.SeedAnchor);
         existing.Secrets.Should().BeEmpty("the record a reader holds is never changed");
         GeneratedDepthPolicy.SeedSecret(seeded, Secret, Player, 41).Should().BeNull("the same secret is never seeded twice");
         GeneratedDepthPolicy.SeedSecret(null, "", Player, 41).Should().BeNull();
         GeneratedDepthPolicy.SeedSecret(new MeetingMemory {AboutPlayer = "Old Lord", Unsettled = {"a debt"}}, Secret, Player, 41).Unsettled.Should().BeEmpty();
      }
   }
}
