// Code written by Gabriel Mailhot, 24/10/2026.

// cr.cost_bench measures what the mod costs to play, per model and per MCM setup, and writes the table that goes on
// the Nexus page with the version it was measured on (Gabriel, 24/10/2026, after fearente spent $3.19 in a week the
// page priced at about $1). The page's prices are what a newcomer budgets by, so a table that undercounts, mixes a
// conversation's cold first reply into the price of an ordinary turn, or silently drops a model from the scenario file
// misleads every reader who trusts it. These pin the file, the sums and the table.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CostBenchTests
   {
      private static readonly CostScenario Grok = new CostScenario {Name = "Grok", ProseModel = "x-ai/grok-4.20", InterpreterModel = "x-ai/grok-4.20"};

      // The bench writes the scenario file when none exists, and Gabriel edits it by hand: what it writes must read back
      // as exactly the rows it meant, or the first run measures something else.
      [Test]
      public void GIVEN_the_file_the_bench_writes_WHEN_read_back_THEN_it_holds_the_default_scenarios()
      {
         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(CostScenarioFile.Template(), out List<string> problems);

         problems.Should().BeEmpty();
         rows.Select(r => r.Name).Should().Equal(CostScenarioFile.Defaults.Select(d => d.Name));
         rows.Select(r => (r.ProseModel, r.InterpreterModel, r.Enhanced, r.Compact, r.NsfwModel))
             .Should().Equal(CostScenarioFile.Defaults.Select(d => (d.ProseModel, d.InterpreterModel, d.Enhanced, d.Compact, d.NsfwModel)));
      }

      // A typo in one line must not silently drop that model from the table while the others run: it is named.
      [Test]
      public void GIVEN_a_line_with_a_typo_WHEN_the_file_is_read_THEN_the_others_run_and_the_typo_is_named()
      {
         const string text = "Good | a/b | a/b | Enhanced | Full | | x/nsfw\nBad | a/b | a/b | Enhanced | Huge |\nMix | p/q | | Standard | Compact | Low";

         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(text, out List<string> problems);

         rows.Select(r => r.Name).Should().Equal("Good", "Mix");
         rows[1].InterpreterModel.Should().Be("p/q", "a blank interpret model means the prose model");
         rows[1].Reasoning.Should().Be("Low");
         rows[0].NsfwModel.Should().Be("x/nsfw", "Gabriel's GLM row names Grok as its NSFW fallback");
         rows[1].NsfwModel.Should().BeEmpty("an older six-column line keeps the player's own NSFW model");
         problems.Should().ContainSingle().Which.Should().StartWith("line 2");
      }

      // The first reply of a conversation pays for the whole prompt; later ones read it from cache. Averaging them
      // together would price an ordinary turn at several times what it costs. Both calls of an Enhanced reply, and a
      // retry, belong to that reply's price.
      [Test]
      public void GIVEN_a_cold_first_reply_and_warm_later_ones_WHEN_summed_THEN_each_is_priced_apart_with_every_call_of_a_reply()
      {
         var calls = new List<CostCall> {
            Call(1, CostPhase.Prose, 0.030), Call(1, CostPhase.Interpreter, 0.020),
            Call(2, CostPhase.Prose, 0.004), Call(2, CostPhase.Interpreter, 0.002),
            Call(3, CostPhase.Prose, 0.003), Call(3, CostPhase.Prose, 0.003), Call(3, CostPhase.Interpreter, 0.002),
            Call(0, CostPhase.Letter, 0.025)
         };

         CostScenarioResult r = CostBenchReport.Summarize(Grok, calls);

         r.FirstReply.Should().BeApproximately(0.050, 1e-9);
         r.LaterReply.Should().BeApproximately(0.007, 1e-9);
         r.Letter.Should().BeApproximately(0.025, 1e-9);
         r.Replies.Should().Be(3);
         r.Total.Should().BeApproximately(0.089, 1e-9);
         r.HundredReplies.Should().BeApproximately(10 * 0.050 + 90 * 0.007, 1e-9);
      }

      // Gabriel (24/10/2026): the PROSE and INTERPRET calls do different work on different prompts, so a later reply's
      // price is shown split between them; a model can be cheap at one and dear at the other.
      [Test]
      public void GIVEN_enhanced_replies_WHEN_summed_THEN_a_later_reply_is_split_between_prose_and_interpret()
      {
         var calls = new List<CostCall> {
            Call(1, CostPhase.Prose, 0.030), Call(1, CostPhase.Interpreter, 0.020),
            Call(2, CostPhase.Prose, 0.004), Call(2, CostPhase.Interpreter, 0.002),
            Call(3, CostPhase.Prose, 0.003), Call(3, CostPhase.Fallback, 0.005)
         };

         CostScenarioResult r = CostBenchReport.Summarize(Grok, calls);

         r.ProsePart.Should().BeApproximately((0.004 + 0.003 + 0.005) / 2, 1e-9, "a redone reply is still the prose's work");
         r.InterpretPart.Should().BeApproximately(0.002 / 2, 1e-9);
         CostBenchReport.Markdown(new[] {r}, "v2.7.20", new DateTime(2026, 10, 24)).Should().Contain("of which INTERPRET");
      }

      // A call the provider did not price counts as zero in the sum: the row must say it undercounts rather than look
      // cheaper than it is.
      [Test]
      public void GIVEN_a_call_without_a_price_WHEN_the_table_is_written_THEN_its_row_says_it_undercounts()
      {
         var calls = new List<CostCall> {Call(1, CostPhase.Prose, 0.01), Call(2, CostPhase.Prose, null)};

         CostScenarioResult r = CostBenchReport.Summarize(Grok, calls);
         string table = CostBenchReport.Markdown(new[] {r}, "v2.7.20", new DateTime(2026, 10, 24));

         r.Unpriced.Should().BeTrue();
         table.Should().Contain("undercounts");
      }

      // Gabriel: a table in the description WITH the version of the mod used for the test. A price without its version
      // cannot be told stale when the prompt changes.
      [Test]
      public void GIVEN_a_finished_run_WHEN_the_nexus_table_is_written_THEN_it_names_the_version_and_the_day()
      {
         CostScenarioResult r = CostBenchReport.Summarize(Grok, new List<CostCall> {Call(1, CostPhase.Prose, 0.02)});

         string code = CostBenchReport.NexusCode(new[] {r}, "v2.7.20", new DateTime(2026, 10, 24));

         code.Should().Contain("Calradia Remembers v2.7.20, 2026-10-24");
         code.Should().Contain("[code]").And.Contain("[/code]");
         code.Should().Contain("Grok");
      }

      // The cap is what keeps a long scenario file from emptying a player's credit: reached means stop, and none set
      // means none.
      [Test]
      public void GIVEN_a_spend_cap_WHEN_spending_reaches_it_THEN_the_run_stops_and_no_cap_never_stops_it()
      {
         CostBenchReport.CapReached(2.99, 3).Should().BeFalse();
         CostBenchReport.CapReached(3.00, 3).Should().BeTrue();
         CostBenchReport.CapReached(50, 0).Should().BeFalse();
      }

      private static CostCall Call(int turn, CostPhase phase, double? cost)
         => new CostCall {ScenarioName = "Grok", Turn = turn, Phase = phase, Model = "x-ai/grok-4.20", PromptTokens = 1000, CompletionTokens = 200, Cost = cost};
   }
}
