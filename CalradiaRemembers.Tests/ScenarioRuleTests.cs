// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the rule's four fields (+ fire-once, weight) and that ScenarioRule.Parse turns the raw DSL
// strings a JSON pack carries into tokens. The weight clamp is a small but load-bearing guarantee for the
// weighted draw, so it is pinned here.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScenarioRuleTests
    {
        [Test]
        public void GIVEN_raw_dsl_strings_WHEN_parsing_a_rule_THEN_trigger_guards_and_consequences_become_tokens()
        {
            ScenarioRule rule = ScenarioRule.Parse(
                id: "rhagaea_two_crowns",
                trigger: "ON_MARRIAGE:Rhagaea,Player",
                guards: new[] { "IF_PLAYER_RULES" },
                consequences: new[] { "CHANGE_RELATION:Rhagaea:+10", "INJECT_MEMORY" },
                message: "Two thrones are joined.");

            rule.Id.Should().Be("rhagaea_two_crowns");
            rule.Trigger.Should().Be(KeywordToken.Parse("ON_MARRIAGE:Rhagaea,Player"));
            rule.Guards.Should().ContainSingle().Which.Verb.Should().Be("IF_PLAYER_RULES");
            rule.Consequences.Should().HaveCount(2);
            rule.Consequences[0].Verb.Should().Be("CHANGE_RELATION");
            rule.Message.Should().Be("Two thrones are joined.");
        }

        [Test]
        public void GIVEN_no_guards_or_consequences_WHEN_parsing_THEN_they_default_to_empty_not_null()
        {
            ScenarioRule rule = ScenarioRule.Parse("r", "ON_DEATH:Rhagaea", null, null, "msg");

            rule.Guards.Should().BeEmpty();
            rule.Consequences.Should().BeEmpty();
            rule.Message.Should().Be("msg");
        }

        [Test]
        public void GIVEN_blank_lines_among_consequences_WHEN_parsing_THEN_they_are_skipped()
        {
            ScenarioRule rule = ScenarioRule.Parse(
                "r", "ON_DEATH:Rhagaea", null,
                new[] { "DECLARE_WAR:A->B", "   ", "" }, "msg");

            rule.Consequences.Should().ContainSingle().Which.Verb.Should().Be("DECLARE_WAR");
        }

        [Test]
        public void GIVEN_a_non_positive_weight_WHEN_building_a_rule_THEN_it_is_clamped_to_one()
        {
            ScenarioRule.Parse("a", "T", null, null, "m", weight: 0).Weight.Should().Be(1);
            ScenarioRule.Parse("b", "T", null, null, "m", weight: -5).Weight.Should().Be(1);
            ScenarioRule.Parse("c", "T", null, null, "m", weight: 3).Weight.Should().Be(3);
        }

        [Test]
        public void GIVEN_defaults_WHEN_building_a_rule_THEN_it_is_repeatable_with_weight_one()
        {
            ScenarioRule rule = ScenarioRule.Parse("r", "T", null, null, "m");

            rule.FireOnce.Should().BeFalse();
            rule.Weight.Should().Be(1);
        }
    }
}
