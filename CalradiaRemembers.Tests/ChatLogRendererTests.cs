// Code written by Gabriel Mailhot, 28/11/2026.
// What ChatLogRenderer and ChatLogCollector decide: the conversation log a chat writes when the MCM switch "Write a conversation
// log" is on. Gabriel (28/11/2026): "ajoute une option MCM qui permet de créer un log d'une conversation sous la forme que tu
// faisais déjà (ex: cr_group_third_20261127_235024.md) et qui explique le Why each turn went as it did". If this breaks, the
// log stops reading like the simulator's transcripts Gabriel compares against, a deed or a fallback lands under the wrong
// turn, a key or a provider URL reaches a file a player shares, or a judge line appears that no judge wrote.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChatLogRendererTests
   {
      private static readonly ChatLogModels Models = new() {Mode = "Enhanced", Prose = "deepseek/deepseek-v4-pro", Interpreter = "deepseek/deepseek-v4.1-flash", Nsfw = "x-ai/grok-4.20"};

      private static ChatLogCollector Staged()
      {
         var c = new ChatLogCollector(Models, new DateTime(2026, 11, 28, 21, 0, 0));
         c.AddLine(new ChatLogLine {Speaker = "Scene", Text = "Candles burn low in the hall.", Kind = ChatLogLineKind.Narration});
         c.BeginTurn();
         c.AddCall(new ChatLogCall {Model = "deepseek/deepseek-v4-pro", PromptTokens = 5400, CachedTokens = 4100, CompletionTokens = 210, Cost = 0.0012, FinishReason = "stop"}, null);
         c.AddLine(new ChatLogLine {Speaker = "You", Text = "My lady, I bring your husband's sword.", IsPlayer = true});
         c.AddLine(new ChatLogLine {Speaker = "Ilsa", Text = "*She takes it.* Then he is truly gone."});
         c.AddCall(new ChatLogCall {Model = "deepseek/deepseek-v4.1-flash", PromptTokens = 900, CompletionTokens = 30}, "lord_ilsa");
         c.AddAction("give_item: done (You give the sword.)");
         c.AddWhy(ChatLogWhyKind.Reply, "Ilsa's reply took 2.3 s on deepseek/deepseek-v4-pro.");

         return c;
      }

      private static string Rendered(ChatLogCollector c)
         => ChatLogRenderer.Render(c.Snapshot(new ChatLogFacts {Character = "Ilsa", Place = "Pravend", Day = "Summer 12, 1084", Closed = new DateTime(2026, 11, 28, 21, 3, 0)}));

      // STAKE: Gabriel reads these beside the simulator's transcripts; a different shape cannot be compared at a glance.
      [Test]
      public void GIVEN_a_staged_chat_WHEN_rendered_THEN_it_reads_like_the_simulator_transcript()
      {
         string log = Rendered(Staged());

         log.Should().StartWith("# Conversation with Ilsa, Pravend, Summer 12, 1084");
         log.Should().Contain("Mode: Enhanced, prose deepseek/deepseek-v4-pro (1 calls), interpreter deepseek/deepseek-v4.1-flash (1), NSFW x-ai/grok-4.20 (0). 2 model calls. Cost: $0.0012 (1 calls not priced). 3.0 min.");
         log.Should().Contain(ChatLogRenderer.SceneHeading);
         log.Should().Contain("> Candles burn low in the hall.");
         log.Should().Contain("**You**: My lady, I bring your husband's sword.");
         log.Should().Contain("**Ilsa**: *She takes it.* Then he is truly gone.\n  `actions: give_item: done (You give the sword.)`");
         log.IndexOf(ChatLogRenderer.SceneHeading, StringComparison.Ordinal).Should().BeLessThan(log.IndexOf(ChatLogRenderer.WhyHeading, StringComparison.Ordinal));
      }

      // STAKE: a deed or a model call listed under the wrong turn explains the wrong reply.
      [Test]
      public void GIVEN_calls_and_deeds_in_a_turn_WHEN_rendered_THEN_the_why_lists_them_under_that_turn()
      {
         string log = Rendered(Staged());
         string why = log.Substring(log.IndexOf(ChatLogRenderer.WhyHeading, StringComparison.Ordinal));

         why.Should().Contain("**Opening**");
         why.Should().Contain("**Turn 1** (\"My lady, I bring your husband's sword.\")\n"
                              + "- call: prose deepseek/deepseek-v4-pro, 5400 in (4100 cached) / 210 out, $0.0012, finish stop\n"
                              + "- call: interpreter deepseek/deepseek-v4.1-flash, 900 in / 30 out\n"
                              + "- action: give_item: done (You give the sword.)\n"
                              + "- reply: Ilsa's reply took 2.3 s on deepseek/deepseek-v4-pro.");
      }

      // STAKE: Gabriel asked for "judge -> referee" lines; none run in the two-person chat yet, and a line pretending they did would mislead his reading.
      [Test]
      public void GIVEN_no_judge_ran_WHEN_rendered_THEN_no_judge_or_referee_line_is_written()
      {
         string log = Rendered(Staged());

         log.Should().NotContain("- judge:").And.NotContain("- referee:");
         log.Should().Contain(ChatLogRenderer.WhyScope);
      }

      // STAKE: the later stages must be able to add their judge and referee lines to the same why section without a new format.
      [Test]
      public void GIVEN_a_later_stage_adds_a_judge_entry_WHEN_rendered_THEN_it_shows_under_its_turn()
      {
         ChatLogCollector c = Staged();
         c.AddWhy(ChatLogWhyKind.Judge, "Ilsa High [to] => Ilsa");

         Rendered(c).Should().Contain("- judge: Ilsa High [to] => Ilsa");
      }

      // STAKE: a player shares this file; a key or a provider URL in an error must never reach it.
      [Test]
      public void GIVEN_an_error_with_a_key_and_a_url_WHEN_rendered_THEN_both_are_masked()
      {
         ChatLogCollector c = Staged();
         c.AddCall(new ChatLogCall {Model = "deepseek/deepseek-v4-pro", Error = "401 at https://openrouter.ai/api/v1/chat/completions key sk-or-v1-0123456789abcdef0123"}, null);

         string log = Rendered(c);

         log.Should().Contain("FAILED: 401 at [url] key [masked]");
         log.Should().NotContain("openrouter.ai").And.NotContain("0123456789abcdef0123");
      }

      // STAKE: the game's out-of-character note about a refused deed is not the player speaking.
      [Test]
      public void GIVEN_the_games_refusal_note_WHEN_recorded_THEN_it_is_a_note_not_the_players_line()
      {
         ChatLogLineKind kind = ChatLogLine.KindOf(true, false, "(Out of character: the game refused that. It did not happen.)");
         ChatLogCollector c = Staged();
         c.AddLine(new ChatLogLine {Speaker = "You", Text = "(Out of character: the game refused that. It did not happen.)", IsPlayer = true, Kind = kind});

         kind.Should().Be(ChatLogLineKind.Note);
         Rendered(c).Should().Contain("*(game note to the model)* (Out of character: the game refused that.");
         ChatLogLine.KindOf(true, false, "I refuse.").Should().Be(ChatLogLineKind.Speech);
      }

      // STAKE: the interpreter often runs on the prose model; told apart by model alone, its calls would count as replies.
      [Test]
      public void GIVEN_calls_WHEN_their_role_is_read_THEN_the_interpreter_nsfw_and_background_calls_are_told_apart()
      {
         var same = new ChatLogModels {Prose = "m/a", Interpreter = "m/a", Nsfw = "m/a"};

         ChatLogRoles.Of(same, "m/a", ChatLogRoles.InterpreterSubject("lord_x")).Should().Be(ChatLogRoles.Interpreter);
         ChatLogRoles.Of(same, "m/a", null).Should().Be(ChatLogRoles.Prose);
         // Gabriel's chat with Monchug (28/11/2026): the reply carries the bare character id, and the scene engine names its purpose.
         ChatLogRoles.Of(same, "m/a", "lord_x").Should().Be(ChatLogRoles.Prose);
         ChatLogRoles.Of(same, "m/a", "keeper").Should().Be("keeper");
         ChatLogRoles.Of(new ChatLogModels {Prose = "m/p", Interpreter = "m/i"}, "m/i", "lord_x").Should().Be(ChatLogRoles.Interpreter);
         ChatLogRoles.Of(Models, "x-ai/grok-4.20", null).Should().Be(ChatLogRoles.Nsfw);
         ChatLogRoles.Of(Models, "some/summary-model", null).Should().Be(ChatLogRoles.Other);
      }

      // STAKE: a name with spaces or apostrophes must still give a file Windows accepts.
      [Test]
      public void GIVEN_a_name_with_spaces_WHEN_the_file_is_named_THEN_it_is_safe_and_dated()
      {
         ChatLogRenderer.FileName("Lady Ira d'Ostican", new DateTime(2026, 11, 27, 23, 50, 24)).Should().Be("cr_chat_lady_ira_d_ostican_20261127_235024.md");
         ChatLogRenderer.FileName("  ", new DateTime(2026, 11, 27, 23, 50, 24)).Should().Be("cr_chat_someone_20261127_235024.md");
      }

      // STAKE: "why did she agree and nothing happen" is answered only if the log says the game refused the deed, and why.
      [Test]
      public void GIVEN_deeds_the_game_did_refused_or_does_not_know_WHEN_logged_THEN_each_says_which_with_the_games_reason()
      {
         ChatLogActionText.For(new ChatLogActionFacts {Verb = "give_gold", Reported = true, Message = "You receive 100 denars."}).Should().Be("give_gold: done (You receive 100 denars.)");
         ChatLogActionText.For(new ChatLogActionFacts {Verb = "marry", Reported = true, WasRefused = true, Message = "Their realms are at war."}).Should().Be("marry: REFUSED (Their realms are at war.)");
         ChatLogActionText.For(new ChatLogActionFacts {Verb = "summon_dragon", Reported = true, WasUnknownVerb = true, Message = "unknown"}).Should().Be("summon_dragon: no such deed in the game");
      }

      // STAKE: a deed read before any character line (a scene opener's choice) must not vanish nor attach to the player.
      [Test]
      public void GIVEN_a_deed_before_any_character_line_WHEN_recorded_THEN_only_the_why_holds_it()
      {
         var c = new ChatLogCollector(Models, DateTime.Now);
         c.BeginTurn();
         c.AddLine(new ChatLogLine {Speaker = "You", Text = "Hello.", IsPlayer = true});
         c.AddAction("give_gold: refused (no coin)");

         ChatLogFacts f = c.Snapshot(null);
         f.Lines[0].Actions.Should().BeEmpty();
         f.Why.Should().ContainSingle(w => w.Kind == ChatLogWhyKind.Action && w.Turn == 1);
      }
   }
}
