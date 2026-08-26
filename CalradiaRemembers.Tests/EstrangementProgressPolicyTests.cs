// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 2: paces how an affair unravels a married NPC's marriage. The behavior reads
// Phase each day to decide what the NPC voices, and hands off to Increment 3's divorce at ReadyToDivorce; it
// reads ProgressPercent for the fraying note. If the day spans drift, a marriage of years either collapses in a
// week (absurd) or never reaches the payoff and the pillar stalls at "they seem troubled" forever, and the
// difficulty scaling is what makes a hard-won heart break its vows slower than an already-dead marriage.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class EstrangementProgressPolicyTests
   {
      // A bad clock (negative elapsed) must read as not-begun, never as some partial or complete rift, or a save
      // hiccup could teleport a marriage to the brink of divorce.
      [Test]
      public void GIVEN_negative_elapsed_WHEN_reading_the_phase_THEN_it_is_none()
      {
         EstrangementProgressPolicy.Phase(-3, SeductionDifficulty.Easy).Should().Be(EstrangementPhase.None);
      }

      // The opening calm: right after the affair begins the marriage must not already look broken, or every
      // tryst would instantly read as a household in crisis. It only starts to fray once the grace has passed.
      [Test]
      public void GIVEN_within_the_opening_grace_WHEN_reading_the_phase_THEN_it_has_not_frayed_yet()
      {
         EstrangementProgressPolicy.Phase(EstrangementProgressPolicy.FrayingGraceDays - 1, SeductionDifficulty.Hard)
            .Should().Be(EstrangementPhase.None);
         EstrangementProgressPolicy.Phase(EstrangementProgressPolicy.FrayingGraceDays, SeductionDifficulty.Hard)
            .Should().Be(EstrangementPhase.Fraying);
      }

      // The payoff boundary: at exactly DaysToReady the NPC is resolved to leave, so Increment 3 can act. One day
      // short they are still merely fraying; the arc must actually complete, not hover below the line forever.
      [Test]
      public void GIVEN_the_ready_boundary_WHEN_reading_the_phase_THEN_it_flips_to_ready_to_divorce()
      {
         int ready = EstrangementProgressPolicy.DaysToReady(SeductionDifficulty.Hard);

         EstrangementProgressPolicy.Phase(ready - 1, SeductionDifficulty.Hard).Should().Be(EstrangementPhase.Fraying);
         EstrangementProgressPolicy.Phase(ready, SeductionDifficulty.Hard).Should().Be(EstrangementPhase.ReadyToDivorce);
      }

      // The core of the difficulty scaling: a hard-won heart breaks its vows more slowly than an already-dead
      // marriage. At the day an Easy affair is ready, a VeryHard one must still only be fraying, or the whole
      // point of grading the seduction (Inc 1's Difficulty) is lost on the pacing.
      [Test]
      public void GIVEN_a_harder_seduction_WHEN_the_same_days_pass_THEN_it_unravels_slower()
      {
         int easyReady = EstrangementProgressPolicy.DaysToReady(SeductionDifficulty.Easy);

         EstrangementProgressPolicy.Phase(easyReady, SeductionDifficulty.Easy).Should().Be(EstrangementPhase.ReadyToDivorce);
         EstrangementProgressPolicy.Phase(easyReady, SeductionDifficulty.VeryHard).Should().Be(EstrangementPhase.Fraying);
         EstrangementProgressPolicy.DaysToReady(SeductionDifficulty.Easy)
            .Should().BeLessThan(EstrangementProgressPolicy.DaysToReady(SeductionDifficulty.VeryHard));
      }

      // The fraying note reads a 0..100 sense of progress; it must start at nothing, end at full, and never
      // exceed its bounds however stale the elapsed value, so the note can't claim "120% estranged".
      [Test]
      public void GIVEN_elapsed_days_WHEN_reading_the_progress_percent_THEN_it_stays_within_zero_to_one_hundred()
      {
         EstrangementProgressPolicy.ProgressPercent(0, SeductionDifficulty.Hard).Should().Be(0);
         EstrangementProgressPolicy.ProgressPercent(-50, SeductionDifficulty.Hard).Should().Be(0);
         int ready = EstrangementProgressPolicy.DaysToReady(SeductionDifficulty.Hard);
         EstrangementProgressPolicy.ProgressPercent(ready, SeductionDifficulty.Hard).Should().Be(100);
         EstrangementProgressPolicy.ProgressPercent(ready + 999, SeductionDifficulty.Hard).Should().Be(100);
         EstrangementProgressPolicy.ProgressPercent(ready / 2, SeductionDifficulty.Hard).Should().BeInRange(40, 60);
      }
   }
}
