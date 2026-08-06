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
        // Betraying a friend must cost the most of the three tiers, or "help a plotter against someone you
        // like" would be nearly free. Anchors the top tier of the three-way scale.
        [Test]
        public void GIVEN_a_caught_plot_against_a_friend_of_another_realm_WHEN_pricing_THEN_it_is_a_grave_betrayal()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Succeeded, playerRelationWithTarget: 25)
                .Should().Be(SchemeBacklash.AbettorFriendPenalty);
        }

        // The default tier when the relation signal is neither friend nor enemy: anchors the middle of the
        // three-way scale so the friend/enemy tiers above and below can be judged against something.
        [Test]
        public void GIVEN_a_caught_plot_against_a_stranger_WHEN_pricing_THEN_the_cost_is_moderate()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, playerRelationWithTarget: 0)
                .Should().Be(SchemeBacklash.AbettorNeutralPenalty);
        }

        // Moving against someone the player already feuded with is half-expected, so it must cost the least
        // of the three tiers, otherwise there would be no incentive difference between helping a plot against
        // a stranger and helping one against an existing enemy.
        [Test]
        public void GIVEN_a_caught_plot_against_an_existing_enemy_WHEN_pricing_THEN_it_stings_least()
        {
            //Arrange — moving against someone you already feuded with is half-expected.
            SchemeBacklash.AbettorPenalty(SchemeState.Succeeded, playerRelationWithTarget: -20)
                .Should().Be(SchemeBacklash.AbettorEnemyPenalty);
        }

        // Pins the FriendThreshold/EnemyThreshold boundaries INCLUSIVE at exactly +10/-10, matching the
        // thresholds the rest of the mod uses for "a friend"/"an enemy". A future retune of those constants
        // must not silently move which of the three tiers a borderline relation lands in.
        [Test]
        public void GIVEN_relations_exactly_at_the_friend_and_enemy_thresholds_WHEN_pricing_THEN_the_tiers_are_inclusive()
        {
            //Arrange — +10 reads as a friend, -10 as an enemy.
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, 10).Should().Be(SchemeBacklash.AbettorFriendPenalty);
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered, -10).Should().Be(SchemeBacklash.AbettorEnemyPenalty);
        }

        // Per SchemeBacklash's own doc: a plot that Failed without exposure never surfaces the player's hand,
        // so it must cost nothing, whatever the relation. Get this wrong and the player is punished for a
        // scheme nobody ever discovered they touched.
        [Test]
        public void GIVEN_an_abetted_plot_that_fizzled_unseen_WHEN_pricing_THEN_there_is_no_cost_whoever_the_target()
        {
            //Arrange — a plot that simply failed without exposure never surfaces the player's role.
            SchemeBacklash.AbettorPenalty(SchemeState.Failed, playerRelationWithTarget: 50)
                .Should().Be(0);
        }

        // A plot still Plotted/Progressing has not reached an outcome yet, so nothing is owed. Without this
        // guard the player could be billed for merely having helped a scheme that has not (and may never)
        // come to light.
        [Test]
        public void GIVEN_a_plot_still_in_motion_WHEN_pricing_THEN_nothing_is_owed_yet()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Plotted, 0).Should().Be(0);
            SchemeBacklash.AbettorPenalty(SchemeState.Progressing, 0).Should().Be(0);
        }
    }

    [TestFixture]
    public sealed class SchemeBacklash_WarnedPlotterResentmentTests
    {
        // SchemeStore.TargetHeeds applies this magnitude as a straight relation hit against the plotter, on
        // top of a Grudges-pillar plant. Pins the formula's baseline (30, before any trait sways it) so a
        // future retune of the trait coefficients below can be judged against a known starting point.
        [Test]
        public void GIVEN_a_neutral_plotter_WHEN_computing_resentment_THEN_the_base_resentment_applies()
        {
            SchemeBacklash.WarnedPlotterResentment(mercy: 0, calculating: 0).Should().Be(30);
        }

        // A merciful lord must resent LESS, not more, or the Mercy trait would mean the opposite of what it
        // means everywhere else in the mod (RomanticProfileBuilder, jealousy, divorce all read Mercy the same
        // direction). Wrong sign here silently inverts the trait for this one formula only.
        [Test]
        public void GIVEN_a_merciful_plotter_WHEN_computing_resentment_THEN_they_bear_the_betrayal_with_more_grace()
        {
            // mercy=+2 → 30 - 6 = 24; calculating=0 has no effect.
            SchemeBacklash.WarnedPlotterResentment(mercy: 2, calculating: 0).Should().Be(24);
        }

        // The ceiling clamp (per SchemeBacklash's own doc) guarantees a warned-of plotter always becomes a
        // clear enemy, but never spirals past a bound that would let a single warning wreck a relation far
        // beyond what any other single event in the mod can do.
        [Test]
        public void GIVEN_a_vindictive_calculating_plotter_WHEN_computing_resentment_THEN_the_hate_is_at_the_ceiling()
        {
            // mercy=-2, calculating=+2 → 30 + 6 + 6 = 42, clamped to 40.
            SchemeBacklash.WarnedPlotterResentment(mercy: -2, calculating: 2).Should().Be(40);
        }

        // The floor clamp means even the gentlest, least calculating plotter still resents the player at
        // least 20: warning a target is never consequence-free for the player, whatever the plotter's traits.
        [Test]
        public void GIVEN_traits_that_would_underflow_WHEN_clamping_THEN_the_floor_is_twenty()
        {
            // mercy=+2, calculating=-2 → 30 - 6 - 6 = 18, clamped to 20.
            SchemeBacklash.WarnedPlotterResentment(mercy: 2, calculating: -2).Should().Be(20);
        }

        // Factual note: exercises the same mercy/calculating pair as the ceiling test above (both -2/+2);
        // this test does not add a distinct case, only restates the ceiling under its own name.
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
        // SchemeStore.TargetHeeds applies this as the warned target's relation gain toward the player. Pins
        // the formula's baseline (25) so the trait swings below can be judged against a known starting point.
        [Test]
        public void GIVEN_a_neutral_target_WHEN_computing_gratitude_THEN_the_base_gratitude_applies()
        {
            SchemeBacklash.WarnedTargetGratitude(honor: 0, calculating: 0).Should().Be(25);
        }

        // An honorable lord must feel a GREATER debt for being warned, not a lesser one, or Honor would read
        // backwards for this one formula while meaning the opposite everywhere else the mod reads it.
        [Test]
        public void GIVEN_an_honorable_target_WHEN_computing_gratitude_THEN_they_feel_a_genuine_debt()
        {
            // honor=+2 → 25 + 6 = 31; calculating=0 has no effect.
            SchemeBacklash.WarnedTargetGratitude(honor: 2, calculating: 0).Should().Be(31);
        }

        // A calculating lord discounts the warning as self-interest rather than kindness, so gratitude must
        // fall even with honor unchanged: guards the two traits acting as independent, not conflated, signals.
        [Test]
        public void GIVEN_a_calculating_target_WHEN_computing_gratitude_THEN_they_discount_the_motive()
        {
            // calculating=+2 → 25 - 4 = 21; honor=0 has no effect.
            SchemeBacklash.WarnedTargetGratitude(honor: 0, calculating: 2).Should().Be(21);
        }

        // Per SchemeBacklash's own doc, the floor clamp guarantees the target always becomes "at least a
        // cautious ally": even the least grateful honor/calculating combination must not fall through to a
        // token or negative gain, or warning someone could leave them no better disposed than before.
        [Test]
        public void GIVEN_a_dishonorable_calculating_target_WHEN_computing_gratitude_THEN_the_floor_holds()
        {
            // honor=-2, calculating=+2 → 25 - 6 - 4 = 15, clamped to 16.
            SchemeBacklash.WarnedTargetGratitude(honor: -2, calculating: 2).Should().Be(16);
        }

        // The ceiling clamp caps the reward so a single warning cannot, on its own, catapult the target to
        // maximal goodwill regardless of how favorable their traits are.
        [Test]
        public void GIVEN_a_noble_and_generous_target_WHEN_computing_gratitude_THEN_the_ceiling_holds()
        {
            // honor=+2, calculating=-2 → 25 + 6 + 4 = 35, clamped to 34.
            SchemeBacklash.WarnedTargetGratitude(honor: 2, calculating: -2).Should().Be(34);
        }
    }
}
