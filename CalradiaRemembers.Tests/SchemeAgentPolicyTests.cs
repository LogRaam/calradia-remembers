// Code written by Gabriel Mailhot, 21/06/2026.
// Protects the player's hand in a scheme: when a plotter may recruit them, and how far their help
// carries the plot. The gate and the boost break in silence — too loose and the player is dragged
// into every feud, too generous and one favour wins any plot outright.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SchemeAgentPolicyTests
    {
        // ── CanRecruitPlayer ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_secret_plot_and_a_trusting_plotter_WHEN_checking_THEN_the_player_can_be_recruited()
        {
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, plotterRegardForPlayer: 50, playerAlliedWithTarget: false)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_freshly_plotted_scheme_WHEN_checking_THEN_the_player_can_still_be_recruited()
        {
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Plotted, plotterRegardForPlayer: 30, playerAlliedWithTarget: false)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_regard_exactly_at_the_floor_WHEN_checking_THEN_it_passes()
        {
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, SchemeAgentPolicy.MinPlotterRegard, false)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_plotter_who_barely_knows_the_player_WHEN_checking_THEN_they_will_not_confide_the_plot()
        {
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, plotterRegardForPlayer: 19, playerAlliedWithTarget: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_already_exposed_plot_WHEN_checking_THEN_the_player_is_not_recruited_into_it()
        {
            //Arrange — once discovered, a plotter does not draw new agents into a burning scheme.
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Discovered, plotterRegardForPlayer: 90, playerAlliedWithTarget: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_terminal_plot_WHEN_checking_THEN_there_is_nothing_to_recruit_into()
        {
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Succeeded, 90, false).Should().BeFalse();
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Failed, 90, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_player_is_allied_with_the_target_WHEN_checking_THEN_no_recruitment_however_warm_the_plotter()
        {
            SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, plotterRegardForPlayer: 90, playerAlliedWithTarget: true)
                .Should().BeFalse();
        }

        // ── ApplyPlayerAid ─────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_freshly_plotted_scheme_WHEN_the_player_aids_THEN_it_gets_under_way_and_leaps_forward()
        {
            //Arrange
            Scheme s = Scheme(SchemeState.Plotted, progress: 0);

            //Act
            SchemeAgentPolicy.ApplyPlayerAid(s);

            //Assert
            s.State.Should().Be(SchemeState.Progressing);
            s.Progress.Should().Be(SchemeAgentPolicy.AidProgressBoost);
        }

        [Test]
        public void GIVEN_a_progressing_scheme_WHEN_the_player_aids_THEN_it_advances_by_the_boost()
        {
            //Arrange
            Scheme s = Scheme(SchemeState.Progressing, progress: 50);

            //Act
            SchemeAgentPolicy.ApplyPlayerAid(s);

            //Assert
            s.State.Should().Be(SchemeState.Progressing);
            s.Progress.Should().Be(80);
        }

        [Test]
        public void GIVEN_a_scheme_near_fruition_WHEN_the_player_aids_THEN_it_succeeds_outright_and_caps()
        {
            //Arrange — 80 + 30 = 110, clamped to 100.
            Scheme s = Scheme(SchemeState.Progressing, progress: 80);

            //Act
            SchemeAgentPolicy.ApplyPlayerAid(s);

            //Assert
            s.State.Should().Be(SchemeState.Succeeded);
            s.Progress.Should().Be(100);
        }

        [Test]
        public void GIVEN_a_terminal_scheme_WHEN_the_player_aids_THEN_nothing_changes()
        {
            //Arrange
            Scheme s = Scheme(SchemeState.Failed, progress: 40);

            //Act
            SchemeAgentPolicy.ApplyPlayerAid(s);

            //Assert
            s.State.Should().Be(SchemeState.Failed);
            s.Progress.Should().Be(40);
        }

        [Test]
        public void GIVEN_a_null_scheme_WHEN_the_player_aids_THEN_it_is_a_safe_no_op()
        {
            FluentActions.Invoking(() => SchemeAgentPolicy.ApplyPlayerAid(null)).Should().NotThrow();
        }

        // ── Warning the target ─────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_secret_plot_WHEN_asking_if_the_target_can_be_warned_THEN_yes()
        {
            SchemeAgentPolicy.CanWarnTarget(SchemeState.Plotted).Should().BeTrue();
            SchemeAgentPolicy.CanWarnTarget(SchemeState.Progressing).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_exposed_or_finished_plot_WHEN_asking_if_the_target_can_be_warned_THEN_no()
        {
            SchemeAgentPolicy.CanWarnTarget(SchemeState.Discovered).Should().BeFalse();
            SchemeAgentPolicy.CanWarnTarget(SchemeState.Succeeded).Should().BeFalse();
            SchemeAgentPolicy.CanWarnTarget(SchemeState.Failed).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_secret_plot_WHEN_the_player_warns_the_target_THEN_it_is_dragged_into_the_light()
        {
            //Arrange — exposure does not fail it outright; it becomes Discovered (counterplay follows).
            Scheme plotted = Scheme(SchemeState.Plotted, progress: 0);
            Scheme progressing = Scheme(SchemeState.Progressing, progress: 60);

            //Act
            SchemeAgentPolicy.ExposeToTarget(plotted);
            SchemeAgentPolicy.ExposeToTarget(progressing);

            //Assert — progress is untouched; only the state flips to exposed.
            plotted.State.Should().Be(SchemeState.Discovered);
            progressing.State.Should().Be(SchemeState.Discovered);
            progressing.Progress.Should().Be(60);
        }

        [Test]
        public void GIVEN_an_already_exposed_or_terminal_plot_WHEN_warning_THEN_nothing_changes()
        {
            Scheme discovered = Scheme(SchemeState.Discovered, progress: 50);
            Scheme succeeded = Scheme(SchemeState.Succeeded, progress: 100);

            SchemeAgentPolicy.ExposeToTarget(discovered);
            SchemeAgentPolicy.ExposeToTarget(succeeded);

            discovered.State.Should().Be(SchemeState.Discovered);
            succeeded.State.Should().Be(SchemeState.Succeeded);
        }

        [Test]
        public void GIVEN_a_null_scheme_WHEN_warning_THEN_it_is_a_safe_no_op()
        {
            FluentActions.Invoking(() => SchemeAgentPolicy.ExposeToTarget(null)).Should().NotThrow();
        }

        #region private

        private static Scheme Scheme(SchemeState state, int progress)
            => new Scheme { PlotterId = "plotter", TargetId = "target", Kind = SchemeKind.Slander, State = state, Progress = progress };

        #endregion
    }
}
