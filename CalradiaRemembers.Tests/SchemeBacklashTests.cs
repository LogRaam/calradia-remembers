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
        public void GIVEN_an_abetted_plot_that_succeeded_WHEN_pricing_THEN_the_player_pays_for_their_shown_hand()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Succeeded).Should().Be(SchemeBacklash.AbettorExposurePenalty);
        }

        [Test]
        public void GIVEN_an_abetted_plot_caught_mid_flight_WHEN_pricing_THEN_the_complicity_surfaces_and_costs()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Discovered).Should().Be(SchemeBacklash.AbettorExposurePenalty);
        }

        [Test]
        public void GIVEN_an_abetted_plot_that_fizzled_unseen_WHEN_pricing_THEN_there_is_no_cost()
        {
            //Arrange — a plot that simply failed without exposure never surfaces the player's role.
            SchemeBacklash.AbettorPenalty(SchemeState.Failed).Should().Be(0);
        }

        [Test]
        public void GIVEN_a_plot_still_in_motion_WHEN_pricing_THEN_nothing_is_owed_yet()
        {
            SchemeBacklash.AbettorPenalty(SchemeState.Plotted).Should().Be(0);
            SchemeBacklash.AbettorPenalty(SchemeState.Progressing).Should().Be(0);
        }
    }
}
