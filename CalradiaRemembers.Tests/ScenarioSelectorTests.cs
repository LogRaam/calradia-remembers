// Code written by Gabriel Mailhot, 24/06/2026.
// The keystone: trigger matching (incl. wildcard), one-shot exclusion, guard filtering, and the weighted
// random pool that makes "several rules on one trigger → one fires" — replayability with no branching. The
// RNG and the guard/fired facts are injected, so each path is deterministic here. The final test documents
// the flagship Rhagaea marriage as four rules on a single trigger.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScenarioSelectorTests
    {
        private static ScenarioRule Rule(string id, string trigger, int weight = 1, bool fireOnce = false, string[]? guards = null)
            => ScenarioRule.Parse(id, trigger, guards, null, $"msg-{id}", fireOnce, weight);

        // ── Trigger matching ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_same_verb_and_payload_WHEN_matching_THEN_it_matches_case_insensitively()
        {
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH:Rhagaea"), KeywordToken.Parse("on_death:rhagaea"))
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_wildcard_rule_payload_WHEN_matching_any_payload_of_the_same_verb_THEN_it_matches()
        {
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH:*"), KeywordToken.Parse("ON_DEATH:Caladog"))
                .Should().BeTrue();
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH"), KeywordToken.Parse("ON_DEATH:Caladog"))
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_different_payload_WHEN_matching_THEN_it_does_not_match()
        {
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH:Rhagaea"), KeywordToken.Parse("ON_DEATH:Caladog"))
                .Should().BeFalse();
        }

        // ── Selection: filtering ───────────────────────────────────────────────

        [Test]
        public void GIVEN_no_rule_matches_the_event_WHEN_selecting_THEN_it_returns_null()
        {
            var rules = new[] { Rule("a", "ON_DEATH:Rhagaea") };

            ScenarioSelector.Select(KeywordToken.Parse("ON_MARRIAGE:Rhagaea"), rules, null, null, _ => 0)
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_a_spent_one_shot_WHEN_selecting_THEN_it_is_excluded()
        {
            var rules = new[] { Rule("once", "ON_DEATH:Rhagaea", fireOnce: true) };
            var fired = new HashSet<string> { "once" };

            ScenarioSelector.Select(KeywordToken.Parse("ON_DEATH:Rhagaea"), rules, null, fired, _ => 0)
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_a_guard_that_fails_WHEN_selecting_THEN_the_rule_is_excluded()
        {
            var rules = new[] { Rule("b", "ON_MARRIAGE:Rhagaea", guards: new[] { "IF_PLAYER_RULES" }) };

            ScenarioSelector.Select(KeywordToken.Parse("ON_MARRIAGE:Rhagaea"), rules, _ => false, null, _ => 0)
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_a_single_eligible_rule_WHEN_selecting_THEN_it_is_returned_without_consulting_the_rng()
        {
            var rules = new[] { Rule("only", "ON_DEATH:Rhagaea") };
            bool rngUsed = false;

            ScenarioRule chosen = ScenarioSelector.Select(
                KeywordToken.Parse("ON_DEATH:Rhagaea"), rules, null, null,
                _ => { rngUsed = true; return 0; });

            chosen.Id.Should().Be("only");
            rngUsed.Should().BeFalse();
        }

        // ── Selection: the weighted random pool ────────────────────────────────

        [Test]
        public void GIVEN_three_equal_weight_rules_WHEN_the_rng_points_at_each_slot_THEN_that_rule_is_drawn()
        {
            var rules = new[]
            {
                Rule("a", "T"), Rule("b", "T"), Rule("c", "T")
            };

            KeywordToken evt = KeywordToken.Parse("T");
            ScenarioSelector.Select(evt, rules, null, null, _ => 0).Id.Should().Be("a");
            ScenarioSelector.Select(evt, rules, null, null, _ => 1).Id.Should().Be("b");
            ScenarioSelector.Select(evt, rules, null, null, _ => 2).Id.Should().Be("c");
        }

        [Test]
        public void GIVEN_weighted_rules_WHEN_drawing_THEN_the_weight_widens_a_rules_slot_range()
        {
            var rules = new[] { Rule("heavy", "T", weight: 2), Rule("light", "T", weight: 1) };
            KeywordToken evt = KeywordToken.Parse("T");

            // total weight 3: rolls 0,1 → heavy; roll 2 → light
            ScenarioSelector.Select(evt, rules, null, null, _ => 0).Id.Should().Be("heavy");
            ScenarioSelector.Select(evt, rules, null, null, _ => 1).Id.Should().Be("heavy");
            ScenarioSelector.Select(evt, rules, null, null, _ => 2).Id.Should().Be("light");
        }

        // ── The flagship: the Rhagaea marriage as four rules on one trigger ─────

        [Test]
        public void GIVEN_the_four_rhagaea_marriage_rules_WHEN_the_player_does_not_rule_THEN_the_two_crowns_rule_is_filtered_out()
        {
            var rules = RhagaeaMarriageRules();
            KeywordToken married = KeywordToken.Parse("ON_MARRIAGE:Rhagaea,Player");

            // The player rules nothing → guard IF_PLAYER_RULES fails → "two_crowns" is ineligible.
            bool PlayerRulesNothing(KeywordToken guard) => guard.Verb != "IF_PLAYER_RULES";

            IReadOnlyList<ScenarioRule> eligible =
                ScenarioSelector.Eligible(married, rules, PlayerRulesNothing, null);

            eligible.Select(r => r.Id).Should().BeEquivalentTo("consort", "regent", "suspicious");
            eligible.Should().NotContain(r => r.Id == "two_crowns");
        }

        [Test]
        public void GIVEN_the_four_rhagaea_marriage_rules_WHEN_the_player_rules_THEN_all_four_are_eligible_and_one_is_drawn()
        {
            var rules = RhagaeaMarriageRules();
            KeywordToken married = KeywordToken.Parse("ON_MARRIAGE:Rhagaea,Player");

            ScenarioRule chosen = ScenarioSelector.Select(married, rules, _ => true, null, _ => 1);

            chosen.Should().NotBeNull();
            chosen.Id.Should().Be("two_crowns"); // slot 1 of the four equal-weight rules
        }

        private static ScenarioRule[] RhagaeaMarriageRules()
        {
            const string trigger = "ON_MARRIAGE:Rhagaea,Player";
            return new[]
            {
                Rule("consort", trigger),
                Rule("two_crowns", trigger, guards: new[] { "IF_PLAYER_RULES" }),
                Rule("regent", trigger),
                Rule("suspicious", trigger)
            };
        }
    }
}
