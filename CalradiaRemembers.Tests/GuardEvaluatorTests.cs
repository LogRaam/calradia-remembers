// Code written by Gabriel Mailhot, 24/06/2026.
// Pins the guard contract: AND-combined, empty passes, short-circuits on the first failure. The resolver is
// injected (the engine knows what each keyword means against real game state), so these tests stub it.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class GuardEvaluatorTests
    {
        private static KeywordToken[] Guards(params string[] raw)
        {
            var list = new List<KeywordToken>();
            foreach (string r in raw) list.Add(KeywordToken.Parse(r));
            return list.ToArray();
        }

        // Per GuardEvaluator's own doc, guards are optional and most authored rules will have none at all.
        // If empty/null guards failed instead of passing, every unguarded rule in every pack would become
        // silently unselectable, breaking the majority of the scripted-scenarios engine's content.
        [Test]
        public void GIVEN_no_guards_WHEN_evaluating_THEN_it_passes()
        {
            GuardEvaluator.AllPass(null, _ => false).Should().BeTrue();
            GuardEvaluator.AllPass(Guards(), _ => false).Should().BeTrue();
        }

        // The baseline AND-combination: a rule authored with several guards (e.g. the shipped Rhagaea pack's
        // IF_PLAYER_INVOLVED + IF_HERO_IS:Rhagaea) must have ALL of them hold, not just one, before it is
        // eligible.
        [Test]
        public void GIVEN_all_guards_resolve_true_WHEN_evaluating_THEN_it_passes()
        {
            GuardEvaluator.AllPass(Guards("IF_PLAYER_RULES", "IF_AT_WAR:Vlandia"), _ => true)
                .Should().BeTrue();
        }

        // One failing guard among several must veto the whole rule (AND, not OR/majority): this is what stops
        // e.g. the Rhagaea "two_crowns" outcome from firing just because its other guards happen to hold when
        // the player does not actually rule a kingdom.
        [Test]
        public void GIVEN_one_guard_resolves_false_WHEN_evaluating_THEN_it_fails()
        {
            bool Resolve(KeywordToken g) => g.Verb != "IF_PLAYER_RULES"; // this one is false

            GuardEvaluator.AllPass(Guards("IF_AT_WAR:Vlandia", "IF_PLAYER_RULES"), Resolve)
                .Should().BeFalse();
        }

        // Guards resolve against live game state (Hero.MainHero, faction lookups) via the engine resolver, so
        // needless further lookups are wasted work once one guard has already failed. Pins the short-circuit
        // (per GuardEvaluator's own doc) so a change to the loop cannot silently start resolving every guard.
        [Test]
        public void GIVEN_a_failing_guard_first_WHEN_evaluating_THEN_it_short_circuits_and_skips_the_rest()
        {
            var seen = new List<string>();

            bool Resolve(KeywordToken g)
            {
                seen.Add(g.Verb);
                return false; // first one already fails
            }

            GuardEvaluator.AllPass(Guards("FIRST", "SECOND"), Resolve).Should().BeFalse();
            seen.Should().Equal("FIRST"); // SECOND was never resolved
        }
    }
}
