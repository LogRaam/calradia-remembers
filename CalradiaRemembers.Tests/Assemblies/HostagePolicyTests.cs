// Code written by Gabriel Mailhot, 04/08/2026.
// Documents give_hostage's ONLY pure policy figure: the fixed 45-day residency term. Kimi's "v1 invite
// d'honneur" (COUNCIL_ACTIONS.md's Partie 8) deliberately fixes the term rather than letting the LLM negotiate a
// second parameter alongside the relative's own name, mirroring TributePolicy.TributeDays'/StipendPolicy.TrancheDays'
// own simplification. The STAKE across the single test here: a future tuning pass cannot silently drift this
// figure out of COUNCIL_ACTIONS.md's own 20-90 day range without a test noticing.
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class HostagePolicyTests
    {
        // COUNCIL_ACTIONS.md's own range is 20-90 days; this build picks the midpoint, 45, as a FIXED term
        // (never LLM-chosen). Pinned here so the value cannot silently drift outside the doc's own range.
        [Test]
        public void GIVEN_the_hostage_term_WHEN_asked_THEN_it_is_a_fixed_forty_five_days()
        {
            HostagePolicy.TermDays.Should().Be(45);
        }

        // The floor and ceiling of COUNCIL_ACTIONS.md's own documented range: the fixed term must sit strictly
        // inside it, never at or outside either edge, or the "fixed" simplification would secretly be picking a
        // boundary value instead of a genuine midpoint.
        [Test]
        public void GIVEN_the_hostage_term_WHEN_asked_THEN_it_sits_within_the_documented_twenty_to_ninety_day_range()
        {
            HostagePolicy.TermDays.Should().BeInRange(20, 90);
        }
    }
}
