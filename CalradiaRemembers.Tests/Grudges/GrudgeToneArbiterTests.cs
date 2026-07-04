// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, tone arbiter: a strong grudge must outrank stated warmth. These tests pin the three
// bands, their boundaries, and the known-vs-hidden wording split at GrudgeDominates.

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
        [TestCase(80)]
        public void GIVEN_any_reputation_value_WHEN_resolving_the_ruling_THEN_only_the_magnitude_band_decides_it(int reputation)
        {
            // The owner ruling speaks only of the grudge's own magnitude; reputation must never move the band.
            GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold, grudgeKnownToPlayer: true, reputationWithPlayer: reputation)
                .Should().Be(ToneRuling.GrudgeDominates);
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
