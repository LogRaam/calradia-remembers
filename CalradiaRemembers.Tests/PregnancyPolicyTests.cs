// Code written by Gabriel Mailhot, 05/08/2026.
// Protects the fix for a player report: a companion romance conceived while the campaign's Life/Death
// Cycle option was disabled (no aging, no birth), and the pregnancy never delivered - vanilla's own
// PregnancyCampaignBehavior.DailyTickHero gates ITS ENTIRE offspring-delivery check behind the same
// `IsLifeDeathCycleDisabled` flag (confirmed by decompiling the engine), so she stayed "pregnant" for
// game-years and the NPC would not stop mentioning it in chat. MayConceive stops a NEW pregnancy from ever
// being created in that state; ShouldEndStuckPregnancy decides when an EXISTING tracked pregnancy has gone
// stuck long enough that PregnancyBirthBehavior's daily cleanup should end it childless. Get either wrong
// toward "false" and a pregnancy lingers forever again; get ShouldEndStuckPregnancy wrong toward "true"
// and a pregnancy in a NORMAL campaign (births enabled) could be cut short before vanilla ever gets a
// chance to deliver it.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PregnancyPolicyTests
    {
        // ── MayConceive: the conception guard ───────────────────────────────────

        // STAKE: this is the exact bug. Without this refusal, a conception rolled while births are
        // disabled creates a pregnancy vanilla will never deliver, so it lingers forever and the NPC never
        // stops mentioning it - precisely the player report this fix closes.
        [Test]
        public void GIVEN_births_are_disabled_WHEN_deciding_whether_to_conceive_THEN_conception_is_refused()
        {
            PregnancyPolicy.MayConceive(birthsDisabled: true).Should().BeFalse();
        }

        // STAKE: the ordinary case (births enabled, the overwhelming majority of campaigns) must be
        // completely unaffected by this guard - vanilla delivers normally, so conception proceeds as before.
        [Test]
        public void GIVEN_births_are_enabled_WHEN_deciding_whether_to_conceive_THEN_conception_is_permitted()
        {
            PregnancyPolicy.MayConceive(birthsDisabled: false).Should().BeTrue();
        }

        // ── ShouldEndStuckPregnancy: the daily cleanup guard ─────────────────────

        // STAKE: the normal case must do nothing. When births are enabled, vanilla's own daily tick
        // delivers on schedule; CR's cleanup must never fire here, or it would prematurely end a pregnancy
        // vanilla was about to deliver on its own.
        [Test]
        public void GIVEN_births_are_enabled_WHEN_a_pregnancy_is_far_past_its_due_date_THEN_cleanup_never_fires()
        {
            PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled: false, daysOverdue: 999, overdueMarginDays: 5)
                .Should().BeFalse();
        }

        // STAKE: not yet due (negative "overdue") must never be treated as stuck, even with births
        // disabled - a pregnancy en route to its due date is not a bug, just an ordinary in-progress one.
        [Test]
        public void GIVEN_births_are_disabled_WHEN_the_pregnancy_is_not_yet_due_THEN_cleanup_does_not_fire()
        {
            PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled: true, daysOverdue: -10, overdueMarginDays: 5)
                .Should().BeFalse();
        }

        // STAKE: the margin exists to give the engine's own daily check (and ordinary day-boundary
        // jitter around the due date) a fair window before CR intervenes; exactly AT the margin must still
        // be tolerated, not yet cleaned up.
        [Test]
        public void GIVEN_births_are_disabled_WHEN_overdue_exactly_at_the_margin_THEN_cleanup_does_not_fire_yet()
        {
            PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled: true, daysOverdue: 5, overdueMarginDays: 5)
                .Should().BeFalse();
        }

        // STAKE: one day past the margin is the exact boundary where the mod must finally intervene -
        // pinning "> margin" (strictly greater), not ">=", so the margin itself is never contentious.
        [Test]
        public void GIVEN_births_are_disabled_WHEN_overdue_one_day_past_the_margin_THEN_cleanup_fires()
        {
            PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled: true, daysOverdue: 6, overdueMarginDays: 5)
                .Should().BeTrue();
        }

        // STAKE: the reported bug left a pregnancy stuck for over TWO GAME-YEARS - a large overdue value,
        // clear of the boundary, must unambiguously trigger cleanup.
        [Test]
        public void GIVEN_births_are_disabled_WHEN_the_pregnancy_has_been_stuck_for_game_years_THEN_cleanup_fires()
        {
            PregnancyPolicy.ShouldEndStuckPregnancy(birthsDisabled: true, daysOverdue: 730, overdueMarginDays: 5)
                .Should().BeTrue();
        }
    }
}
