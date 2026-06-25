// Code written by Gabriel Mailhot, 21/06/2026.
// Protects the price of meddling in a scheme: when an abetted plot's exposure turns the wronged
// party against the player. The rule breaks in silence — too eager and helping is suicidal, too lax
// and there is no risk to weigh.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SchemeBacklashTests
    {
        [Test]
        public void GIVEN_a_caught_plot_against_the_players_own_realm_WHEN_pricing_THEN_it_is_high_treason()
        {
            //Arrange — same faction dominates: treason even if the target is a personal rival.
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, targetSharesPlayerFaction: true, playerRelationWithTarget: -40)
                .Should().Be(SchemeBacklash.AbettorTreasonPenalty);
        }

        [Test]
        public void GIVEN_a_caught_plot_against_a_friend_of_another_realm_WHEN_pricing_THEN_it_is_a_grave_betrayal()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Succeeded, targetSharesPlayerFaction: false, playerRelationWithTarget: 25)
                .Should().Be(SchemeBacklash.AbettorFriendPenalty);
        }

        [Test]
        public void GIVEN_a_caught_plot_against_a_stranger_WHEN_pricing_THEN_the_cost_is_moderate()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, targetSharesPlayerFaction: false, playerRelationWithTarget: 0)
                .Should().Be(SchemeBacklash.AbettorNeutralPenalty);
        }

        [Test]
        public void GIVEN_a_caught_plot_against_an_existing_enemy_WHEN_pricing_THEN_it_stings_least()
        {
            //Arrange — moving against someone you already feuded with is half-expected.
            SchemeBacklash.AbettorPenalty(SchemeState.Succeeded, targetSharesPlayerFaction: false, playerRelationWithTarget: -20)
                .Should().Be(SchemeBacklash.AbettorEnemyPenalty);
        }

        [Test]
        public void GIVEN_relations_exactly_at_the_friend_and_enemy_thresholds_WHEN_pricing_THEN_the_tiers_are_inclusive()
        {
            //Arrange — +10 reads as a friend, -10 as an enemy.
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, false, 10).Should().Be(SchemeBacklash.AbettorFriendPenalty);
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, false, -10).Should().Be(SchemeBacklash.AbettorEnemyPenalty);
        }

        [Test]
        public void GIVEN_an_abetted_plot_that_fizzled_unseen_WHEN_pricing_THEN_there_is_no_cost_whoever_the_target()
        {
            //Arrange — a plot that simply failed without exposure never surfaces the player's role.
            SchemeBacklash.AbettorPenalty(SchemeState.Failed, targetSharesPlayerFaction: true, playerRelationWithTarget: 50)
                .Should().Be(0);
        }

        [Test]
        public void GIVEN_a_plot_still_in_motion_WHEN_pricing_THEN_nothing_is_owed_yet()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Plotted, false, 0).Should().Be(0);
            SchemeBacklash.AbettorPenalty(SchemeState.Progressing, true, 0).Should().Be(0);
        }
    }

    [TestFixture]
    public sealed class SchemeBacklash_WarnedPlotterResentmentTests
    {
        [Test]
        public void GIVEN_a_neutral_plotter_WHEN_computing_resentment_THEN_the_base_resentment_applies()
        {
            SchemeBacklash.WarnedPlotterResentment(mercy: 0, calculating: 0).Should().Be(30);
        }

        [Test]
        public void GIVEN_a_merciful_plotter_WHEN_computing_resentment_THEN_they_bear_the_betrayal_with_more_grace()
        {
            // mercy=+2 → 30 - 6 = 24; calculating=0 has no effect.
            SchemeBacklash.WarnedPlotterResentment(mercy: 2, calculating: 0).Should().Be(24);
        }

        [Test]
        public void GIVEN_a_vindictive_calculating_plotter_WHEN_computing_resentment_THEN_the_hate_is_at_the_ceiling()
        {
            // mercy=-2, calculating=+2 → 30 + 6 + 6 = 42, clamped to 40.
            SchemeBacklash.WarnedPlotterResentment(mercy: -2, calculating: 2).Should().Be(40);
        }

        [Test]
        public void GIVEN_traits_that_would_underflow_WHEN_clamping_THEN_the_floor_is_twenty()
        {
            // mercy=+2, calculating=-2 → 30 - 6 - 6 = 18, clamped to 20.
            SchemeBacklash.WarnedPlotterResentment(mercy: 2, calculating: -2).Should().Be(20);
        }

        [Test]
        public void GIVEN_traits_that_would_overflow_WHEN_clamping_THEN_the_ceiling_is_forty()
        {
            // Any combination exceeding 40 is capped.
            SchemeBacklash.WarnedPlotterResentment(mercy: -2, calculating: 2).Should().Be(40);
        }
    }

    [TestFixture]
    public sealed class SchemeBacklash_WarnedTargetGratitudeTests
    {
        [Test]
        public void GIVEN_a_neutral_target_WHEN_computing_gratitude_THEN_the_base_gratitude_applies()
        {
            SchemeBacklash.WarnedTargetGratitude(honor: 0, calculating: 0).Should().Be(25);
        }

        [Test]
        public void GIVEN_an_honorable_target_WHEN_computing_gratitude_THEN_they_feel_a_genuine_debt()
        {
            // honor=+2 → 25 + 6 = 31; calculating=0 has no effect.
            SchemeBacklash.WarnedTargetGratitude(honor: 2, calculating: 0).Should().Be(31);
        }

        [Test]
        public void GIVEN_a_calculating_target_WHEN_computing_gratitude_THEN_they_discount_the_motive()
        {
            // calculating=+2 → 25 - 4 = 21; honor=0 has no effect.
            SchemeBacklash.WarnedTargetGratitude(honor: 0, calculating: 2).Should().Be(21);
        }

        [Test]
        public void GIVEN_a_dishonorable_calculating_target_WHEN_computing_gratitude_THEN_the_floor_holds()
        {
            // honor=-2, calculating=+2 → 25 - 6 - 4 = 15, clamped to 16.
            SchemeBacklash.WarnedTargetGratitude(honor: -2, calculating: 2).Should().Be(16);
        }

        [Test]
        public void GIVEN_a_noble_and_generous_target_WHEN_computing_gratitude_THEN_the_ceiling_holds()
        {
            // honor=+2, calculating=-2 → 25 + 6 + 4 = 35, clamped to 34.
            SchemeBacklash.WarnedTargetGratitude(honor: 2, calculating: -2).Should().Be(34);
        }
    }
}
