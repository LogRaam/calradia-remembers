// Code written by Gabriel Mailhot, 03/09/2026.
//
// WHY IT MATTERS: this counter decides, for every frame, whether the mod skips the vanilla settlement-visual tick
// (SettlementVisualManager.OnTick) while a CR panel owns the screen. That tick's siege-bombardment branch calls
// GameEntity.SetGlobalFrame on projectiles in flight and access-violates natively under a pushed panel (four
// confirmed crashes, 0xC0000005). Too little suppression and the crash returns; suppression stuck on and the
// settlement/siege visuals freeze forever after the panel closes. The two subtle failure modes both live here and
// are why this is pure and tested rather than only watched in game: the count MUST stay above zero across a panel
// reload's close-then-reopen gap (the repeated-cr.reload_ui crash is exactly a tick slipping through that gap), and
// it MUST never underflow (teardown reaches the count by two paths, an explicit close and the engine's finalize).
// Consumed by CrPanelHost (Enter on open, Exit on any pop, an extra Enter held across a reload) and read by the
// SettlementVisualTickGuard Harmony prefix.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PanelTickSuppressionCounterTests
    {
        // The floor: a fresh counter suppresses nothing, so at rest (no panel open) the vanilla siege visuals tick
        // normally. A counter that started "suppressed" would freeze every settlement visual from boot.
        [Test]
        public void GIVEN_a_fresh_counter_WHEN_nothing_has_opened_THEN_it_does_not_suppress()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();

            counter.IsSuppressed.Should().BeFalse();
            counter.Depth.Should().Be(0);
        }

        // The whole point of the guard: while a panel owns the screen the crashing siege-visual tick must be skipped.
        [Test]
        public void GIVEN_a_panel_opened_WHEN_it_holds_the_screen_THEN_the_tick_is_suppressed()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();

            counter.Enter(); // CrPanelHost.Open

            counter.IsSuppressed.Should().BeTrue();
        }

        // A balanced open/close returns to normal ticking: the siege visuals must resume the instant the last panel
        // is gone, or the player is left with a permanently frozen map.
        [Test]
        public void GIVEN_a_panel_opened_and_closed_WHEN_balanced_THEN_suppression_clears()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();

            counter.Enter(); // Open
            counter.Exit();  // ResetState on the pop

            counter.IsSuppressed.Should().BeFalse();
            counter.Depth.Should().Be(0);
        }

        // THE reload crash guard. A reload is Close-then-reopen; without the extra hold the count would touch zero in
        // the gap and a siege tick would fire mid-reload (the repeated-cr.reload_ui crash in the field). This models
        // CrPanelHost.Reload's exact call order and asserts the count NEVER reaches zero until the final real close.
        [Test]
        public void GIVEN_an_open_panel_reloaded_WHEN_it_closes_then_reopens_THEN_suppression_never_drops_in_the_gap()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();
            counter.Enter(); // the panel is already open before the reload

            counter.Enter(); // Reload: hold across the gap
            counter.IsSuppressed.Should().BeTrue();

            counter.Exit(); // Reload -> Close -> ResetState
            counter.IsSuppressed.Should().BeTrue("the held Enter must span the close->reopen gap");

            counter.Enter(); // Reload -> Open
            counter.IsSuppressed.Should().BeTrue();

            counter.Exit(); // Reload finally: release the hold
            counter.IsSuppressed.Should().BeTrue("the panel is still open after the reload");
            counter.Depth.Should().Be(1);

            counter.Exit(); // later, the real close
            counter.IsSuppressed.Should().BeFalse();
        }

        // Teardown reaches the count by two paths (CrPanelHost.Close's explicit ResetState AND the screen's
        // OnFinalize->OnClosed->ResetState). The first Exit is guarded by a non-null screen so only one counts, but
        // the counter itself must also refuse to go negative, or a stray double Exit would wedge suppression OFF and
        // the crash would return.
        [Test]
        public void GIVEN_more_exits_than_enters_WHEN_it_would_underflow_THEN_it_stays_at_zero()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();

            counter.Enter();
            counter.Exit();
            counter.Exit(); // the defensive second teardown path

            counter.Depth.Should().Be(0);
            counter.IsSuppressed.Should().BeFalse();
        }

        // Two panels at once (a future World Almanac panel over another): the tick stays suppressed until BOTH have
        // released the screen, never after only the first closes.
        [Test]
        public void GIVEN_two_panels_open_WHEN_only_one_closes_THEN_suppression_holds_until_both_close()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();

            counter.Enter();
            counter.Enter();

            counter.Exit();
            counter.IsSuppressed.Should().BeTrue("one panel still owns the screen");

            counter.Exit();
            counter.IsSuppressed.Should().BeFalse();
        }

        // Session launch: no panel can be open, so any residue from a screen the previous session tore down without
        // balancing (a save-load, a campaign end) must be cleared, or the siege visuals stay frozen into the new game.
        [Test]
        public void GIVEN_residual_suppression_WHEN_reset_for_a_new_session_THEN_it_clears()
        {
            PanelTickSuppressionCounter counter = new PanelTickSuppressionCounter();
            counter.Enter();
            counter.Enter();

            counter.Reset();

            counter.Depth.Should().Be(0);
            counter.IsSuppressed.Should().BeFalse();
        }
    }
}
