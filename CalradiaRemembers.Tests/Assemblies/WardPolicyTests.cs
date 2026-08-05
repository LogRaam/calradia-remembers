// Code written by Gabriel Mailhot, 05/08/2026.
// Documents sponsor_ward's pure tuning: the "young companion" age band and the daily skill-XP trickle. Neither
// figure comes from COUNCIL_ACTIONS.md itself (unlike HostagePolicy.TermDays, which inherits Kimi's own 20-90
// day range) - both are this build's own author's design call (WardPolicy's own header), so the STAKE here is
// narrower than most policy tests: pinning the CHOSEN values so a future tuning pass changes them deliberately,
// with a comment, rather than by silent drift.
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class WardPolicyTests
    {
        // The vanilla adult floor (mirrors MarriageEligibility.MinMarriageAge): a ward must never be a minor,
        // exactly the same floor the codebase already uses elsewhere for an adult-only gate.
        [Test]
        public void GIVEN_the_minimum_ward_age_WHEN_asked_THEN_it_matches_the_vanilla_adult_floor()
        {
            WardPolicy.MinAge.Should().Be(18f);
        }

        // Below the floor, at the floor, and above the ceiling: the three boundary readings that pin exactly
        // where "young" starts and stops, so a future change to either edge is deliberate, not accidental.
        [Test]
        public void GIVEN_an_age_below_the_floor_WHEN_asked_if_within_the_ward_age_band_THEN_it_is_refused()
        {
            WardPolicy.IsWithinAgeBand(WardPolicy.MinAge - 1f).Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_age_at_the_floor_WHEN_asked_if_within_the_ward_age_band_THEN_it_is_accepted()
        {
            WardPolicy.IsWithinAgeBand(WardPolicy.MinAge).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_age_at_the_ceiling_WHEN_asked_if_within_the_ward_age_band_THEN_it_is_accepted()
        {
            WardPolicy.IsWithinAgeBand(WardPolicy.MaxAge).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_age_above_the_ceiling_WHEN_asked_if_within_the_ward_age_band_THEN_it_is_refused()
        {
            WardPolicy.IsWithinAgeBand(WardPolicy.MaxAge + 1f).Should().BeFalse();
        }

        // STAKE: the daily bonus must stay a slow trickle, not a farmable burst - a bond has NO term (CouncilWard's
        // own header), so a generous daily figure compounding over an unbounded run would quietly out-pace
        // training a companion through ordinary play. Pinned well below CaptiveEscapeCoordinator's own ONE-TIME
        // GrantEscapeXp amounts (15 XP for a real feat).
        [Test]
        public void GIVEN_the_daily_mentorship_xp_WHEN_asked_THEN_it_is_a_small_flat_trickle()
        {
            WardPolicy.DailyXpPerSkill.Should().Be(2f);
            WardPolicy.DailyXpPerSkill.Should().BeLessThan(15f);
        }
    }
}
