// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the "insults travel" tuning: what becomes a public-disgrace rumour, and how far it
// carries. It breaks in silence — too sensitive and every cross word spreads, too coarse and grave
// affronts evaporate the moment the conversation closes.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PublicAffrontPolicyTests
    {
        [Test]
        public void GIVEN_a_grave_drop_before_witnesses_WHEN_judging_THEN_it_is_a_public_affront()
        {
            PublicAffrontPolicy.IsAffront(worstRelationDelta: -8, witnessCount: 2).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_drop_exactly_at_the_threshold_WHEN_judging_THEN_it_still_counts()
        {
            //Arrange — the threshold is inclusive at -5.
            PublicAffrontPolicy.IsAffront(PublicAffrontPolicy.AffrontThreshold, witnessCount: 1).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_drop_just_short_of_the_threshold_WHEN_judging_THEN_it_does_not_spread()
        {
            //Arrange — a -4 cooling is a slight, not a disgrace.
            PublicAffrontPolicy.IsAffront(-4, witnessCount: 3).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_grave_drop_with_no_one_watching_WHEN_judging_THEN_it_does_not_spread()
        {
            //Arrange — a private affront leaves no rumour.
            PublicAffrontPolicy.IsAffront(-20, witnessCount: 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_positive_or_neutral_delta_WHEN_judging_THEN_it_is_never_an_affront()
        {
            PublicAffrontPolicy.IsAffront(0, witnessCount: 5).Should().BeFalse();
            PublicAffrontPolicy.IsAffront(3, witnessCount: 5).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_small_audience_WHEN_computing_magnitude_THEN_it_grows_with_the_witnesses()
        {
            //Arrange — 25 base + 2 witnesses x 5 = 35.
            PublicAffrontPolicy.Magnitude(witnessCount: 2).Should().Be(35);
        }

        [Test]
        public void GIVEN_a_large_audience_WHEN_computing_magnitude_THEN_it_caps()
        {
            //Arrange — 25 + 10 x 5 = 75, capped at 50.
            PublicAffrontPolicy.Magnitude(witnessCount: 10).Should().Be(50);
        }

        [Test]
        public void GIVEN_the_audience_size_that_lands_exactly_on_the_cap_WHEN_computing_magnitude_THEN_it_is_the_cap()
        {
            //Arrange — 25 + 5 x 5 = 50, exactly the cap.
            PublicAffrontPolicy.Magnitude(witnessCount: 5).Should().Be(50);
        }
    }
}
