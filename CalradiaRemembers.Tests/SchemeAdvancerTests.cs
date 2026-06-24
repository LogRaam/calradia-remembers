// Code written by Gabriel Mailhot, 21/06/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SchemeAdvancerTests
   {
      [Test]
      public void GIVEN_a_bold_and_hateful_plotter_WHEN_reading_momentum_THEN_both_add_to_the_base()
      {
         //Arrange — 8 base + boldness/10 (5) + animosity/10 (5) = 18.
         SchemeAdvancer.Momentum(Factors(0, 50, 50)).Should().Be(18);
      }

      [Test]
      public void GIVEN_a_cunning_plotter_against_a_careless_target_WHEN_reading_discovery_chance_THEN_it_floors_at_zero()
      {
         //Arrange — vigilance/4 (5) - cunning/10 (10) = -5, clamped to 0.
         SchemeAdvancer.DiscoveryChance(Factors(100, 0, 0, 20)).Should().Be(0);
      }

      // ── Lifecycle ──────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_freshly_plotted_scheme_WHEN_advancing_THEN_it_simply_gets_under_way()
      {
         //Arrange
         Scheme s = Scheme(SchemeState.Plotted);

         //Act
         SchemeAdvancer.Advance(s, AnyFactors(), StubRandomSource.AlwaysLow);

         //Assert — no roll, no progress, just set in motion.
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(0);
      }

      [Test]
      public void GIVEN_a_null_scheme_or_null_random_WHEN_advancing_THEN_it_is_a_safe_no_op()
      {
         //Act/Assert — neither throws.
         FluentActions.Invoking(() => SchemeAdvancer.Advance(null, AnyFactors(), StubRandomSource.AlwaysHigh))
                      .Should().NotThrow();
         FluentActions.Invoking(() => SchemeAdvancer.Advance(Scheme(SchemeState.Progressing), AnyFactors(), null))
                      .Should().NotThrow();
      }

      [Test]
      public void GIVEN_a_progressing_scheme_and_a_high_roll_WHEN_advancing_THEN_it_quietly_gains_ground()
      {
         //Arrange — roll 99 clears both the backfire and discovery bands.
         Scheme s = Scheme(SchemeState.Progressing, 30);

         //Act — momentum 18 (boldness 50 + animosity 50).
         SchemeAdvancer.Advance(s, Factors(0, 50, 50, 0), StubRandomSource.AlwaysHigh);

         //Assert
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(48);
      }

      [Test]
      public void GIVEN_a_progressing_scheme_and_a_low_roll_WHEN_advancing_THEN_it_backfires()
      {
         //Arrange — roll 0 always lands in the backfire band (>= 2).
         Scheme s = Scheme(SchemeState.Progressing, 30);

         //Act
         SchemeAdvancer.Advance(s, AnyFactors(), StubRandomSource.AlwaysLow);

         //Assert
         s.State.Should().Be(SchemeState.Failed);
      }

      [Test]
      public void GIVEN_a_scheme_reaching_full_progress_WHEN_advancing_THEN_it_succeeds_and_caps_at_a_hundred()
      {
         //Arrange — 90 + momentum 18 = 108, clamped to 100.
         Scheme s = Scheme(SchemeState.Progressing, 90);

         //Act
         SchemeAdvancer.Advance(s, Factors(0, 50, 50, 0), StubRandomSource.AlwaysHigh);

         //Assert
         s.State.Should().Be(SchemeState.Succeeded);
         s.Progress.Should().Be(100);
      }

      [Test]
      public void GIVEN_a_secret_scheme_and_a_roll_above_the_discovery_band_WHEN_advancing_THEN_it_progresses_instead()
      {
         //Arrange — band [15, 40); roll 50 is above it, so the scheme gains ground.
         Scheme s = Scheme(SchemeState.Progressing, 30);

         //Act — momentum 8 (no boldness/animosity).
         SchemeAdvancer.Advance(s, Factors(0, 0, 0, 100), StubRandomSource.Returning(50));

         //Assert
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(38);
      }

      [Test]
      public void GIVEN_a_secret_scheme_and_a_roll_in_the_discovery_band_WHEN_advancing_THEN_it_is_discovered_without_progress()
      {
         //Arrange — cunning 0 → backfire 15; vigilance 100 → discovery 25; band [15, 40). Roll 20 lands inside.
         Scheme s = Scheme(SchemeState.Progressing, 30);

         //Act
         SchemeAdvancer.Advance(s, Factors(0, 0, 0, 100), StubRandomSource.Returning(20));

         //Assert
         s.State.Should().Be(SchemeState.Discovered);
         s.Progress.Should().Be(30);
      }

      [Test]
      public void GIVEN_a_terminal_scheme_WHEN_advancing_THEN_nothing_changes()
      {
         //Arrange
         Scheme succeeded = Scheme(SchemeState.Succeeded, 100);
         Scheme failed = Scheme(SchemeState.Failed, 40);

         //Act
         SchemeAdvancer.Advance(succeeded, AnyFactors(), StubRandomSource.AlwaysLow);
         SchemeAdvancer.Advance(failed, AnyFactors(), StubRandomSource.AlwaysHigh);

         //Assert
         succeeded.State.Should().Be(SchemeState.Succeeded);
         failed.State.Should().Be(SchemeState.Failed);
      }

      [Test]
      public void GIVEN_a_watchful_target_and_a_dim_plotter_WHEN_reading_discovery_chance_THEN_vigilance_raises_it_cunning_lowers_it()
      {
         //Arrange — vigilance/4 (15) - cunning/10 (4) = 11.
         SchemeAdvancer.DiscoveryChance(Factors(40, 0, 0, 60)).Should().Be(11);
      }

      [Test]
      public void GIVEN_an_already_discovered_scheme_WHEN_advancing_THEN_it_is_not_discovered_again_and_races_on()
      {
         //Arrange — discovered + cunning 0 → backfire 40; roll 50 clears it. No second discovery check.
         Scheme s = Scheme(SchemeState.Discovered, 50);

         //Act
         SchemeAdvancer.Advance(s, Factors(0, 0, 0, 100), StubRandomSource.Returning(50));

         //Assert
         s.State.Should().Be(SchemeState.Discovered);
         s.Progress.Should().Be(58);
      }

      [Test]
      public void GIVEN_an_exposed_plot_WHEN_reading_backfire_chance_THEN_it_is_far_likelier_to_collapse()
      {
         //Arrange — discovered adds a flat 25 to the base.
         SchemeAdvancer.BackfireChance(SchemeState.Discovered, Factors(50, 0, 0)).Should().Be(35);
      }

      [Test]
      public void GIVEN_an_exposed_scheme_and_a_low_roll_WHEN_advancing_THEN_counterplay_collapses_it()
      {
         //Arrange — discovered plots collapse far more readily.
         Scheme s = Scheme(SchemeState.Discovered, 70);

         //Act
         SchemeAdvancer.Advance(s, AnyFactors(), StubRandomSource.AlwaysLow);

         //Assert
         s.State.Should().Be(SchemeState.Failed);
      }

      [Test]
      public void GIVEN_each_lifecycle_state_WHEN_asking_if_terminal_THEN_only_succeeded_and_failed_are()
      {
         SchemeAdvancer.IsTerminal(SchemeState.Plotted).Should().BeFalse();
         SchemeAdvancer.IsTerminal(SchemeState.Progressing).Should().BeFalse();
         SchemeAdvancer.IsTerminal(SchemeState.Discovered).Should().BeFalse();
         SchemeAdvancer.IsTerminal(SchemeState.Succeeded).Should().BeTrue();
         SchemeAdvancer.IsTerminal(SchemeState.Failed).Should().BeTrue();
      }
      // ── Tuning helpers ─────────────────────────────────────────────────────

      [Test]
      public void GIVEN_no_factors_WHEN_reading_momentum_THEN_only_the_base_applies()
      {
         SchemeAdvancer.Momentum(null).Should().Be(8);
      }

      [Test]
      public void GIVEN_null_factors_WHEN_advancing_a_progressing_scheme_THEN_it_still_moves_on_the_base_momentum()
      {
         //Arrange — null factors must not throw; base momentum 8, base backfire 15, no discovery.
         Scheme s = Scheme(SchemeState.Progressing, 10);

         //Act
         SchemeAdvancer.Advance(s, null, StubRandomSource.AlwaysHigh);

         //Assert
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(18);
      }

      [Test]
      public void GIVEN_plotters_of_varying_guile_WHEN_reading_backfire_chance_THEN_guile_lowers_it_within_bounds()
      {
         SchemeAdvancer.BackfireChance(SchemeState.Progressing, Factors(0, 0, 0)).Should().Be(15);
         SchemeAdvancer.BackfireChance(SchemeState.Progressing, Factors(50, 0, 0)).Should().Be(10);
         SchemeAdvancer.BackfireChance(SchemeState.Progressing, Factors(100, 0, 0)).Should().Be(5);
      }

      #region private

      private static SchemeFactors AnyFactors() => Factors(30, 30, 30, 30);

      private static SchemeFactors Factors(int cunning, int boldness, int animosity, int vigilance = 0)
         => new() {
            PlotterCunning = cunning,
            PlotterBoldness = boldness,
            Animosity = animosity,
            TargetVigilance = vigilance
         };

      private static Scheme Scheme(SchemeState state, int progress = 0) => new() {PlotterId = "plotter", TargetId = "target", Kind = SchemeKind.Slander, State = state, Progress = progress};

      #endregion
   }
}