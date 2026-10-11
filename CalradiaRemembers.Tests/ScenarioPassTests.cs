// Code written by Gabriel Mailhot, 11/10/2026.
// Decides which rules a fired event plays. Gabriel (11/10/2026): "Si plusieurs regles existent pour la mort de Rhagaea,
// tous devraient etre joues", e.g. one that starts a war and another where a bastard heir claims the throne; and guards
// must protect the game by checking that its state allows the scenario. Before, ONE rule was drawn among all eligible
// ones, so a mod's rule could take the place of CR's. Now every eligible rule plays, in reading order, each checked at
// its own turn (it sees what the rules before it changed); rules sharing a GROUP are alternatives (the four outcomes of
// marrying Rhagaea), one drawn by weight; and a rule plays only when ALL its consequences can apply, so the realm is
// never told of a war that was not declared. If this is wrong, scenarios vanish, contradict each other, or lie.

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
    public sealed class ScenarioPassTests
    {
        private static readonly KeywordToken OnDeath = KeywordToken.Parse("ON_DEATH");

        private static ScenarioRule Rule(string id, string group = null, int weight = 1, bool once = true,
                                         string[] guards = null, string[] consequences = null, string trigger = "ON_DEATH")
            => ScenarioRule.Parse(id, trigger, guards, consequences, "message of " + id, once, weight, group);

        // The world as a test sees it: guards and consequences answer from these sets, and playing a rule records it.
        private sealed class World
        {
            public readonly List<string> Played = new();
            public readonly HashSet<string> Sealed = new();
            public readonly HashSet<string> FailingGuards = new();
            public readonly HashSet<string> ImpossibleConsequences = new();
            public int Roll;

            public ScenarioChecks Checks() => new()
            {
                Sealed = Sealed,
                GuardPasses = g => !FailingGuards.Contains(g.ToString()),
                ConsequenceApplies = c => !ImpossibleConsequences.Contains(c.ToString()),
                NextBelow = _ => Roll,
                Play = r =>
                {
                    Played.Add(r.Id);
                    if (r.FireOnce) Sealed.Add(r.Id);
                }
            };
        }

        // STAKE: Gabriel's own example. On Rhagaea's death one scenario starts a war and another raises a pretender; with a
        // single draw one of the two was silently lost, at random.
        [Test]
        public void GIVEN_two_rules_for_the_same_death_WHEN_she_dies_THEN_both_are_played_in_reading_order()
        {
            var w = new World();

            ScenarioPass.Play(OnDeath, new[] {Rule("war"), Rule("pretender")}, w.Checks());

            w.Played.Should().Equal("war", "pretender");
        }

        // STAKE: the four outcomes of marrying Rhagaea are alternatives; played together she would remember four marriages
        // that contradict each other. A rule outside the group still plays beside the one drawn.
        [Test]
        public void GIVEN_alternatives_in_a_group_and_a_rule_outside_it_WHEN_the_event_fires_THEN_one_alternative_and_the_other_rule_play()
        {
            var w = new World();

            ScenarioPass.Play(OnDeath, new[] {Rule("consort", "marriage"), Rule("regent", "marriage"), Rule("chronicle")}, w.Checks());

            w.Played.Should().Equal("consort", "chronicle");
        }

        // STAKE: weight still biases which alternative is drawn, as authors were told; a roll past the first weight lands
        // on the second.
        [Test]
        public void GIVEN_weighted_alternatives_WHEN_the_roll_passes_the_first_weight_THEN_the_second_is_played()
        {
            var w = new World {Roll = 3};

            ScenarioPass.Play(OnDeath, new[] {Rule("consort", "marriage", 3), Rule("regent", "Marriage ", 1)}, w.Checks());

            w.Played.Should().Equal("regent");
        }

        // STAKE: an alternative whose guard fails now is not in the draw; the group still plays one of the others rather
        // than nothing, and the draw counts only the eligible ones' weights.
        [Test]
        public void GIVEN_an_alternative_whose_guard_fails_WHEN_the_group_draws_THEN_only_the_eligible_ones_are_drawn()
        {
            var w = new World {Roll = 0};
            w.FailingGuards.Add("IF_PLAYER_RULES");

            ScenarioPass.Play(OnDeath, new[] {Rule("two_crowns", "marriage", guards: new[] {"IF_PLAYER_RULES"}), Rule("regent", "marriage")}, w.Checks());

            w.Played.Should().Equal("regent");
        }

        // STAKE: "des guards devraient proteger le jeu". If the North is already at war with the South, the rule that declares
        // that war must not play: otherwise the realm is told the eagles turned while nothing changed. It is not sealed, so
        // the next time the same trigger fires it is asked again (a death happens once, so for Rhagaea's death that is the
        // end of it; a rule on a repeating event gets its chance again).
        [Test]
        public void GIVEN_a_rule_one_of_whose_consequences_cannot_apply_WHEN_the_event_fires_THEN_it_does_not_play_and_is_not_sealed()
        {
            var w = new World();
            w.ImpossibleConsequences.Add("DECLARE_WAR:empire,empire_s");

            ScenarioPass.Play(OnDeath, new[] {Rule("eagles", consequences: new[] {"DECLARE_WAR:empire,empire_s", "DECLARE_WAR:empire_w,empire_s"})}, w.Checks());

            w.Played.Should().BeEmpty();
            w.Sealed.Should().BeEmpty();
        }

        // STAKE: a rule that only tells the realm something (no consequence) has nothing that could fail; it plays.
        [Test]
        public void GIVEN_a_rule_with_a_message_and_no_consequence_WHEN_the_event_fires_THEN_it_plays()
        {
            var w = new World();

            ScenarioPass.Play(OnDeath, new[] {Rule("chronicle")}, w.Checks());

            w.Played.Should().Equal("chronicle");
        }

        // STAKE: rules play one after the other, each checked at its own turn. A second rule guarded on "these two are at
        // peace" must see the war the first one has just declared, or two packs would act on a world that no longer is.
        [Test]
        public void GIVEN_a_rule_whose_guard_the_previous_rule_breaks_WHEN_the_event_fires_THEN_the_second_sees_the_change_and_does_not_play()
        {
            var w = new World();
            ScenarioChecks checks = w.Checks();
            ScenarioChecks watching = new()
            {
                Sealed = checks.Sealed, ConsequenceApplies = checks.ConsequenceApplies, NextBelow = checks.NextBelow,
                GuardPasses = checks.GuardPasses,
                Play = r =>
                {
                    checks.Play(r);
                    w.FailingGuards.Add("IF_AT_PEACE:empire,empire_s");
                }
            };

            ScenarioPass.Play(OnDeath, new[] {Rule("war"), Rule("treaty", guards: new[] {"IF_AT_PEACE:empire,empire_s"})}, watching);

            w.Played.Should().Equal("war");
        }

        // STAKE: a one-shot already sealed in the save must never play again (no reloading for another outcome); a
        // repeatable rule is not held back by it.
        [Test]
        public void GIVEN_a_sealed_one_shot_and_a_repeatable_rule_WHEN_the_event_fires_THEN_only_the_repeatable_one_plays()
        {
            var w = new World();
            w.Sealed.Add("eagles");

            ScenarioPass.Play(OnDeath, new[] {Rule("eagles"), Rule("dirge", once: false)}, w.Checks());

            w.Played.Should().Equal("dirge");
        }

        // STAKE: a group whose alternatives were all played before (one sealed per marriage) or are all ineligible plays
        // nothing, and the rules after it still play.
        [Test]
        public void GIVEN_a_group_with_no_eligible_alternative_WHEN_the_event_fires_THEN_it_plays_nothing_and_the_next_rule_still_plays()
        {
            var w = new World();
            w.Sealed.Add("consort");

            ScenarioPass.Play(OnDeath, new[] {Rule("consort", "marriage"), Rule("chronicle")}, w.Checks());

            w.Played.Should().Equal("chronicle");
        }

        // STAKE: a rule on another trigger is not part of this event at all.
        [Test]
        public void GIVEN_a_rule_on_another_trigger_WHEN_a_death_fires_THEN_it_does_not_play()
        {
            var w = new World();

            ScenarioPass.Play(OnDeath, new[] {Rule("wedding", trigger: "ON_MARRIAGE"), Rule("dirge")}, w.Checks());

            w.Played.Should().Equal("dirge");
        }

        // STAKE: what the event played is returned, so the console and the self-tests can say what happened.
        [Test]
        public void GIVEN_rules_that_play_WHEN_the_event_fires_THEN_the_played_rules_are_returned()
        {
            var w = new World();

            ScenarioPass.Play(OnDeath, new[] {Rule("war"), Rule("pretender")}, w.Checks()).Select(r => r.Id).Should().Equal("war", "pretender");
        }

        // STAKE: a blank group in a hand-typed pack means "no group", not one shared group that would make every such
        // rule an alternative of the others.
        [Test]
        public void GIVEN_blank_groups_WHEN_rules_are_read_THEN_they_are_not_grouped_together()
        {
            Rule("a", "  ").Group.Should().BeNull();
            var w = new World();

            ScenarioPass.Play(OnDeath, new[] {Rule("a", " "), Rule("b", "")}, w.Checks());

            w.Played.Should().Equal("a", "b");
        }
    }
}
