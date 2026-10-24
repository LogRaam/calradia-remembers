// Code written by Gabriel Mailhot, 24/10/2026.

// cr.cost_bench measures what the mod costs to play, per model and per role, and writes the tables that go on the Nexus
// page with the version they were measured on (Gabriel, 24/10/2026, after fearente spent $3.19 in a week the page priced
// at about $1). Gabriel then asked for PROSE and INTERPRET measured apart, each by the length of the conversation, with
// quality beside cost. The page's prices are what a newcomer budgets by, so a table that undercounts, mixes a
// conversation's cold start into an ordinary turn, mixes two roles' bills, scores an interpreter wrongly, or silently
// drops a model from the scenario file misleads every reader who trusts it. These pin the file, the sums and the tables.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NpcMemoryService.Core.Actions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CostBenchTests
   {
      private static readonly CostScenario GrokProse = new CostScenario {Name = "Grok", Pass = CostPass.Prose, Model = "x-ai/grok-4.20"};
      private static readonly CostScenario GrokReads = new CostScenario {Name = "Grok", Pass = CostPass.Interpret, Model = "x-ai/grok-4.20"};

      // The bench writes the scenario file when none exists, and Gabriel edits it by hand: what it writes must read back
      // as exactly the rows it meant, in their sections, or the first run measures something else.
      [Test]
      public void GIVEN_the_file_the_bench_writes_WHEN_read_back_THEN_it_holds_the_default_rows_in_their_sections()
      {
         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(CostScenarioFile.Template(), out List<string> problems);

         problems.Should().BeEmpty();
         rows.Select(r => (r.Pass, r.Name, r.Model, r.Compact, r.FallbackModel))
             .Should().Equal(CostScenarioFile.Defaults.Select(d => (d.Pass, d.Name, d.Model, d.Compact, d.FallbackModel)));
         rows.Should().Contain(r => r.Pass == CostPass.Prose && r.Model == "z-ai/glm-4.6" && r.FallbackModel == "x-ai/grok-4.20",
            "Gabriel's GLM 4.6 row, with Grok 4.20 as its NSFW fallback");
      }

      // A typo, or a row outside any section, must not silently drop a model while the others run: it is named.
      [Test]
      public void GIVEN_a_file_with_mistakes_WHEN_read_THEN_the_good_rows_run_and_each_mistake_is_named()
      {
         const string text = "Orphan | a/b\n[prose]\nGood | a/b | Compact | Low | x/nsfw\nBad | a/b | Huge\n[interpret]\nReader | c/d\n[nonsense]";

         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(text, out List<string> problems);

         rows.Select(r => (r.Pass, r.Name)).Should().Equal((CostPass.Prose, "Good"), (CostPass.Interpret, "Reader"));
         rows[0].Compact.Should().BeTrue();
         rows[0].Reasoning.Should().Be("Low");
         rows[0].FallbackModel.Should().Be("x/nsfw");
         problems.Should().HaveCount(3);
         problems.Should().Contain(p => p.StartsWith("line 1")).And.Contain(p => p.StartsWith("line 4")).And.Contain(p => p.StartsWith("line 7"));
      }

      // Gabriel: start, middle and long conversation priced apart, the middle and the long one each answered after a
      // warm-up reply. The first reply pays for the whole prompt; the warm-ups are billed but must not be counted, or the
      // middle and the long conversation would be priced as cold as the start. A fallback that redoes the measured reply
      // is still the prose's work.
      [Test]
      public void GIVEN_the_three_points_WHEN_summed_THEN_start_middle_and_long_are_priced_apart_without_their_warm_ups()
      {
         var calls = new List<CostCall> {
            Call(GrokProse, CostPhase.Prose, 1, 0.05), Call(GrokProse, CostPhase.Prose, 2, 0.004), Call(GrokProse, CostPhase.Prose, 3, 0.004),
            Call(GrokProse, CostPhase.Prose, CostWindows.MiddleWarmUp, 0.04), Call(GrokProse, CostPhase.Prose, 8, 0.005),
            Call(GrokProse, CostPhase.Fallback, 8, 0.003),
            Call(GrokProse, CostPhase.Prose, CostWindows.LongWarmUp, 0.06), Call(GrokProse, CostPhase.Prose, 18, 0.007),
            Call(GrokProse, CostPhase.Letter, 0, 0.03)
         };

         ProseRowResult r = CostBenchReport.SummarizeProse(GrokProse, calls, Enumerable.Repeat("Fine.", 7).ToList());

         r.Start.Should().BeApproximately(0.05, 1e-9);
         r.Middle.Should().BeApproximately(0.008, 1e-9);
         r.Long.Should().BeApproximately(0.007, 1e-9);
         r.Letter.Should().BeApproximately(0.03, 1e-9);
      }

      // The long conversation's measured question asks for what only the folded recap holds (forty spearmen for the
      // levies): whether the model found it is shown beside the cost.
      [Test]
      public void GIVEN_the_last_reply_WHEN_signalled_THEN_the_recap_is_recalled_only_when_the_agreement_is_named()
      {
         CostBenchReport.SummarizeProse(GrokProse, new List<CostCall>(), new[] {"a", "We agreed: forty spearmen, home before the sowing."})
                        .Signals.RecapRecalled.Should().BeTrue();
         CostBenchReport.SummarizeProse(GrokProse, new List<CostCall>(), new[] {"a", "I do not recall agreeing to anything."})
                        .Signals.RecapRecalled.Should().BeFalse();
      }

      // A name may stand in several sections (Grok writes, reads and folds): its bills must never be mixed.
      [Test]
      public void GIVEN_one_model_in_two_roles_WHEN_summed_THEN_each_role_keeps_its_own_bill()
      {
         var calls = new List<CostCall> {
            Call(GrokProse, CostPhase.Prose, 1, 0.05),
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Interpret, Phase = CostPhase.Interpreter, Difficulty = InterpretDifficulty.Simple, Cost = 0.01}
         };
         var outcomes = new List<InterpretOutcome> {new InterpretOutcome {ScenarioName = "Grok", Difficulty = InterpretDifficulty.Simple, Right = true}};

         CostBenchReport.SummarizeProse(GrokProse, calls, new[] {"Fine."}).Start.Should().BeApproximately(0.05, 1e-9);
         CostBenchReport.SummarizeInterpret(GrokReads, calls, outcomes).PerReading.Should().BeApproximately(0.01, 1e-9);
      }

      // The interpreter's quality is measurable: each scripted reply has a known answer. Its cost and score are kept by
      // difficulty, and a deed invented (the game acting on something that never happened) is counted apart.
      [Test]
      public void GIVEN_readings_of_each_difficulty_WHEN_summed_THEN_cost_score_and_inventions_are_kept_by_difficulty()
      {
         var calls = new List<CostCall> {
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Interpret, Phase = CostPhase.Interpreter, Difficulty = InterpretDifficulty.FalseLead, Cost = 0.010},
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Interpret, Phase = CostPhase.Interpreter, Difficulty = InterpretDifficulty.FalseLead, Cost = 0.012}
         };
         var outcomes = new List<InterpretOutcome> {
            new InterpretOutcome {ScenarioName = "Grok", Difficulty = InterpretDifficulty.FalseLead, Right = true},
            new InterpretOutcome {ScenarioName = "Grok", Difficulty = InterpretDifficulty.FalseLead, Invented = true}
         };

         InterpretRowResult r = CostBenchReport.SummarizeInterpret(GrokReads, calls, outcomes);

         (double cost, int right, int cases, int invented) = r.ByDifficulty[InterpretDifficulty.FalseLead];
         cost.Should().BeApproximately(0.011, 1e-9);
         (right, cases, invented).Should().Be((1, 2, 1));
         r.PerReading.Should().BeApproximately(0.011, 1e-9);
      }

      // The difficulty of a catalog case decides which column it scores in: a withheld deed is a false lead, a
      // rephrasing (_v2) or several deeds is oblique, a plain first phrasing is simple.
      [Test]
      public void GIVEN_catalog_cases_WHEN_classified_THEN_each_lands_in_its_difficulty()
      {
         ActionBenchCase plain = ActionBenchCase.Expect("give_gold", "give_gold", "ctx", "Take it.", "give_gold");
         ActionBenchCase rephrased = ActionBenchCase.Expect("give_gold_v2", "give_gold", "ctx", "Coin for you.", "give_gold");
         ActionBenchCase two = plain.And("change_relation");
         ActionBenchCase offered = ActionBenchCase.ExpectNone("give_gold_offered", "give_gold", "ctx", "I could pay you.", "give_gold");

         InterpretCases.Classify(plain).Should().Be(InterpretDifficulty.Simple);
         InterpretCases.Classify(rephrased).Should().Be(InterpretDifficulty.Oblique);
         InterpretCases.Classify(two).Should().Be(InterpretDifficulty.Oblique);
         InterpretCases.Classify(offered).Should().Be(InterpretDifficulty.FalseLead);
      }

      // Interpreters are compared on identical work: the picked cases are the same every run and spread over the catalog.
      [Test]
      public void GIVEN_the_catalog_WHEN_cases_are_picked_twice_THEN_the_same_spread_cases_come_back()
      {
         IReadOnlyList<ActionBenchCase> a = InterpretCases.Pick(ActionBenchCatalog.All, InterpretDifficulty.Simple, 6);
         IReadOnlyList<ActionBenchCase> b = InterpretCases.Pick(ActionBenchCatalog.All, InterpretDifficulty.Simple, 6);

         a.Should().HaveCount(6);
         a.Select(c => c.Id).Should().Equal(b.Select(c => c.Id));
         a.Select(c => c.Verb).Distinct().Should().HaveCountGreaterThan(3, "spread over the catalog, not six phrasings of one verb");
      }

      // In an ordinary reply nothing is done: a change of regard is allowed (warm talk), any deed is an invention.
      [Test]
      public void GIVEN_an_ordinary_reply_WHEN_read_THEN_only_a_change_of_regard_is_tolerated()
      {
         InterpretCases.NoActionReadRight(new string[0]).Should().BeTrue();
         InterpretCases.NoActionReadRight(new[] {"change_relation"}).Should().BeTrue();
         InterpretCases.NoActionReadRight(new[] {"give_gold"}).Should().BeFalse();
      }

      // fearente's complaint, as a signal: "even after I reassured her ... she launched into another speech about pride".
      // The reply to the reassurance (line 3), or any later one, that still lectures is a sermon; a warning before it is
      // counsel.
      [Test]
      public void GIVEN_replies_after_the_reassurance_WHEN_signalled_THEN_a_lecture_on_pride_counts_as_a_sermon()
      {
         var replies = new List<string> {
            "Beware the sea.", "Ships? Mind the storms.", "Pride goes before a fall, husband. Be mindful.", "The harvest was good."
         };

         ProseRowResult r = CostBenchReport.SummarizeProse(GrokProse, new List<CostCall>(), replies);

         r.Signals.Sermons.Should().Be(1, "the warnings before the reassurance are counsel; the lecture answering it is the sermon");
      }

      // Gabriel's grid: an ordinary Enhanced reply = its PROSE part + its INTERPRET part, for every pairing; and the page
      // names the version and the day.
      [Test]
      public void GIVEN_measured_parts_WHEN_the_nexus_tables_are_written_THEN_the_grid_adds_them_and_the_version_is_named()
      {
         var prose = new List<ProseRowResult> {new ProseRowResult {Row = GrokProse, Middle = 0.004}};
         var reads = new List<InterpretRowResult> {new InterpretRowResult {Row = GrokReads, PerReading = 0.002}};

         string code = CostBenchReport.NexusCode(prose, reads, "v2.7.20", new DateTime(2026, 10, 24));

         code.Should().Contain("Calradia Remembers v2.7.20, 2026-10-24");
         code.Should().Contain("$0.0060");
      }

      // A call the provider did not price counts as zero in the sum: the row must say it undercounts.
      [Test]
      public void GIVEN_a_call_without_a_price_WHEN_the_table_is_written_THEN_its_row_says_it_undercounts()
      {
         var calls = new List<CostCall> {Call(GrokProse, CostPhase.Prose, 1, 0.01), Call(GrokProse, CostPhase.Prose, 2, null)};

         ProseRowResult r = CostBenchReport.SummarizeProse(GrokProse, calls, new[] {"a", "b"});
         string table = CostBenchReport.Markdown(new[] {r}, new InterpretRowResult[0], new FoldRowResult[0], "v2.7.20", new DateTime(2026, 10, 24));

         r.Unpriced.Should().BeTrue();
         table.Should().Contain("undercounts");
      }

      // Gabriel (24/10/2026): response time beside cost. The seconds of every attempt of a reply (a retry, a fallback)
      // are what the player waited for it; an interpreter's seconds are added to the prose's in the grid, since the
      // two calls run one after the other.
      [Test]
      public void GIVEN_timed_attempts_WHEN_summed_THEN_a_reply_waits_for_all_its_attempts_and_the_grid_adds_both_roles()
      {
         var calls = new List<CostCall> {
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Prose, Phase = CostPhase.Prose, Turn = 8, Cost = 0.004, Seconds = 6.0},
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Prose, Phase = CostPhase.Fallback, Turn = 8, Cost = 0.001, Seconds = 3.5}
         };

         ProseRowResult p = CostBenchReport.SummarizeProse(GrokProse, calls, new[] {"a"});
         var reads = new List<InterpretRowResult> {new InterpretRowResult {Row = GrokReads, PerReading = 0.002, SecondsPerReading = 2.0}};

         p.MiddleSeconds.Should().BeApproximately(9.5, 1e-9);
         CostBenchReport.NexusCode(new[] {p}, reads, "v2.7.20", new DateTime(2026, 10, 24)).Should().Contain("$0.0070 11.5 s");
      }

      // Gabriel: the sample an interpreter's score needs. 24 of 24 says much less than 290 of 296; the interval says how
      // much, and stays honest near 100%.
      [Test]
      public void GIVEN_a_score_WHEN_its_interval_is_computed_THEN_a_small_perfect_sample_claims_far_less_than_a_large_one()
      {
         (int _, int _, double smallLow, double smallHigh) = CostBenchReport.Wilson(24, 24);
         (int _, int _, double largeLow, double _) = CostBenchReport.Wilson(290, 296);

         smallHigh.Should().Be(1);
         smallLow.Should().BeLessThan(0.87);
         largeLow.Should().BeGreaterThan(0.95);
      }

      // The first run's Grok calls read 128 cached tokens of 16,000 although Grok caches a stable prefix (probed): the
      // bench names where a prose call's cacheable prefix stopped matching the previous one.
      [Test]
      public void GIVEN_two_prefixes_WHEN_compared_THEN_the_first_changed_character_is_found_and_a_mere_extension_is_none()
      {
         CostBenchReport.FirstDivergence("ROLE: lord. VERBS: give_gold, marry.", "ROLE: lord. VERBS: give_gold, duel.").Should().Be(30);
         CostBenchReport.FirstDivergence("same", "same").Should().Be(-1);
         CostBenchReport.FirstDivergence("same", "same and more").Should().Be(4);
      }

      // An interpret row may ask for a sample; blank means the whole catalog, the default.
      [Test]
      public void GIVEN_interpret_rows_WHEN_read_THEN_a_number_is_a_sample_and_blank_the_whole_catalog()
      {
         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse("[interpret]\nAll | a/b\nSome | c/d | 10\nBad | e/f | many", out List<string> problems);

         rows.Select(r => (r.Name, r.CasesPerDifficulty)).Should().Equal(("All", 0), ("Some", 10));
         problems.Should().ContainSingle().Which.Should().StartWith("line 4");
      }

      // The cap keeps a long scenario file from emptying a player's credit: reached means stop, and none set means none.
      [Test]
      public void GIVEN_a_spend_cap_WHEN_spending_reaches_it_THEN_the_run_stops_and_no_cap_never_stops_it()
      {
         CostBenchReport.CapReached(2.99, 3).Should().BeFalse();
         CostBenchReport.CapReached(3.00, 3).Should().BeTrue();
         CostBenchReport.CapReached(50, 0).Should().BeFalse();
      }

      private static CostCall Call(CostScenario row, CostPhase phase, int turn, double? cost)
         => new CostCall {ScenarioName = row.Name, Pass = row.Pass, Phase = phase, Turn = turn, Model = row.Model, PromptTokens = 1000, CompletionTokens = 200, Cost = cost};
   }
}
