// Code written by Gabriel Mailhot, 26/09/2026.
// Documents how a trial's verdict is read, and what happens when the council rises without one. The stakes: a verdict
// misread as a conviction jails a player the council meant to free; a trial the player could escape by closing the
// window would make the whole arrest hollow.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
   [TestFixture]
   public sealed class TrialVerdictPolicyTests
   {
      [TestCase("convict")]
      [TestCase("The council finds you guilty of treason.")]
      [TestCase("We condemn the accused to the cells.")]
      public void GIVEN_words_that_convict_WHEN_read_THEN_it_is_a_conviction(string detail)
      {
         TrialVerdictPolicy.IsConviction(detail).Should().BeTrue();
      }

      // "not guilty" contains "guilty": acquittal is read first.
      [TestCase("acquit")]
      [TestCase("The council finds you not guilty.")]
      [TestCase("Innocent. You are released.")]
      public void GIVEN_words_that_acquit_WHEN_read_THEN_it_is_no_conviction(string detail)
      {
         TrialVerdictPolicy.IsConviction(detail).Should().BeFalse();
      }

      // A garbled verdict never jails the player.
      [TestCase("")]
      [TestCase(null)]
      [TestCase("The council will think on it.")]
      public void GIVEN_words_that_say_neither_WHEN_read_THEN_nobody_is_convicted(string detail)
      {
         TrialVerdictPolicy.IsConviction(detail).Should().BeFalse();
      }

      // Rising without a verdict: a hostile bench convicts in the accused's absence...
      [Test]
      public void GIVEN_a_hostile_bench_WHEN_the_council_rose_without_a_verdict_THEN_it_convicts()
      {
         TrialVerdictPolicy.DefaultConviction(new[] {-60, -30, 10, -5}).Should().BeTrue();
      }

      // ...and a bench that does not stand against them lets them go.
      [Test]
      public void GIVEN_a_bench_not_against_the_accused_WHEN_the_council_rose_without_a_verdict_THEN_it_acquits()
      {
         TrialVerdictPolicy.DefaultConviction(new[] {20, -10, 0}).Should().BeFalse();
         TrialVerdictPolicy.DefaultConviction(new int[0]).Should().BeFalse();
      }
   }
}
