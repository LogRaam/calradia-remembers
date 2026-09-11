// Code written by Gabriel Mailhot, 04/08/2026.
// This policy is the single gate standing between an LLM's [ACTION] and a REAL, IRREVERSIBLE kill of the
// player's character (game over, or heir succession where a clan heir exists). It exists because a player asked
// for exactly this (parity with the "AI Executes" mod, COUNCIL_ACTIONS.md Partie 10.6), but the stakes demand
// every guard hold at once: opt-in MCM (default OFF), genuine captivity, the same Hardcore/Adult gate every
// other captive-scene act requires, and Bannerlord's own campaign death setting. What breaks if this is wrong:
// get it wrong toward "true" and a stray or premature emission permanently ends a player's campaign; get it
// wrong toward "false" and the feature a player explicitly asked for silently never fires.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PlayerExecutionPolicyTests
    {
        // STAKE: the feature ships OFF. A player who never touches the MCM toggle must NEVER have their
        // character killed by a captor scene, no matter what else lines up (captive, Hardcore, death allowed).
        [Test]
        public void GIVEN_the_MCM_toggle_is_off_WHEN_every_other_guard_holds_THEN_execution_is_refused()
        {
            PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = false, PlayerIsCaptive = true, Hardcore = true, DeathAllowed = true})
                .Should().BeFalse();
        }

        // STAKE: execute_player must never fire outside an actual captive scene. Without this guard, a stray
        // emission (or a bug mis-detecting captivity) could kill the player during an ordinary conversation,
        // exactly the "stray RP line" the design forbids (COUNCIL_ACTIONS.md 10.6).
        [Test]
        public void GIVEN_the_player_is_not_captive_WHEN_every_other_guard_holds_THEN_execution_is_refused()
        {
            PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = true, PlayerIsCaptive = false, Hardcore = true, DeathAllowed = true})
                .Should().BeFalse();
        }

        // STAKE: the rest of the captive-scene vocabulary (harm_prisoner, the CNC framing) is gated on
        // Hardcore; execution, being strictly graver, must never be looser than that same floor.
        [Test]
        public void GIVEN_content_level_is_below_Hardcore_WHEN_every_other_guard_holds_THEN_execution_is_refused()
        {
            PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = true, PlayerIsCaptive = true, Hardcore = false, DeathAllowed = true})
                .Should().BeFalse();
        }

        // STAKE: Bannerlord's own campaign death setting (IsLifeDeathCycleDisabled) is the player's standing
        // choice about whether their character can die at all; this optional feature must never override it.
        [Test]
        public void GIVEN_the_campaign_death_setting_disallows_death_WHEN_every_other_guard_holds_THEN_execution_is_refused()
        {
            PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = true, PlayerIsCaptive = true, Hardcore = true, DeathAllowed = false})
                .Should().BeFalse();
        }

        // STAKE: only when ALL FOUR guards hold simultaneously does the captor's deliberate choice become real.
        [Test]
        public void GIVEN_every_guard_holds_THEN_execution_is_permitted()
        {
            PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = true, PlayerIsCaptive = true, Hardcore = true, DeathAllowed = true})
                .Should().BeTrue();
        }

        // STAKE: exhaustive combination coverage (the full 2^4 truth table), not just the five hand-picked
        // cases above, because this is a pure AND across four booleans and permadeath tolerates no untested
        // corner: every combination but all-true must refuse.
        [TestCase(false, false, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(false, true, false, false, false)]
        [TestCase(false, false, true, false, false)]
        [TestCase(false, false, false, true, false)]
        [TestCase(true, true, false, false, false)]
        [TestCase(true, false, true, false, false)]
        [TestCase(true, false, false, true, false)]
        [TestCase(false, true, true, false, false)]
        [TestCase(false, true, false, true, false)]
        [TestCase(false, false, true, true, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, false, true, true, false)]
        [TestCase(false, true, true, true, false)]
        [TestCase(true, true, true, true, true)]
        public void GIVEN_every_combination_of_the_four_guards_THEN_only_all_true_permits_execution(
            bool mcmOn, bool playerIsCaptive, bool hardcore, bool deathAllowed, bool expected)
        {
            PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = mcmOn, PlayerIsCaptive = playerIsCaptive, Hardcore = hardcore, DeathAllowed = deathAllowed}).Should().Be(expected);
        }
    }
}
