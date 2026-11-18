// Code written by Gabriel Mailhot, 18/11/2026.

// A [standard] row of cr.cost_bench now answers staged cases (StandardActionCases) and its [ACTION] tags are scored, so one
// command gives a Standard model's action accuracy, speed, cost and cache (Gabriel, 18/11/2026: a benchmark offered to
// players and modders). The bench opens the gate that teaches each verb on the context, as the game would for a character
// who could do the deed (Gabriel's option B). These pin what breaks a player's reading of the table: a gate that names a
// context field that no longer exists (the verb is never taught, every case of it skipped), a score that counts a promise
// for later as a deed or an ordinary change of regard as an invention, and tagging calls that leak into the conversation's
// own cost, speed and cache columns.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class StandardActionCasesTests
   {
      private static readonly CostScenario GrokStandard = new CostScenario {Name = "Grok", Pass = CostPass.Standard, Model = "x-ai/grok-4.20"};

      // STAKE: the bench sets each gate by property name; a renamed or retyped context field would leave the verb untaught,
      // and every case of it would be skipped instead of scored.
      [Test]
      public void GIVEN_every_gate_WHEN_its_facts_are_set_on_a_context_THEN_each_names_a_writable_field_of_the_right_type()
      {
         foreach (StandardGate gate in Enum.GetValues(typeof(StandardGate)))
         {
            var ctx = new EncounterContext();
            foreach (KeyValuePair<string, object> fact in StandardActionCases.GateFacts(gate))
            {
               var property = typeof(EncounterContext).GetProperty(fact.Key);
               property.Should().NotBeNull($"{gate} sets {fact.Key}");
               property.SetValue(ctx, fact.Value);
               property.GetValue(ctx).Should().BeEquivalentTo(fact.Value);
            }
         }
      }

      // STAKE: a score over a handful of cases says little; the set must cover every difficulty and every gate, and a
      // positive case must expect the very verb whose teaching it checks, or the skip check guards the wrong word.
      [Test]
      public void GIVEN_the_staged_cases_WHEN_listed_THEN_they_cover_every_difficulty_and_gate_and_each_expects_its_own_verb()
      {
         IReadOnlyList<StandardActionCase> all = StandardActionCases.All;

         all.Select(c => c.Id).Should().OnlyHaveUniqueItems();
         all.Select(c => c.Difficulty).Distinct().Should().BeEquivalentTo((InterpretDifficulty[]) Enum.GetValues(typeof(InterpretDifficulty)));
         all.Select(c => c.Gate).Distinct().Should().BeEquivalentTo((StandardGate[]) Enum.GetValues(typeof(StandardGate)));
         foreach (StandardActionCase c in all.Where(c => c.Scoring.Expected.Count > 0))
            c.Scoring.Expected.Select(e => e.Type).Should().Contain(c.Verb, c.Id);
         foreach (StandardActionCase c in all.Where(c => c.Difficulty == InterpretDifficulty.FalseLead))
            c.Scoring.ForbiddenType.Should().Be(c.Verb, c.Id);
      }

      // STAKE: the cacheable prefix changes with the gate; cases kept together by gate let the cache column measure the
      // model, not the order the cases were written in.
      [Test]
      public void GIVEN_the_staged_cases_WHEN_run_in_order_THEN_each_gate_comes_in_one_unbroken_run()
      {
         List<StandardGate> order = StandardActionCases.All.Select(c => c.Gate).ToList();
         var runs = new List<StandardGate>();
         foreach (StandardGate g in order)
            if (runs.Count == 0 || runs[runs.Count - 1] != g)
               runs.Add(g);

         runs.Should().OnlyHaveUniqueItems();
      }

      // STAKE: the agreed hundred paid is a right tag; the wrong sum is not (the game would pay what the tag says).
      [Test]
      public void GIVEN_the_agreed_payment_WHEN_the_reply_tags_it_THEN_the_right_sum_scores_and_a_wrong_one_does_not()
      {
         StandardActionCase paid = Case("give_gold_agreed");

         StandardActionCases.Score(paid, new[] {Gold("give_gold", "100")}).Should().Be((true, false));
         StandardActionCases.Score(paid, new[] {Gold("give_gold", "500")}).Right.Should().BeFalse();
         StandardActionCases.Score(paid, new GameAction[0]).Right.Should().BeFalse();
      }

      // STAKE: a payment only promised for next week, tagged as paid, is a deed invented: the game would hand over coin
      // for work not done.
      [Test]
      public void GIVEN_a_payment_spoken_of_for_later_WHEN_the_reply_tags_it_THEN_it_is_an_invented_deed()
      {
         StandardActionCase later = Case("give_gold_later");

         StandardActionCases.Score(later, new GameAction[0]).Should().Be((true, false));
         StandardActionCases.Score(later, new[] {Gold("give_gold", "100")}).Should().Be((false, true));
      }

      // STAKE: in an ordinary talk a change of regard is a judgement, not a deed; anything else is an invention.
      [Test]
      public void GIVEN_an_ordinary_line_WHEN_the_reply_tags_only_a_change_of_regard_THEN_it_is_right_and_a_payment_is_invented()
      {
         StandardActionCase ordinary = Case("ordinary_roads");

         StandardActionCases.Score(ordinary, new[] {new GameAction {Type = "change_relation"}}).Should().Be((true, false));
         StandardActionCases.Score(ordinary, new[] {Gold("give_gold", "50")}).Should().Be((false, true));
      }

      // STAKE: a case whose verb the prompt never taught would score the bench, not the model; it is skipped.
      [Test]
      public void GIVEN_a_prompt_WHEN_the_case_verb_is_absent_THEN_the_case_is_not_taught_and_an_ordinary_line_always_is()
      {
         StandardActionCases.Taught(Case("ride_with_me_asked"), "GAME ACTIONS: give_gold, take_gold").Should().BeFalse();
         StandardActionCases.Taught(Case("ride_with_me_asked"), "... ride_with_me: the character rides in your party ...").Should().BeTrue();
         StandardActionCases.Taught(Case("ordinary_tax"), "").Should().BeTrue();
      }

      // STAKE: the staged cases are many short calls; counted in the conversation's columns they would change every
      // Standard row's thinking and cache shares. They stay in their own table.
      [Test]
      public void GIVEN_a_standard_row_with_tagging_calls_WHEN_its_conversation_is_summed_THEN_they_stay_out_and_have_their_own_table()
      {
         var calls = new List<CostCall> {
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Standard, Phase = CostPhase.Prose, Turn = 8, Cost = 0.004, Seconds = 5, PromptTokens = 1000, CachedTokens = 0},
            new CostCall {ScenarioName = "Grok", Pass = CostPass.Standard, Phase = CostPhase.Tagging, Difficulty = InterpretDifficulty.Simple, Cost = 0.001, Seconds = 2, PromptTokens = 1000, CachedTokens = 900}
         };
         var outcomes = new List<InterpretOutcome> {new InterpretOutcome {ScenarioName = "Grok", Difficulty = InterpretDifficulty.Simple, Right = true}};

         ProseRowResult p = CostBenchReport.SummarizeProse(GrokStandard, calls, new[] {"a"});
         InterpretRowResult t = CostBenchReport.SummarizeInterpret(GrokStandard, calls.Where(c => c.Phase == CostPhase.Tagging).ToList(), outcomes);

         p.CachedShare.Should().Be(0);
         p.Middle.Should().BeApproximately(0.004, 1e-9);
         t.CachedShare.Should().BeApproximately(0.9, 1e-9);
         t.Accuracy.Right.Should().Be(1);
         string table = CostBenchReport.Markdown(new[] {p}, new InterpretRowResult[0], new FoldRowResult[0], "v2.8.16", new DateTime(2026, 11, 18), new[] {t});
         table.Should().Contain("Tagged right (95%)").And.Contain("| 90% |");
         CostBenchReport.NexusCode(new[] {p}, new InterpretRowResult[0], "v2.8.16", new DateTime(2026, 11, 18), new[] {t}).Should().Contain("Tagged right");
      }

      // STAKE: 'plan' is what a player reads before spending: it must count the staged calls a Standard row adds.
      [Test]
      public void GIVEN_a_standard_row_WHEN_planned_THEN_the_plan_counts_its_staged_deeds()
      {
         string plan = CostBenchReport.Plan(new[] {GrokStandard}, 10, 6, 2);

         plan.Should().Contain($"{StandardActionCases.All.Count} staged deeds").And.Contain($"about {10 + StandardActionCases.All.Count} calls");
      }

      private static StandardActionCase Case(string id) => StandardActionCases.All.Single(c => c.Id == id);

      private static GameAction Gold(string type, string amount) => new GameAction {Type = type, Parameters = new Dictionary<string, string> {{"amount", amount}}};
   }
}
