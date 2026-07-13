// Code written by Gabriel Mailhot, 21/06/2026.
// SchemeAdvancer decides the ONE roll that resolves a feud plot's daily tick: backfire (Failed), discovery
// (secret plot only, -> Discovered), or quiet progress toward Succeeded at 100. It is entirely mechanical, no
// LLM involved (CHANGELOG: "entirely mechanical, no AI cost"), and its state changes are what SchemeStore turns
// into rumours (WorldEventStore) and, on success, real standing damage (clan influence hit + relation hit).
// Wrong odds either make plots consequence-free noise (always quiet progress, no rumour tension) or so fragile
// the feud system the player is meant to hear about, warn against, or aid never gets time to matter. The
// backfire and discovery bands must partition the roll deterministically, or the outcome could not be pinned by
// a test at all.

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
      // Both terms must be live at once: a scorned-but-timid plotter or a bold-but-indifferent one would
      // otherwise progress no faster than a base plotter, flattening the drama SchemeFactors promises
      // ("daring drives how fast the scheme gains ground", "how badly the plotter wants the target harmed...
      // adds momentum"). Pins the exact arithmetic so a future retune of either term can't silently zero it out.
      [Test]
      public void GIVEN_a_bold_and_hateful_plotter_WHEN_reading_momentum_THEN_both_add_to_the_base()
      {
         //Arrange — 8 base + boldness/10 (5) + animosity/10 (5) = 18.
         SchemeAdvancer.Momentum(Factors(0, 50, 50)).Should().Be(18);
      }

      // Without the floor, a highly cunning plotter facing a careless target would score a NEGATIVE discovery
      // chance. Advance() adds discovery straight onto backfire to size the roll band (roll < backfire +
      // DiscoveryChance): a negative term would shrink or skew that band in an undefined way instead of simply
      // meaning "cannot be caught".
      [Test]
      public void GIVEN_a_cunning_plotter_against_a_careless_target_WHEN_reading_discovery_chance_THEN_it_floors_at_zero()
      {
         //Arrange — vigilance/4 (5) - cunning/10 (10) = -5, clamped to 0.
         SchemeAdvancer.DiscoveryChance(Factors(100, 0, 0, 20)).Should().Be(0);
      }

      // ── Lifecycle ──────────────────────────────────────────────────────────

      // A plot on day one has not yet had a tick's worth of effort behind it: it must not be able to backfire
      // or leap to success before the first "quietly moving against" rumour even fires (SchemeStore's own
      // Plotted -> Progressing transition is that first whisper). No roll this tick keeps it purely a scene-set.
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

      // SchemeStore.AdvanceActive calls Advance from inside a plain foreach over every live scheme with no
      // per-scheme try/catch; an unguarded exception here on a bad scheme or a missing random source would abort
      // the whole daily tick for EVERY other feud in flight, not just this one.
      [Test]
      public void GIVEN_a_null_scheme_or_null_random_WHEN_advancing_THEN_it_is_a_safe_no_op()
      {
         //Act/Assert — neither throws.
         FluentActions.Invoking(() => SchemeAdvancer.Advance(null, AnyFactors(), StubRandomSource.AlwaysHigh))
                      .Should().NotThrow();
         FluentActions.Invoking(() => SchemeAdvancer.Advance(Scheme(SchemeState.Progressing), AnyFactors(), null))
                      .Should().NotThrow();
      }

      // The common case, pinned exactly: SchemeStore only logs a rumour on a STATE change, not on every progress
      // tick, so most days a scheme must be able to simply gain ground in total silence. Get the arithmetic
      // wrong and either every day becomes a rumour-worthy event, or progress silently stalls.
      [Test]
      public void GIVEN_a_progressing_scheme_and_a_high_roll_WHEN_advancing_THEN_it_quietly_gains_ground()
      {
         //Arrange — roll 99 clears both the backfire and discovery bands.
         Scheme s = Scheme(SchemeState.Progressing, 30);

         //Act — momentum 18 (boldness 50 + animosity 50).
         SchemeAdvancer.Advance(s, Factors(0, 50, 50), StubRandomSource.AlwaysHigh);

         //Assert
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(48);
      }

      // MinBackfireChance=2 exists so even a maximally cunning plotter (see the guile-lowers-backfire test
      // below) never drops to a genuinely zero risk. This pins that the lowest possible roll always lands in
      // that floor, so no plotter, however skilled, can ever plot risk-free.
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

      // Progress is what SchemeStore reports via cr.schemes and what triggers ApplyStandingDamage + the success
      // rumour + CommitmentBehavior.OnSchemeResolved. It must land at exactly 100, never overshoot, or the save
      // and the diagnostics would misreport how far a "finished" plot actually went.
      [Test]
      public void GIVEN_a_scheme_reaching_full_progress_WHEN_advancing_THEN_it_succeeds_and_caps_at_a_hundred()
      {
         //Arrange — 90 + momentum 18 = 108, clamped to 100.
         Scheme s = Scheme(SchemeState.Progressing, 90);

         //Act
         SchemeAdvancer.Advance(s, Factors(0, 50, 50), StubRandomSource.AlwaysHigh);

         //Assert
         s.State.Should().Be(SchemeState.Succeeded);
         s.Progress.Should().Be(100);
      }

      // Pins the upper edge of the discovery band precisely: paired with the test below (a roll INSIDE the
      // band), this fixes exactly where "caught" ends and "gets away with it" begins, so a future retune of
      // vigilance/cunning math can't silently widen or narrow how often feuds actually get discovered.
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

      // This is the transition SchemeStore.OnStateChanged reacts to with a "discovered" rumour AND, if the
      // player had secretly abetted this plot, MaybeAbettorBacklash: so a plot can expose the player's hidden
      // hand purely by chance, without them ever having warned anyone themselves. The lower band edge (paired
      // with the test above) is what makes that passive discovery possible at all.
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

      // SchemeStore removes a scheme from its list once IsTerminal is true, but Advance is still called
      // uniformly before that check runs. A terminal scheme that kept mutating would risk a double-applied
      // effect (standing damage, rumour) or a spurious progress-changed log on a feud that is already resolved.
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

      // Confirms the two terms combine correctly in the ordinary middling case, not just at the clamp edges
      // covered elsewhere: an unusually watchful lord facing a middling schemer must get a proportionate chance
      // to catch wind, or the "some feuds get discovered, some don't" balance the rumour feed relies on skews.
      [Test]
      public void GIVEN_a_watchful_target_and_a_dim_plotter_WHEN_reading_discovery_chance_THEN_vigilance_raises_it_cunning_lowers_it()
      {
         //Arrange — vigilance/4 (15) - cunning/10 (4) = 11.
         SchemeAdvancer.DiscoveryChance(Factors(40, 0, 0, 60)).Should().Be(11);
      }

      // "Only a still-secret plot can be discovered": once exposed, CHANGELOG's own framing is that it "races
      // against counterplay", meaning progress keeps accumulating under the elevated backfire risk, not that it
      // keeps rolling for a second, redundant discovery. Guards against a plot bouncing Discovered/Progressing
      // states illogically.
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

      // This flat +25 is the actual mechanical payoff of warning a target: SchemeStore.TargetHeeds's own doc
      // says exposure "gives them a real chance" via this tick's elevated backfire. Too small and warning reads
      // as cosmetic; get it wrong and the CHANGELOG's promised "fighting chance" feature is a lie.
      [Test]
      public void GIVEN_an_exposed_plot_WHEN_reading_backfire_chance_THEN_it_is_far_likelier_to_collapse()
      {
         //Arrange — discovered adds a flat 25 to the base.
         SchemeAdvancer.BackfireChance(SchemeState.Discovered, Factors(50, 0, 0)).Should().Be(35);
      }

      // Confirms the elevated backfire chance above is not just a number on paper: it must actually turn into a
      // real Failed outcome, or "warn the target" is a hollow promise (CHANGELOG: "warn the target and give
      // them a fighting chance").
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

      // IsTerminal gates both Advance's early exit and SchemeStore.AdvanceActive's list-removal, and
      // SchemeAgentPolicy leans on it too. Miscategorizing Discovered as terminal would freeze a plot that is
      // supposed to keep racing toward failure or success, leaving a permanently stuck feud in the save;
      // miscategorizing Plotted/Progressing as terminal would brick fresh plots before they ever get a tick.
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

      // A missing-factors call must degrade to the same base momentum as a factorless zero-boldness/zero-
      // animosity plotter, not throw or silently stall progress, matching the mod's "never brick a save" default
      // when engine-side data is unavailable.
      [Test]
      public void GIVEN_no_factors_WHEN_reading_momentum_THEN_only_the_base_applies()
      {
         SchemeAdvancer.Momentum(null).Should().Be(8);
      }

      // Same guarantee at the full Advance() level: a null-factors tick must behave exactly like an all-zero
      // Factors call, so a missing engine-side lookup only slows a scheme's pace to the baseline, never crashes
      // or freezes the daily tick for it.
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

      // Confirms cunning is a real, graduated lever ("lowers both the chance of discovery and of a backfire",
      // SchemeFactors doc) rather than a binary toggle, and that even at maximum cunning (5%, not the raw
      // MinBackfireChance=2 floor, since cunning only reaches 100) a plotter is never truly safe, matching the
      // low-roll backfire test's guarantee that risk never hits zero.
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