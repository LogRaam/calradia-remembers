// Code written by Gabriel Mailhot, 21/06/2026.
// SchemeAgentPolicy decides the two player-facing levers on an otherwise fully mechanical, AI-free feud system
// (CHANGELOG: "entirely mechanical, no AI cost"): whether a plotter would loop the player in as a secret agent
// (CanRecruitPlayer) and what that help does (ApplyPlayerAid), and whether/how the player can warn the target
// (CanWarnTarget/ExposeToTarget). SchemeStore.PlayerAssists and TargetHeeds "re-validate the gate" against a
// live scheme before applying standing damage, planting grudges, and firing rumours, so a wrong answer here
// either lets a plotter confide treason in a stranger or an enemy ally, or silently denies the player the
// scheme_assist/scheme_heed prompt teaching they had actually earned.

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
      // Plotted is one of the two eligible states ("still SECRET and active (Plotted or Progressing)"). Without
      // this, the player could never be approached at the earliest, most natural point in a feud, before the
      // first daily tick even sets it in motion.
      [Test]
      public void GIVEN_a_freshly_plotted_scheme_WHEN_checking_THEN_the_player_can_still_be_recruited()
      {
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Plotted, 30, false)
                          .Should().BeTrue();
      }

      // ── ApplyPlayerAid ─────────────────────────────────────────────────────

      // Unlike an ordinary daily tick on a fresh plot (which spends the whole tick JUST getting under way, zero
      // progress, see SchemeAdvancerTests), the player's aid also applies the full AidProgressBoost the SAME
      // tick. This is what makes helping a genuine shortcut, not merely equivalent to letting a day pass.
      [Test]
      public void GIVEN_a_freshly_plotted_scheme_WHEN_the_player_aids_THEN_it_gets_under_way_and_leaps_forward()
      {
         //Arrange
         Scheme s = Scheme(SchemeState.Plotted, 0);

         //Act
         SchemeAgentPolicy.ApplyPlayerAid(s);

         //Assert
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(SchemeAgentPolicy.AidProgressBoost);
      }

      // SchemeStore.PlayerAssists always resolves a real scheme via ResolveRecruitableScheme first, but the
      // policy method is public and must stay defensive on its own: a bad call here must never throw into the
      // chat-action pipeline that invokes it.
      [Test]
      public void GIVEN_a_null_scheme_WHEN_the_player_aids_THEN_it_is_a_safe_no_op()
      {
         FluentActions.Invoking(() => SchemeAgentPolicy.ApplyPlayerAid(null)).Should().NotThrow();
      }

      // Same defensive guarantee for TargetHeeds's parallel path: a bad or already-cleared scheme reference must
      // never throw while the player is mid-conversation warning someone.
      [Test]
      public void GIVEN_a_null_scheme_WHEN_warning_THEN_it_is_a_safe_no_op()
      {
         FluentActions.Invoking(() => SchemeAgentPolicy.ExposeToTarget(null)).Should().NotThrow();
      }

      // Pins MinPlotterRelation=20 as a genuine gate, not decoration: "the plotter must trust the player at least
      // this much to involve them in something illicit". Without this floor, a near-stranger could be recruited
      // into treason/sabotage, cheapening the "trusted confidant" framing and surfacing scheme_assist prompt
      // teaching for a relationship that hasn't earned it.
      [Test]
      public void GIVEN_a_plotter_who_barely_knows_the_player_WHEN_checking_THEN_they_will_not_confide_the_plot()
      {
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, 19, false)
                          .Should().BeFalse();
      }

      // Pins the boost applying cleanly mid-lifecycle (50 + 30 = 80), without also re-triggering the one-time
      // Plotted -> Progressing transition the test above covers.
      [Test]
      public void GIVEN_a_progressing_scheme_WHEN_the_player_aids_THEN_it_advances_by_the_boost()
      {
         //Arrange
         Scheme s = Scheme(SchemeState.Progressing, 50);

         //Act
         SchemeAgentPolicy.ApplyPlayerAid(s);

         //Assert
         s.State.Should().Be(SchemeState.Progressing);
         s.Progress.Should().Be(80);
      }

      // This is the actual "fruition" branch of SchemeStore.PlayerAssists: it fires ApplyStandingDamage, the
      // success rumour, MaybeAbettorBacklash, and CommitmentBehavior.OnSchemeResolved. The 100 cap here must
      // match SchemeAdvancer's own ceiling exactly, or Progress could read past 100 and misreport a "finished"
      // plot to the save and cr.schemes diagnostics.
      [Test]
      public void GIVEN_a_scheme_near_fruition_WHEN_the_player_aids_THEN_it_succeeds_outright_and_caps()
      {
         //Arrange — 80 + 30 = 110, clamped to 100.
         Scheme s = Scheme(SchemeState.Progressing, 80);

         //Act
         SchemeAgentPolicy.ApplyPlayerAid(s);

         //Assert
         s.State.Should().Be(SchemeState.Succeeded);
         s.Progress.Should().Be(100);
      }
      // ── CanRecruitPlayer ───────────────────────────────────────────────────

      // The positive case, pinned against the floor test above and the terminal/allied tests below: together
      // they triangulate CanRecruitPlayer's exact eligibility rule (secret + active, trusted enough, not allied
      // to the target) rather than leaving any one clause unproven.
      [Test]
      public void GIVEN_a_secret_plot_and_a_trusting_plotter_WHEN_checking_THEN_the_player_can_be_recruited()
      {
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, 50, false)
                          .Should().BeTrue();
      }

      // ── Warning the target ─────────────────────────────────────────────────

      // Gates whether the scheme_heed prompt teaching and the warn button/free-text resolve at all
      // (SchemeStore.HasSecretSchemeAgainst calls this same policy method): a still-secret plot must be
      // warnable in both of its active states, or the player would have no way to intervene before it either
      // succeeds or is discovered on its own.
      [Test]
      public void GIVEN_a_secret_plot_WHEN_asking_if_the_target_can_be_warned_THEN_yes()
      {
         SchemeAgentPolicy.CanWarnTarget(SchemeState.Plotted).Should().BeTrue();
         SchemeAgentPolicy.CanWarnTarget(SchemeState.Progressing).Should().BeTrue();
      }

      // "Does not fail it outright... the target now has a fighting chance" (TargetHeeds doc): warning is a
      // state-flip only, never a progress wipe. If it silently killed the plot, the standing-damage/rumour/
      // grudge machinery TargetHeeds fires afterward would be reacting to a scheme that no longer exists; and
      // preserving progress means a far-advanced plot can still succeed even after being warned against, if the
      // elevated Discovered backfire roll happens to miss.
      [Test]
      public void GIVEN_a_secret_plot_WHEN_the_player_warns_the_target_THEN_it_is_dragged_into_the_light()
      {
         //Arrange — exposure does not fail it outright; it becomes Discovered (counterplay follows).
         Scheme plotted = Scheme(SchemeState.Plotted, 0);
         Scheme progressing = Scheme(SchemeState.Progressing, 60);

         //Act
         SchemeAgentPolicy.ExposeToTarget(plotted);
         SchemeAgentPolicy.ExposeToTarget(progressing);

         //Assert — progress is untouched; only the state flips to exposed.
         plotted.State.Should().Be(SchemeState.Discovered);
         progressing.State.Should().Be(SchemeState.Discovered);
         progressing.Progress.Should().Be(60);
      }

      // "Never once exposed or finished": a resolved feud has nothing left to offer the player, and SchemeStore
      // has already removed it from the active list by the time anyone could ask. Guards against surfacing
      // scheme_assist teaching for a plot that no longer exists.
      [Test]
      public void GIVEN_a_terminal_plot_WHEN_checking_THEN_there_is_nothing_to_recruit_into()
      {
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Succeeded, 90, false).Should().BeFalse();
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Failed, 90, false).Should().BeFalse();
      }

      // Mirrors SchemeAdvancer's own terminal no-op (ApplyPlayerAid checks SchemeAdvancer.IsTerminal itself): a
      // finished plot's outcome, and any standing damage or rumour already applied to it, must never be
      // re-triggered by a stray aid call arriving after resolution.
      [Test]
      public void GIVEN_a_terminal_scheme_WHEN_the_player_aids_THEN_nothing_changes()
      {
         //Arrange
         Scheme s = Scheme(SchemeState.Failed, 40);

         //Act
         SchemeAgentPolicy.ApplyPlayerAid(s);

         //Assert
         s.State.Should().Be(SchemeState.Failed);
         s.Progress.Should().Be(40);
      }

      // "A no-op once already exposed or terminal" (ExposeToTarget doc): re-flipping an already-Discovered
      // scheme would spuriously re-fire TargetHeeds's discovery rumour, grudge, and relation deltas a second
      // time for the same event; touching a resolved one would do the same for an event that's already over.
      [Test]
      public void GIVEN_an_already_exposed_or_terminal_plot_WHEN_warning_THEN_nothing_changes()
      {
         Scheme discovered = Scheme(SchemeState.Discovered, 50);
         Scheme succeeded = Scheme(SchemeState.Succeeded, 100);

         SchemeAgentPolicy.ExposeToTarget(discovered);
         SchemeAgentPolicy.ExposeToTarget(succeeded);

         discovered.State.Should().Be(SchemeState.Discovered);
         succeeded.State.Should().Be(SchemeState.Succeeded);
      }

      // "Once discovered, a plotter does not draw new agents into a burning scheme": secrecy is the whole reason
      // a plotter would need a confidant in the first place, so recruitment eligibility must close the moment
      // CanWarnTarget also goes false, keeping both player-facing gates consistent with each other.
      [Test]
      public void GIVEN_an_already_exposed_plot_WHEN_checking_THEN_the_player_is_not_recruited_into_it()
      {
         //Arrange — once discovered, a plotter does not draw new agents into a burning scheme.
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Discovered, 90, false)
                          .Should().BeFalse();
      }

      // The mirror-image boundary of the "dragged into the light" test above: nothing is left to warn of once a
      // scheme is already exposed or resolved. Without this, TargetHeeds could re-expose an already-Discovered
      // plot, generating a duplicate rumour/grudge/relation change for what is really the same discovery event.
      [Test]
      public void GIVEN_an_exposed_or_finished_plot_WHEN_asking_if_the_target_can_be_warned_THEN_no()
      {
         SchemeAgentPolicy.CanWarnTarget(SchemeState.Discovered).Should().BeFalse();
         SchemeAgentPolicy.CanWarnTarget(SchemeState.Succeeded).Should().BeFalse();
         SchemeAgentPolicy.CanWarnTarget(SchemeState.Failed).Should().BeFalse();
      }

      // Pins the floor as INCLUSIVE: MinPlotterRelation's own doc says "must trust the player AT LEAST this much",
      // so a plotter sitting exactly on the threshold should already qualify. Paired with the below-floor test,
      // this fixes the boundary precisely against a future retune.
      [Test]
      public void GIVEN_regard_exactly_at_the_floor_WHEN_checking_THEN_it_passes()
      {
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, SchemeAgentPolicy.MinPlotterRelation, false)
                          .Should().BeTrue();
      }

      // "One does not ask the fox to mind the henhouse": even a plotter's deepest trust (regard 90) can never
      // override this structural conflict of interest. Without it, a plotter could recruit the target's own
      // ally or faction-mate as their agent against the target, which is narratively incoherent and would make
      // the alliance check dead code.
      [Test]
      public void GIVEN_the_player_is_allied_with_the_target_WHEN_checking_THEN_no_recruitment_however_warm_the_plotter()
      {
         SchemeAgentPolicy.CanRecruitPlayer(SchemeState.Progressing, 90, true)
                          .Should().BeFalse();
      }

      #region private

      private static Scheme Scheme(SchemeState state, int progress) => new() {PlotterId = "plotter", TargetId = "target", Kind = SchemeKind.Slander, State = state, Progress = progress};

      #endregion
   }
}