// Code written by Gabriel Mailhot, 05/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisEscapeChancePolicyTests
    {
        [Test]
        public void GIVEN_a_first_captivity_with_no_prior_recapture_WHEN_resolving_the_chance_THEN_it_is_the_base_chance()
        {
            NemesisEscapeChancePolicy.EscapeChance(0).Should().Be(NemesisEscapeChancePolicy.BaseEscapeChance);
        }

        [Test]
        public void GIVEN_one_prior_recapture_WHEN_resolving_the_chance_THEN_it_has_dropped_by_one_step()
        {
            NemesisEscapeChancePolicy.EscapeChance(1)
                .Should().BeApproximately(NemesisEscapeChancePolicy.BaseEscapeChance - NemesisEscapeChancePolicy.DropPerRecapture, 1e-9);
        }

        [Test]
        public void GIVEN_growing_recapture_counts_WHEN_resolving_the_chance_THEN_it_never_increases()
        {
            double previous = 1.0;
            for (int recaptures = 0; recaptures <= 10; recaptures++)
            {
                double chance = NemesisEscapeChancePolicy.EscapeChance(recaptures);
                chance.Should().BeLessThanOrEqualTo(previous);
                previous = chance;
            }
        }

        [Test]
        public void GIVEN_many_recaptures_WHEN_resolving_the_chance_THEN_it_settles_on_the_floor_but_never_zero()
        {
            double chance = NemesisEscapeChancePolicy.EscapeChance(1000);

            chance.Should().Be(NemesisEscapeChancePolicy.MinEscapeChance);
            chance.Should().BeGreaterThan(0.0);
        }

        [Test]
        public void GIVEN_a_negative_recapture_count_WHEN_resolving_the_chance_THEN_it_is_treated_as_a_first_captivity()
        {
            NemesisEscapeChancePolicy.EscapeChance(-5).Should().Be(NemesisEscapeChancePolicy.BaseEscapeChance);
        }
    }
}
