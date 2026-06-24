// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the camp-life rapport model — trait compatibility (bond vs clash), the relation bands, the
// "worth raising with the player" threshold, and the companion-marriage warmth gate.

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
         => CompanionRapport.Compatibility(a.v, a.h, a.m, a.g, a.c, b.v, b.h, b.m, b.g, b.c);

      // ── Compatibility ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_two_honourable_companions_WHEN_compared_THEN_they_bond()
      {
         //Arrange/Act/Assert — shared honour leaning is a positive (the heaviest axis).
         Compat((0, 2, 0, 0, 0), (0, 1, 0, 0, 0)).Should().BeGreaterThan(0);
      }

      [Test]
      public void GIVEN_an_honourable_and_a_dishonourable_companion_WHEN_compared_THEN_they_clash()
      {
         //Arrange/Act/Assert — opposed honour is a negative.
         Compat((0, 2, 0, 0, 0), (0, -2, 0, 0, 0)).Should().BeLessThan(0);
      }

      [Test]
      public void GIVEN_a_neutral_axis_WHEN_compared_THEN_it_neither_bonds_nor_grates()
      {
         //Arrange/Act/Assert — one side neutral on every axis → zero.
         Compat((2, 2, 2, 2, 2), (0, 0, 0, 0, 0)).Should().Be(0);
      }

      [Test]
      public void GIVEN_two_fully_aligned_companions_WHEN_compared_THEN_compatibility_sums_every_weight()
      {
         //Arrange/Act/Assert — same sign on all five axes → 3+2+2+1+1 = 9.
         Compat((1, 1, 1, 1, 1), (2, 2, 2, 2, 2)).Should().Be(9);
      }

      [Test]
      public void GIVEN_two_fully_opposed_companions_WHEN_compared_THEN_compatibility_is_the_negated_sum()
      {
         //Arrange/Act/Assert — opposite signs on all five axes → -9.
         Compat((1, 1, 1, 1, 1), (-1, -1, -1, -1, -1)).Should().Be(-9);
      }

      // ── Bands ──────────────────────────────────────────────────────────────

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

      [Test]
      public void GIVEN_a_strong_feeling_either_way_WHEN_asked_THEN_it_is_worth_raising()
      {
         //Arrange/Act/Assert — a warm friendship and a bitter feud both merit a word; a mild standing does not.
         CompanionRapport.WorthRaising(CompanionRapport.RaiseThreshold).Should().BeTrue();
         CompanionRapport.WorthRaising(-CompanionRapport.RaiseThreshold).Should().BeTrue();
         CompanionRapport.WorthRaising(10).Should().BeFalse();
      }

      // ── Marriage gate ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_two_warm_eligible_companions_WHEN_asked_THEN_they_may_wed()
      {
         //Arrange/Act/Assert
         CompanionRapport.CanMarry(CompanionRapport.MarriageThreshold, bothUnmarried: true, oppositeGender: true, bothAdult: true)
            .Should().BeTrue();
      }

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
