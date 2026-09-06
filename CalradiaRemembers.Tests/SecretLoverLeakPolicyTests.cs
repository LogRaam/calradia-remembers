// Code written by Gabriel Mailhot, 05/09/2026.
// SecretLoverLeakPolicy gates when a secret lover's EXISTENCE may begin to leak as anonymous gossip (mystery-lover
// pillar). The whole point of the design is that the bond stays GENUINELY secret for a while first (Gabriel: a
// delay, not an instant leak): if this let a leak roll fire immediately, a companion's secret would be gossip the
// same day it began, and the original "kept discreet" promise would be broken outright.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SecretLoverLeakPolicyTests
   {
      // Before the delay elapses, the bond is never eligible: a fresh secret is a real secret, not tomorrow's gossip.
      [Test]
      public void GIVEN_a_secret_still_inside_its_delay_WHEN_checking_THEN_it_is_not_eligible_to_leak()
      {
         int since = 100;
         int today = since + SecretLoverLeakPolicy.LeakDelayDays - 1;

         SecretLoverLeakPolicy.IsEligibleToLeak(since, today, alreadyLeaked: false).Should().BeFalse();
      }

      // The day the full delay has passed, it becomes eligible for the leak roll (the engine then rolls the chance).
      [Test]
      public void GIVEN_a_secret_that_has_outlasted_the_delay_WHEN_checking_THEN_it_is_eligible_to_leak()
      {
         int since = 100;
         int today = since + SecretLoverLeakPolicy.LeakDelayDays;

         SecretLoverLeakPolicy.IsEligibleToLeak(since, today, alreadyLeaked: false).Should().BeTrue();
      }

      // Once it has leaked, it never rolls again: the anonymous whisper is born once, not re-emitted daily.
      [Test]
      public void GIVEN_a_secret_that_already_leaked_WHEN_checking_THEN_it_is_not_eligible_again()
      {
         int since = 100;
         int today = since + SecretLoverLeakPolicy.LeakDelayDays + 30;

         SecretLoverLeakPolicy.IsEligibleToLeak(since, today, alreadyLeaked: true).Should().BeFalse();
      }

      // No onset recorded (a bond from before this field existed, or none at all) is never eligible: fail safe,
      // never leak a secret whose age we cannot vouch for.
      [Test]
      public void GIVEN_no_recorded_onset_WHEN_checking_THEN_it_is_not_eligible()
         => SecretLoverLeakPolicy.IsEligibleToLeak(null, 200, alreadyLeaked: false).Should().BeFalse();
   }
}
