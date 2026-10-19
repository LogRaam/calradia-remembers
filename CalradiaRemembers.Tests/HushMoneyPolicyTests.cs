// Code written by Gabriel Mailhot, 05/09/2026.
// HushMoneyPolicy is the numeric contract for blackmail paid to a lord who holds the player's secret-lover identity
// (mystery-lover phase 3b-3). Two failures matter. If the clamp were dropped, a hallucinated or coerced demand could
// bankrupt the player (or a lowballed one let them buy silence for a coin). If the cooldown misjudged recency, a
// paid-off player would be shaken down again the same visit (cooldown too short) or the lord would hold his peace
// forever after one payment (too long). These pin the band and the cooldown boundary.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Bastards;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HushMoneyPolicyTests
   {
      // A negotiated amount below the floor is raised to it: a lord never settles for a pittance the model lowballed.
      [Test]
      public void GIVEN_a_lowball_amount_WHEN_clamped_THEN_it_is_raised_to_the_floor()
         => HushMoneyPolicy.ClampPayment(1).Should().Be(HushMoneyPolicy.MinPayment);

      // An extravagant demand is capped: a hallucinated figure cannot bankrupt the player in one stroke.
      [Test]
      public void GIVEN_an_extravagant_amount_WHEN_clamped_THEN_it_is_capped_at_the_ceiling()
         => HushMoneyPolicy.ClampPayment(HushMoneyPolicy.MaxPayment + 500_000).Should().Be(HushMoneyPolicy.MaxPayment);

      // An amount already inside the band passes through untouched: the negotiation still matters within reason.
      [Test]
      public void GIVEN_a_reasonable_amount_WHEN_clamped_THEN_it_passes_through()
      {
         int reasonable = (HushMoneyPolicy.MinPayment + HushMoneyPolicy.MaxPayment) / 2;

         HushMoneyPolicy.ClampPayment(reasonable).Should().Be(reasonable);
      }

      // The day of payment is inside the cooldown: the lord must not extort again yet, or paying achieves nothing.
      [Test]
      public void GIVEN_a_payment_within_the_cooldown_WHEN_checking_THEN_it_is_recently_paid()
      {
         int paid = 100;
         int today = paid + HushMoneyPolicy.CooldownDays - 1;

         HushMoneyPolicy.IsRecentlyPaid(paid, today).Should().BeTrue();
      }

      // Once the full cooldown has elapsed, the peace lapses and the lord may press again: blackmail is recurring
      // leverage, not a one-time buyout.
      [Test]
      public void GIVEN_the_cooldown_has_elapsed_WHEN_checking_THEN_it_is_no_longer_recently_paid()
      {
         int paid = 100;
         int today = paid + HushMoneyPolicy.CooldownDays;

         HushMoneyPolicy.IsRecentlyPaid(paid, today).Should().BeFalse();
      }

      // No payment on record is never "recently paid": a lord who was never paid presses freely.
      [Test]
      public void GIVEN_no_payment_recorded_WHEN_checking_THEN_it_is_not_recently_paid()
         => HushMoneyPolicy.IsRecentlyPaid(null, 500).Should().BeFalse();
   }
}
