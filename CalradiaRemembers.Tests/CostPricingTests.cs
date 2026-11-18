// Code written by Gabriel Mailhot, 18/11/2026.

// cr.cost_bench now runs on any provider the mod speaks to (Gabriel, 18/11/2026: local setups, Player2, NanoGPT, not only
// OpenRouter). Accuracy, speed and cache come from what every provider reports; the price comes from the provider's own
// bill, else from the row's prices per million tokens, else the row says the model is free; a paid provider with no known
// price is not run, since the spend cap could not hold. These pin what a player relies on: that a row's prices read back
// without moving its other fields, that a call is priced the way the table says, and that no row spends uncounted.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CostPricingTests
   {
      // STAKE: prices close a row; read as positional fields they would become the prompt or the reasoning, and the row
      // would measure another setup than the one written.
      [Test]
      public void GIVEN_rows_ending_with_prices_or_free_WHEN_read_THEN_the_prices_are_kept_and_the_other_fields_keep_their_places()
      {
         const string file = "[prose]\nLocal Llama | llama3.1:8b | Full | Low | free\n"
                             + "[interpret]\nNano | deepseek-v4-flash | 10 | $0.20/0.05/0.80\n"
                             + "[standard]\nNano writer | glm-4.6 | Compact | Low | $0.6/2.2\n";

         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse(file, out List<string> problems);

         problems.Should().BeEmpty();
         CostScenario local = rows.Single(r => r.Name == "Local Llama");
         local.Free.Should().BeTrue();
         local.Reasoning.Should().Be("Low");
         local.FallbackModel.Should().BeEmpty();
         CostScenario reader = rows.Single(r => r.Name == "Nano");
         reader.CasesPerDifficulty.Should().Be(10);
         (reader.InPerMillion, reader.CachedPerMillion, reader.OutPerMillion).Should().Be((0.20, 0.05, 0.80));
         CostScenario writer = rows.Single(r => r.Name == "Nano writer");
         writer.Compact.Should().BeTrue();
         (writer.InPerMillion, writer.CachedPerMillion, writer.OutPerMillion).Should().Be((0.6, 0.6, 2.2));
      }

      // STAKE: a typo in a price must be named, never read as zero (a run would then spend without counting).
      [Test]
      public void GIVEN_a_price_that_cannot_be_read_or_a_row_both_free_and_priced_WHEN_read_THEN_the_row_is_named_and_skipped()
      {
         IReadOnlyList<CostScenario> rows = CostScenarioFile.Parse("[fold]\nA | m | $abc\nB | m | free | $1/2\n", out List<string> problems);

         rows.Should().BeEmpty();
         problems.Should().HaveCount(2);
      }

      // STAKE: the provider's own bill is the truth when it sends one; the row's prices only fill in where it does not, with
      // cached tokens at the cached price, as the provider charges them.
      [Test]
      public void GIVEN_a_call_WHEN_priced_THEN_the_bill_wins_then_the_row_prices_then_free_else_unknown()
      {
         var priced = new CostScenario {InPerMillion = 1.0, CachedPerMillion = 0.1, OutPerMillion = 4.0};

         var tokens = new CallTokens {Prompt = 10_000, Cached = 8_000, Completion = 500};
         CostPricing.Price(priced, tokens, 0.5).Should().Be(((double?) 0.5, PriceSource.Bill));
         (double? cost, PriceSource source) = CostPricing.Price(priced, tokens, null);
         source.Should().Be(PriceSource.RowPrices);
         cost.Should().BeApproximately((2_000 * 1.0 + 8_000 * 0.1 + 500 * 4.0) / 1_000_000.0, 1e-12);
         CostPricing.Price(new CostScenario {Free = true}, tokens, null).Should().Be(((double?) 0.0, PriceSource.Free));
         CostPricing.Price(new CostScenario(), tokens, null).Should().Be(((double?) null, PriceSource.None));
      }

      // STAKE: a paid provider with no bill and no prices would spend past any cap unseen; such a row is not run.
      [Test]
      public void GIVEN_a_row_WHEN_its_provider_sends_no_bill_THEN_it_runs_only_with_prices_or_marked_free()
      {
         CostPricing.CanCap(new CostScenario(), providerBills: true).Should().BeTrue();
         CostPricing.CanCap(new CostScenario(), providerBills: false).Should().BeFalse();
         CostPricing.CanCap(new CostScenario {Free = true}, false).Should().BeTrue();
         CostPricing.CanCap(new CostScenario {InPerMillion = 0.2, OutPerMillion = 0.8}, false).Should().BeTrue();
      }

      // STAKE: 'plan' is read before anything is spent: it must say which provider each row uses and which rows will not run.
      [Test]
      public void GIVEN_rows_on_a_provider_without_a_bill_WHEN_planned_THEN_the_plan_names_the_provider_and_the_rows_that_will_not_run()
      {
         var rows = new[] {
            new CostScenario {Name = "Local", Pass = CostPass.Prose, Model = "llama", Free = true},
            new CostScenario {Name = "Unpriced", Pass = CostPass.Interpret, Model = "x"}
         };

         string plan = CostBenchReport.Plan(rows, 10, 6, 1, pass => ("NanoGpt", false));

         plan.Should().Contain("on NanoGpt, free").And.Contain("NOT RUN: NanoGpt sends no bill");
      }

      // STAKE: a local row's $0 is not a bill; the table says how the row was priced.
      [Test]
      public void GIVEN_calls_priced_from_the_row_or_free_WHEN_summed_THEN_the_row_note_says_so()
      {
         var row = new CostScenario {Name = "Local", Pass = CostPass.Interpret, Model = "llama", Free = true};
         var calls = new List<CostCall> {new CostCall {ScenarioName = "Local", Pass = CostPass.Interpret, Phase = CostPhase.Interpreter, Cost = 0, Source = PriceSource.Free}};
         var outcomes = new List<InterpretOutcome> {new InterpretOutcome {ScenarioName = "Local", Right = true}};

         CostBenchReport.SummarizeInterpret(row, calls, outcomes).Note.Should().Contain("free (local)");
      }
   }
}
