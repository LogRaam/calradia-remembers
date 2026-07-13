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
      // One point below TemperThreshold: the lower edge of the tempered band is exclusive, so a grudge
      // just shy of it must leave warmth fully unshadowed, never a false-positive reserve in the prompt.
      [Test]
      public void GIVEN_a_magnitude_below_the_temper_threshold_WHEN_resolving_the_ruling_THEN_warmth_reads_unshadowed()
      {
         GrudgeToneArbiter.Resolve(GrudgeToneArbiter.TemperThreshold - 1, true, 0)
                          .Should().Be(ToneRuling.WarmthUnshadowed);
      }

      // DominanceThreshold is an INCLUSIVE lower bound: landing exactly on it must already outrank
      // stated warmth, not require one point more, or the "grudge dominates" ruling would fire one turn late.
      [Test]
      public void GIVEN_a_magnitude_exactly_at_the_dominance_threshold_WHEN_resolving_the_ruling_THEN_the_grudge_dominates()
      {
         GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold, true, 0)
                          .Should().Be(ToneRuling.GrudgeDominates);
      }

      // Same inclusive-lower-bound guarantee, one band down: exactly at TemperThreshold the reserve must
      // already show, not wait one more point.
      [Test]
      public void GIVEN_a_magnitude_exactly_at_the_temper_threshold_WHEN_resolving_the_ruling_THEN_warmth_is_tempered()
      {
         GrudgeToneArbiter.Resolve(GrudgeToneArbiter.TemperThreshold, true, 0)
                          .Should().Be(ToneRuling.WarmthTempered);
      }

      // The top edge of the tempered band, paired with the dominance-threshold test above: pins the exact
      // line so a grudge one point short of dominance still reads as a mere reserve, not a takeover of tone.
      [Test]
      public void GIVEN_a_magnitude_just_below_the_dominance_threshold_WHEN_resolving_the_ruling_THEN_warmth_is_still_tempered_not_dominated()
      {
         GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold - 1, true, 0)
                          .Should().Be(ToneRuling.WarmthTempered);
      }

      // Regard 80 is about as warm as this relationship ever reads, yet a maximum-severity grudge (100)
      // still dominates: the tempering can raise the bars, but it must never let warmth fully erase the
      // worst grudges, which would break "regard sets rigidity, not immunity".
      [Test]
      public void GIVEN_a_magnitude_well_above_the_dominance_threshold_WHEN_resolving_the_ruling_THEN_the_grudge_dominates()
      {
         GrudgeToneArbiter.Resolve(100, false, 80)
                          .Should().Be(ToneRuling.GrudgeDominates);
      }

      // Pins RegardTempering's own shape, independent of Resolve: flat zero through WarmthTemperingFloor
      // (including negative regard), then a 1:1 rise just past it. Both bars in Resolve depend on this
      // curve being exactly right, so a drift here would silently move every threshold above.
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

      // Tempering is a one-way favour: it may only ever raise the bars for a warm bond, never lower them
      // for a cold one, or a spurned NPC would need an even smaller grudge to stay civil.
      [Test]
      public void GIVEN_cold_negative_regard_WHEN_resolving_a_magnitude_40_grudge_THEN_the_grudge_still_dominates()
      {
         // RegardTempering clamps at zero: cold or negative regard must never LOWER the bars either.
         GrudgeToneArbiter.Resolve(40, true, -30)
                          .Should().Be(ToneRuling.GrudgeDominates);
      }

      // At regard 0 a magnitude-40 grudge dominates outright; here, with deep trust (50), the same grudge
      // reads fully unshadowed. This is the shape of the fix itself: real warmth must be able to clear a
      // grudge that would otherwise rule the whole conversation's tone.
      [Test]
      public void GIVEN_deep_trust_at_regard_50_WHEN_resolving_a_magnitude_40_grudge_THEN_warmth_reads_unshadowed()
      {
         GrudgeToneArbiter.Resolve(40, true, 50)
                          .Should().Be(ToneRuling.WarmthUnshadowed);
      }

      // Line() output is voiced straight into the LLM prompt as a directive to the NPC: an em dash here
      // would violate the project-wide no-em-dash style rule the moment it reaches production text.
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

      // These lines are prompt directives, not player-facing numbers: a stray digit would leak an internal
      // magnitude or threshold into the NPC's voice, breaking immersion and revealing the mechanic underneath.
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

      // Baseline at neutral regard (no tempering in play): confirms the untempered band edges still hold
      // before the regard-shifted cases below are read against them.
      [Test]
      public void GIVEN_neutral_regard_WHEN_resolving_the_ruling_THEN_just_below_the_temper_band_reads_unshadowed()
      {
         GrudgeToneArbiter.Resolve(19, true, 0)
                          .Should().Be(ToneRuling.WarmthUnshadowed);
      }

      // ── Regard tempering: warmth raises both bars ───────────────────────

      // Regression guard for the owner's original "a strong grudge DOMINATES" ruling: at neutral regard,
      // where tempering contributes nothing, the dominance band must behave exactly as it did before this
      // feature existed, so adding regard-awareness never quietly changes the untempered case.
      [Test]
      public void GIVEN_neutral_regard_WHEN_resolving_the_ruling_THEN_the_dominance_band_matches_todays_behaviour()
      {
         GrudgeToneArbiter.Resolve(40, true, 0)
                          .Should().Be(ToneRuling.GrudgeDominates);
      }

      // Same regression guard, one band down: the temper band at neutral regard must also stay identical
      // to pre-feature behaviour.
      [Test]
      public void GIVEN_neutral_regard_WHEN_resolving_the_ruling_THEN_the_temper_band_matches_todays_behaviour()
      {
         GrudgeToneArbiter.Resolve(20, true, 0)
                          .Should().Be(ToneRuling.WarmthTempered);
      }
      // ── Bands ────────────────────────────────────────────────────────────

      // The floor case: zero magnitude must read unshadowed no matter how warm the regard, since there is
      // no grudge for any amount of tempering to act on.
      [Test]
      public void GIVEN_no_grudge_magnitude_WHEN_resolving_the_ruling_THEN_warmth_reads_unshadowed()
      {
         GrudgeToneArbiter.Resolve(0, true, 50)
                          .Should().Be(ToneRuling.WarmthUnshadowed);
      }

      [TestCase(-50)]
      [TestCase(0)]
      [TestCase(GrudgeToneArbiter.WarmthTemperingFloor)]
      // At or below WarmthTemperingFloor, RegardTempering must contribute exactly zero: pins that a
      // neutral or cold relationship is untouched by this feature, so nothing here can retune an existing
      // grudge's dominance for an NPC who was never warm to begin with.
      public void GIVEN_reputation_at_or_below_the_warmth_tempering_floor_WHEN_resolving_the_ruling_THEN_only_the_magnitude_band_decides_it(int reputation)
      {
         // At or below the floor, regard does not move the band: behaviour is identical to before this feature.
         GrudgeToneArbiter.Resolve(GrudgeToneArbiter.DominanceThreshold, true, reputation)
                          .Should().Be(ToneRuling.GrudgeDominates);
      }

      // Pins the exact wording the LLM is instructed to voice for a HIDDEN dominant grudge (the "warm mask
      // over a cold, ruling truth"). A drift here changes what the player actually hears the NPC say.
      [Test]
      public void GIVEN_the_grudge_dominates_and_is_hidden_from_the_player_WHEN_asking_for_the_line_THEN_it_reads_as_a_concealment_ruling()
      {
         string line = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, false);

         line.Should().Be("Whatever warmth you show is a mask; underneath, this grievance rules you. Stay courteous, but never warm.");
      }

      // Same pin for the KNOWN case: open coolness, not a masked one, so a player who already knows of the
      // grievance sees it reflected honestly in the NPC's tone.
      [Test]
      public void GIVEN_the_grudge_dominates_and_is_known_to_the_player_WHEN_asking_for_the_line_THEN_it_reads_as_open_coolness()
      {
         string line = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, true);

         line.Should().Be("This grievance outweighs whatever warmth you otherwise feel; let it set your tone before any fondness.");
      }

      // If these ever collapsed to the same text, the known/hidden mirror the rest of the Grudges pillar
      // relies on would be gone: a concealed grudge and an openly resented one would sound identical.
      [Test]
      public void GIVEN_the_grudge_dominates_WHEN_knownness_differs_THEN_the_known_and_hidden_lines_differ()
      {
         string known = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, true);
         string hidden = GrudgeToneArbiter.Line(ToneRuling.GrudgeDominates, false);

         known.Should().NotBe(hidden);
      }

      // The reported spouse's exact regard (+31), one point below its own regard-raised temper bar (31):
      // at that magnitude the grudge must not even temper the warmth yet.
      [Test]
      public void GIVEN_the_reported_spouse_regard_of_31_WHEN_resolving_a_magnitude_30_grudge_THEN_warmth_reads_unshadowed()
      {
         GrudgeToneArbiter.Resolve(30, true, 31)
                          .Should().Be(ToneRuling.WarmthUnshadowed);
      }

      // One point up from the previous test: at the same +31 regard, magnitude 31 lands exactly on the
      // regard-raised temper bar, pinning where this specific bond's reserve begins to show.
      [Test]
      public void GIVEN_the_reported_spouse_regard_of_31_WHEN_resolving_a_magnitude_31_grudge_THEN_warmth_is_tempered()
      {
         GrudgeToneArbiter.Resolve(31, true, 31)
                          .Should().Be(ToneRuling.WarmthTempered);
      }

      // The regression test for the bug this feature was built to fix: before regard-tempering existed,
      // this exact spouse/grudge combination made the NPC fixate on the grudge despite a warm +31 bond.
      [Test]
      public void GIVEN_the_reported_spouse_regard_of_31_WHEN_resolving_a_magnitude_40_divorce_grudge_THEN_warmth_is_tempered_not_dominated()
      {
         // The reported bug: a +31 spouse with a magnitude-40 divorce grudge used to be told to fixate on it.
         GrudgeToneArbiter.Resolve(40, true, 31)
                          .Should().Be(ToneRuling.WarmthTempered);
      }

      // Pins the wording contract the prompt depends on: the tempered line must read as a shadow over
      // regard, not a verdict, since a full grudge-dominates tone is not warranted at this band.
      [Test]
      public void GIVEN_warmth_tempered_WHEN_asking_for_the_line_THEN_it_reads_as_a_reserve_showing_through()
      {
         string line = GrudgeToneArbiter.Line(ToneRuling.WarmthTempered, true);

         line.Should().Contain("shadows your regard");
         line.Should().Contain("reserve");
      }

      // Unlike GrudgeDominates, the known/hidden wording split only exists at the dominance band (per
      // Line()'s own doc): a tempered reserve is not yet a concealment ruling, so the text must stay the
      // same whether or not the player knows of the grudge.
      [Test]
      public void GIVEN_warmth_tempered_WHEN_knownness_differs_THEN_the_line_is_identical_either_way()
      {
         string known = GrudgeToneArbiter.Line(ToneRuling.WarmthTempered, true);
         string hidden = GrudgeToneArbiter.Line(ToneRuling.WarmthTempered, false);

         known.Should().Be(hidden);
      }

      // ── Lines: known vs hidden wording, only at GrudgeDominates ─────────

      // WarmthUnshadowed must yield no line at all ("nothing needs saying" per Line()'s own doc): the
      // common no-grudge case should never inject a stray directive into the prompt.
      [Test]
      public void GIVEN_warmth_unshadowed_WHEN_asking_for_the_line_THEN_there_is_none()
      {
         GrudgeToneArbiter.Line(ToneRuling.WarmthUnshadowed, true).Should().BeNull();
         GrudgeToneArbiter.Line(ToneRuling.WarmthUnshadowed, false).Should().BeNull();
      }
   }
}