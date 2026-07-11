// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, tone arbiter: a strong grudge must outrank stated warmth. These tests pin the three
// bands, their boundaries, the known-vs-hidden wording split at GrudgeDominates, and (11/07/2026) how a
// warm personal regard raises both bars so a genuine bond needs a heavier grudge before it is tempered or
// dominated (fixes the "spouse fixates on the divorce grudge despite +31 regard" bug).

#region

using System.Text.RegularExpressions;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
    [TestFixture]
    public sealed class GrudgeToneArbiterTests
    {
        // ── Bands ────────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_no_grudge_magnitude_WHEN_resolving_the_ruling_THEN_warmth_reads_unshadowed()
        {
            GrudgeToneArbiter.Resolve(0, grudgeKnownToPlayer: true, reputationWithPlayer: 50)
                .Should().Be(ToneRuling.WarmthUnshadowed);
        }

        [Test]
        public void GIVEN_a_magnitude_below_the_temper_threshold_WHEN_resolving_the_ruling_THEN_warmth_reads_unshadowed()
        {
            GrudgeToneArbiter.Resolve(GrudgeToneArbiter.TemperThreshold - 1, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.WarmthUnshadowed);
        }

        [Test]
        public void GIVEN_a_magnitude_exactly_at_the_temper_threshold_WHEN_resolving_the_ruling_THEN_warmth_is_tempered()
        {
            GrudgeToneArbiter.Resolve(GrudgeToneArbiter.TemperThreshold, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.WarmthTempered);
        }

        [Test]
        public void GIVEN_a_magnitude_just_below_the_dominance_threshold_WHEN_resolving_the_ruling_THEN_warmth_is_still_tempered_not_dominated()
        {
            GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold - 1, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.WarmthTempered);
        }

        [Test]
        public void GIVEN_a_magnitude_exactly_at_the_dominance_threshold_WHEN_resolving_the_ruling_THEN_the_grudge_dominates()
        {
            GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.GrudgeDominates);
        }

        [Test]
        public void GIVEN_a_magnitude_well_above_the_dominance_threshold_WHEN_resolving_the_ruling_THEN_the_grudge_dominates()
        {
            GrudgeToneArbiter.Resolve(100, grudgeKnownToPlayer: false, reputationWithPlayer: 80)
                .Should().Be(ToneRuling.GrudgeDominates);
        }

        [TestCase(-50)]
        [TestCase(0)]
        [TestCase(GrudgeToneArbiter.WarmthTemperingFloor)]
        public void GIVEN_reputation_at_or_below_the_warmth_tempering_floor_WHEN_resolving_the_ruling_THEN_only_the_magnitude_band_decides_it(int reputation)
        {
            // At or below the floor, regard does not move the band: behaviour is identical to before this feature.
            GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold, grudgeKnownToPlayer: true, reputationWithPlayer: reputation)
                .Should().Be(ToneRuling.GrudgeDominates);
        }

        // ── Regard tempering: warmth raises both bars ───────────────────────

        [Test]
        public void GIVEN_neutral_regard_WHEN_resolving_the_ruling_THEN_the_dominance_band_matches_todays_behaviour()
        {
            GrudgeToneArbiter.Resolve(40, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.GrudgeDominates);
        }

        [Test]
        public void GIVEN_neutral_regard_WHEN_resolving_the_ruling_THEN_the_temper_band_matches_todays_behaviour()
        {
            GrudgeToneArbiter.Resolve(20, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.WarmthTempered);
        }

        [Test]
        public void GIVEN_neutral_regard_WHEN_resolving_the_ruling_THEN_just_below_the_temper_band_reads_unshadowed()
        {
            GrudgeToneArbiter.Resolve(19, grudgeKnownToPlayer: true, reputationWithPlayer: 0)
                .Should().Be(ToneRuling.WarmthUnshadowed);
        }

        [Test]
        public void GIVEN_the_reported_spouse_regard_of_31_WHEN_resolving_a_magnitude_40_divorce_grudge_THEN_warmth_is_tempered_not_dominated()
        {
            // The reported bug: a +31 spouse with a magnitude-40 divorce grudge used to be told to fixate on it.
            GrudgeToneArbiter.Resolve(40, grudgeKnownToPlayer: true, reputationWithPlayer: 31)
                .Should().Be(ToneRuling.WarmthTempered);
        }

        [Test]
        public void GIVEN_the_reported_spouse_regard_of_31_WHEN_resolving_a_magnitude_31_grudge_THEN_warmth_is_tempered()
        {
            GrudgeToneArbiter.Resolve(31, grudgeKnownToPlayer: true, reputationWithPlayer: 31)
                .Should().Be(ToneRuling.WarmthTempered);
        }

        [Test]
        public void GIVEN_the_reported_spouse_regard_of_31_WHEN_resolving_a_magnitude_30_grudge_THEN_warmth_reads_unshadowed()
        {
            GrudgeToneArbiter.Resolve(30, grudgeKnownToPlayer: true, reputationWithPlayer: 31)
                .Should().Be(ToneRuling.WarmthUnshadowed);
        }

        [Test]
        public void GIVEN_deep_trust_at_regard_50_WHEN_resolving_a_magnitude_40_grudge_THEN_warmth_reads_unshadowed()
        {
            GrudgeToneArbiter.Resolve(40, grudgeKnownToPlayer: true, reputationWithPlayer: 50)
                .Should().Be(ToneRuling.WarmthUnshadowed);
        }

        [Test]
        public void GIVEN_cold_negative_regard_WHEN_resolving_a_magnitude_40_grudge_THEN_the_grudge_still_dominates()
        {
            // RegardTempering clamps at zero: cold or negative regard must never LOWER the bars either.
            GrudgeToneArbiter.Resolve(40, grudgeKnownToPlayer: true, reputationWithPlayer: -30)
                .Should().Be(ToneRuling.GrudgeDominates);
        }

        [TestCase(0, 0)]
        [TestCase(19, 0)]
        [TestCase(20, 0)]
        [TestCase(21, 1)]
        [TestCase(31, 11)]
        [TestCase(-5, 0)]
        public void GIVEN_a_reputation_value_WHEN_computing_regard_tempering_THEN_it_matches_the_expected_point_rise(int reputation, int expectedTempering)
        {
            GrudgeToneArbiter.RegardTempering(reputation).Should().Be(expectedTempering);
        }

        // ── Lines: known vs hidden wording, only at GrudgeDominates ─────────

        [Test]
        public void GIVEN_warmth_unshadowed_WHEN_asking_for_the_line_THEN_there_is_none()
        {
            GrudgeToneArbiter.Line(ToneRuling.WarmthUnshadowed, grudgeKnownToPlayer: true).Should().BeNull();
            GrudgeToneArbiter.Line(ToneRuling.WarmthUnshadowed, grudgeKnownToPlayer: false).Should().BeNull();
        }

        [Test]
        public void GIVEN_warmth_tempered_WHEN_asking_for_the_line_THEN_it_reads_as_a_reserve_showing_through()
        {
            string line = GrudgeToneArbiter.Line(ToneRuling.WarmthTempered, grudgeKnownToPlayer: true);

            line.Should().Contain("shadows your regard");
            line.Should().Contain("reserve");
        }

        [Test]
        public void GIVEN_warmth_tempered_WHEN_knownness_differs_THEN_the_line_is_identical_either_way()
        {
            string known = GrudgeToneArbiter.Line(ToneRuling.WarmthTempered, grudgeKnownToPlayer: true);
            string hidden = GrudgeToneArbiter.Line(ToneRuling.WarmthTempered, grudgeKnownToPlayer: false);

            known.Should().Be(hidden);
        }

        [Test]
        public void GIVEN_the_grudge_dominates_and_is_known_to_the_player_WHEN_asking_for_the_line_THEN_it_reads_as_open_coolness()
        {
            string line = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, grudgeKnownToPlayer: true);

            line.Should().Be("This grievance outweighs whatever warmth you otherwise feel; let it set your tone before any fondness.");
        }

        [Test]
        public void GIVEN_the_grudge_dominates_and_is_hidden_from_the_player_WHEN_asking_for_the_line_THEN_it_reads_as_a_concealment_ruling()
        {
            string line = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, grudgeKnownToPlayer: false);

            line.Should().Be("Whatever warmth you show is a mask; underneath, this grievance rules you. Stay courteous, but never warm.");
        }

        [Test]
        public void GIVEN_the_grudge_dominates_WHEN_knownness_differs_THEN_the_known_and_hidden_lines_differ()
        {
            string known = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, grudgeKnownToPlayer: true);
            string hidden = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, grudgeKnownToPlayer: false);

            known.Should().NotBe(hidden);
        }

        [Test]
        public void GIVEN_every_ruling_and_knownness_combination_WHEN_asking_for_the_line_THEN_no_digits_ever_appear()
        {
            foreach (ToneRuling ruling in new[] {ToneRuling.WarmthUnshadowed, ToneRuling.WarmthTempered, ToneRuling.GrudgeDominates})
            foreach (bool known in new[] {true, false})
            {
                string line = GrudgeToneArbiter.Line(ruling, known);
                if (line == null) continue;

                Regex.IsMatch(line, @"\d").Should().BeFalse($"'{line}' must carry no digits");
            }
        }

        [Test]
        public void GIVEN_every_line_WHEN_checking_style_THEN_no_em_dashes_appear()
        {
            foreach (ToneRuling ruling in new[] {ToneRuling.WarmthUnshadowed, ToneRuling.WarmthTempered, ToneRuling.GrudgeDominates})
            foreach (bool known in new[] {true, false})
            {
                string line = GrudgeToneArbiter.Line(ruling, known);
                if (line == null) continue;

                line.Should().NotContain("—");
            }
        }
    }
}
