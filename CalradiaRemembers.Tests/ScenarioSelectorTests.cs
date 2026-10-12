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

        // Authored packs are hand-typed JSON; a modder capitalizing "on_death" instead of "ON_DEATH" (or
        // vice versa) must still arm the rule, or trigger matching would silently depend on exact casing
        // nobody documents.
        [Test]
        public void GIVEN_same_verb_and_payload_WHEN_matching_THEN_it_matches_case_insensitively()
        {
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH:Rhagaea"), KeywordToken.Parse("on_death:rhagaea"))
                .Should().BeTrue();
        }

        // The wildcard payload (empty or "*") is how a modder writes one catch-all rule ("any death") instead
        // of enumerating every hero by name. If wildcard matching broke, every catch-all rule in a pack would
        // silently never fire.
        [Test]
        public void GIVEN_a_wildcard_rule_payload_WHEN_matching_any_payload_of_the_same_verb_THEN_it_matches()
        {
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH:*"), KeywordToken.Parse("ON_DEATH:Caladog"))
                .Should().BeTrue();
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH"), KeywordToken.Parse("ON_DEATH:Caladog"))
                .Should().BeTrue();
        }

        // Without this, a rule authored for a specific named hero (e.g. Rhagaea) could fire on any hero's
        // death, misapplying content written for one character to whoever happened to die.
        [Test]
        public void GIVEN_a_different_payload_WHEN_matching_THEN_it_does_not_match()
        {
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_DEATH:Rhagaea"), KeywordToken.Parse("ON_DEATH:Caladog"))
                .Should().BeFalse();
        }

        // The tale-scenario bridge (Gabriel, 12/10/2026, the Troy-inspired tale): a tale fires ON_TALE:<tale>:<stage>, and
        // ON_TALE:<tale>:end:<ending>. A rule naming the tale alone answers every step of it, one naming the stage or the
        // ending answers only that; another tale whose id merely begins the same never answers.
        [Test]
        public void GIVEN_a_rule_naming_a_tale_WHEN_one_of_its_steps_fires_THEN_it_answers_and_a_rule_naming_the_step_answers_only_it()
        {
            KeywordToken eloped = KeywordToken.Parse("ON_TALE:cr_kings_hall:eloped");

            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_TALE:cr_kings_hall"), eloped).Should().BeTrue();
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_TALE:cr_kings_hall:eloped"), eloped).Should().BeTrue();
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_TALE:cr_kings_hall:refused"), eloped).Should().BeFalse();
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_TALE:cr_kings"), eloped).Should().BeFalse();
            ScenarioSelector.TriggerMatches(KeywordToken.Parse("ON_TALE:cr_kings_hall:end"), KeywordToken.Parse("ON_TALE:cr_kings_hall:end:returned")).Should().BeTrue();
        }

        // ── Selection: filtering ───────────────────────────────────────────────

        // ScenarioRuntime.Fire calls Select on every relevant CampaignEvent; the common case is that nothing
        // authored matches. It must return null cleanly rather than throw or pick something unrelated.
        [Test]
        public void GIVEN_no_rule_matches_the_event_WHEN_selecting_THEN_it_returns_null()
        {
            var rules = new[] { Rule("a", "ON_DEATH:Rhagaea") };

            ScenarioSelector.Select(KeywordToken.Parse("ON_MARRIAGE:Rhagaea"), rules, null, null, _ => 0)
                .Should().BeNull();
        }

        // Anti-save-scum: FireOnce rules seal into a persisted fired-id set (ScenarioBehavior). If this
        // filter broke, a one-shot flavor rule (e.g. one of the four Rhagaea-marriage outcomes) could refire
        // on a reload or a later matching event, breaking the "sealed once drawn" guarantee.
        [Test]
        public void GIVEN_a_spent_one_shot_WHEN_selecting_THEN_it_is_excluded()
        {
            var rules = new[] { Rule("once", "ON_DEATH:Rhagaea", fireOnce: true) };
            var fired = new HashSet<string> { "once" };

            ScenarioSelector.Select(KeywordToken.Parse("ON_DEATH:Rhagaea"), rules, null, fired, _ => 0)
                .Should().BeNull();
        }

        // A failing guard must remove the rule from the pool entirely, not merely deprioritize it: else a
        // rule authored to require a precondition (e.g. IF_PLAYER_RULES) could still be drawn and its
        // consequence could fire on a player who does not meet it.
        [Test]
        public void GIVEN_a_guard_that_fails_WHEN_selecting_THEN_the_rule_is_excluded()
        {
            var rules = new[] { Rule("b", "ON_MARRIAGE:Rhagaea", guards: new[] { "IF_PLAYER_RULES" }) };

            ScenarioSelector.Select(KeywordToken.Parse("ON_MARRIAGE:Rhagaea"), rules, _ => false, null, _ => 0)
                .Should().BeNull();
        }

        // The overwhelmingly common case (one rule per trigger) must stay fully deterministic and never touch
        // the injected RNG at all, both to keep single-rule content predictable and to catch a Select that
        // wastefully or incorrectly rolls even when there is nothing to choose between.
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

        // Pins the mapping between an injected RNG roll and which of several equal-weight rules is drawn:
        // the exact mechanism that lets several rules share a trigger (e.g. the four Rhagaea outcomes) and
        // still resolve deterministically for a given roll, keeping the whole engine testable end to end.
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

        // Weight is the modder's one dial for biasing which of several rules on a shared trigger tends to
        // fire (per ScenarioRule's doc, default uniform). If the slot math were off, an authored "weight: 2"
        // would not actually double a rule's odds relative to its siblings.
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

        // This is the actual shipped content pack (ModuleData/scenarios/rhagaea_marriage.json, v1.14.0): four
        // outcomes on ON_MARRIAGE, one guarded by IF_PLAYER_RULES. A regression here would silently break the
        // shipped "two thrones" outcome, the proof case the whole guards-then-random-draw mechanism was built for.
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

        // Confirms the complementary case of the flagship above: once the guard passes, ALL four outcomes
        // enter the pool (not just the newly-unlocked one), and the weighted draw still resolves to a single
        // named winner for a given roll, end to end.
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
