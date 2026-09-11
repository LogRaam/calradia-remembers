// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the camp-life rapport model — trait compatibility (bond vs clash), the relation bands, the
// "worth raising with the player" threshold, and the companion-marriage warmth gate.
//
// WHY IT MATTERS: CompanionRapportBehavior's weekly tick reads Compatibility to nudge every companion
// pair's native relation up or down, and CanMarry to decide who is warm enough to actually wed (native
// MarriageAction.Apply, permanent). EncounterContextBuilder reads WorthRaising to decide whether the
// player's own companion has anything worth voicing about camp relationships this conversation. A wrong
// Compatibility sign silently pushes two companions apart when the traits say they should bond (or vice
// versa); a wrong CanMarry gate either marries off companions who shouldn't be eligible or blocks a pair
// who should be.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionRapportTests
   {
      // Helper: compatibility from two (valor, honor, mercy, generosity, calculating) tuples.
      private static int Compat(
         (int v, int h, int m, int g, int c) a, (int v, int h, int m, int g, int c) b)
         => CompanionRapport.Compatibility(new RomanticTraits {Valor = a.v, Honor = a.h, Mercy = a.m, Generosity = a.g, Calculating = a.c}, new RomanticTraits {Valor = b.v, Honor = b.h, Mercy = b.m, Generosity = b.g, Calculating = b.c});

      // ── Compatibility ──────────────────────────────────────────────────────

      // Honour carries the heaviest weight (3, vs 1-2 elsewhere) because CompanionRapportBehavior nudges
      // the pair's native relation one point per weekly tick in the sign of this result: a wrong sign here
      // would push two honourable companions apart instead of together, week after week, unnoticed until
      // someone checks cr.rapport or the pair unexpectedly feuds.
      [Test]
      public void GIVEN_two_honourable_companions_WHEN_compared_THEN_they_bond()
      {
         //Arrange/Act/Assert — shared honour leaning is a positive (the heaviest axis).
         Compat((0, 2, 0, 0, 0), (0, 1, 0, 0, 0)).Should().BeGreaterThan(0);
      }

      // The mirror image of the bond test: opposed honour must drive the weekly nudge NEGATIVE, or two
      // companions the traits say should grate would instead be quietly pushed toward marriage eligibility
      // (CanMarry's MarriageThreshold) by a bug that only ever added.
      [Test]
      public void GIVEN_an_honourable_and_a_dishonourable_companion_WHEN_compared_THEN_they_clash()
      {
         //Arrange/Act/Assert — opposed honour is a negative.
         Compat((0, 2, 0, 0, 0), (0, -2, 0, 0, 0)).Should().BeLessThan(0);
      }

      // Axis() special-cases a==0||b==0 as neutral rather than comparing Math.Sign(0) against the other
      // side. Without that guard a companion with no leaning on an axis would still be scored as
      // "opposed" or "aligned" by sign comparison, corrupting compatibility for the many companions who
      // sit neutral on one or more traits.
      [Test]
      public void GIVEN_a_neutral_axis_WHEN_compared_THEN_it_neither_bonds_nor_grates()
      {
         //Arrange/Act/Assert — one side neutral on every axis → zero.
         Compat((2, 2, 2, 2, 2), (0, 0, 0, 0, 0)).Should().Be(0);
      }

      // Pins that all FIVE axes are actually summed (3+2+2+1+1=9), not just the sign of one dominant
      // trait. NudgeRelation only reads Math.Sign(compat) today, so a dropped axis wouldn't show up as a
      // wrong nudge direction here; this test is what actually catches a silently omitted axis.
      [Test]
      public void GIVEN_two_fully_aligned_companions_WHEN_compared_THEN_compatibility_sums_every_weight()
      {
         //Arrange/Act/Assert — same sign on all five axes → 3+2+2+1+1 = 9.
         Compat((1, 1, 1, 1, 1), (2, 2, 2, 2, 2)).Should().Be(9);
      }

      // Symmetry guard on the documented range ("about -9..+9"): opposed traits must negate the exact same
      // per-axis weights the aligned case sums, not a different scale. Note NudgeRelation currently only
      // reads Math.Sign(compat), so a magnitude bug here would slip past that one live consumer; this test
      // is what actually pins the negation.
      [Test]
      public void GIVEN_two_fully_opposed_companions_WHEN_compared_THEN_compatibility_is_the_negated_sum()
      {
         //Arrange/Act/Assert — opposite signs on all five axes → -9.
         Compat((1, 1, 1, 1, 1), (-1, -1, -1, -1, -1)).Should().Be(-9);
      }

      // ── Bands ──────────────────────────────────────────────────────────────

      // Band's only live caller is the cr.rapport console command (ModCheats.Companions), which is how
      // Gabriel actually inspects a companion's felt relationships in-game; a wrong boundary here makes
      // that diagnostic lie about whether a pair reads as merely Cordial or already Close/Feud.
      [Test]
      public void GIVEN_relations_at_each_threshold_WHEN_banded_THEN_they_read_as_expected()
      {
         //Arrange/Act/Assert
         CompanionRapport.Band(30).Should().Be(RapportBand.Close);
         CompanionRapport.Band(29).Should().Be(RapportBand.Cordial);
         CompanionRapport.Band(10).Should().Be(RapportBand.Cordial);
         CompanionRapport.Band(9).Should().Be(RapportBand.Neutral);
         CompanionRapport.Band(-9).Should().Be(RapportBand.Neutral);
         CompanionRapport.Band(-10).Should().Be(RapportBand.Tense);
         CompanionRapport.Band(-29).Should().Be(RapportBand.Tense);
         CompanionRapport.Band(-30).Should().Be(RapportBand.Feud);
      }

      // ── Worth raising ──────────────────────────────────────────────────────

      // EncounterContextBuilder.ResolveCompanionCampNote reads this to decide whether a companion, in
      // conversation with the player, has anything worth voicing about a fellow companion at all. A
      // threshold set too low would have companions constantly commenting on mild camp acquaintances; too
      // high would mute even a real feud or friendship the player should hear about.
      [Test]
      public void GIVEN_a_strong_feeling_either_way_WHEN_asked_THEN_it_is_worth_raising()
      {
         //Arrange/Act/Assert — a warm friendship and a bitter feud both merit a word; a mild standing does not.
         CompanionRapport.WorthRaising(CompanionRapport.RaiseThreshold).Should().BeTrue();
         CompanionRapport.WorthRaising(-CompanionRapport.RaiseThreshold).Should().BeTrue();
         CompanionRapport.WorthRaising(10).Should().BeFalse();
      }

      // ── Marriage gate ──────────────────────────────────────────────────────

      // CanMarry is what gates CompanionRapportBehavior.TryMarry's call into vanilla MarriageAction.Apply,
      // a PERMANENT clan change. This is the base case that must pass when every condition is genuinely
      // met, so the pure gate isn't accidentally stricter than the design intends.
      [Test]
      public void GIVEN_two_warm_eligible_companions_WHEN_asked_THEN_they_may_wed()
      {
         //Arrange/Act/Assert
         CompanionRapport.CanMarry(CompanionRapport.MarriageThreshold, bothUnmarried: true, oppositeGender: true, bothAdult: true)
            .Should().BeTrue();
      }

      // Each precondition must independently veto the marriage: TryMarry already filters out a married
      // pair before calling CanMarry, but the pure gate re-checks bothUnmarried anyway, so this defends any
      // future caller that doesn't pre-filter as carefully. The last case pins the warmth gate itself: one
      // point under MarriageThreshold must still refuse, or the "40" boundary would silently drift.
      [Test]
      public void GIVEN_any_failed_precondition_WHEN_asked_THEN_they_may_not_wed()
      {
         //Arrange/Act/Assert
         CompanionRapport.CanMarry(50, bothUnmarried: false, oppositeGender: true,  bothAdult: true).Should().BeFalse();
         CompanionRapport.CanMarry(50, bothUnmarried: true,  oppositeGender: false, bothAdult: true).Should().BeFalse();
         CompanionRapport.CanMarry(50, bothUnmarried: true,  oppositeGender: true,  bothAdult: false).Should().BeFalse();
         CompanionRapport.CanMarry(39, bothUnmarried: true,  oppositeGender: true,  bothAdult: true).Should().BeFalse("below the warmth gate");
      }
   }
}
