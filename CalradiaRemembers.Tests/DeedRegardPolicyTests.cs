// Code written by Gabriel Mailhot, 23/10/2026.
// DeedRegardPolicy says what each deed of the player does to a character's personal regard, the score the player's page
// lists by. Gabriel (23/10/2026): "il faudrait que pour chaque action il y a un impact sur l'estime, qu'il soit positif ou
// negatif. Car je pourrais complimenter un NPC et augmenter son estime, pour ensuite commettre un mefait contre lui qui ne
// changerait pas sa perception de moi." If this is wrong, a man whose wife the player took still holds the player in no
// contempt, a deed is charged twice, or a purse of gold buys a friend in an afternoon.

#region

using System;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DeedRegardPolicyTests
   {
      // STAKE: Gabriel: "pour chaque action il y a un impact sur l'estime": no deed in the table moves nothing at all.
      [Test]
      public void GIVEN_every_deed_WHEN_its_regard_is_read_THEN_someone_feels_it()
      {
         foreach (PlayerDeed deed in Enum.GetValues(typeof(PlayerDeed)).Cast<PlayerDeed>().Where(d => d != PlayerDeed.Gift))
         {
            DeedRegard r = DeedRegardPolicy.For(deed);
            (r.Victim != 0 || r.Kin != 0 || r.VictimAlreadyApplied).Should().BeTrue($"{deed} must move someone's regard");
         }
      }

      // STAKE: Arcor (Gabriel's save, 23/10/2026): a man whose wife the player took must land in "Hold you in contempt"
      // (regard -20 or lower) from a neutral start, and his family must feel it too.
      [Test]
      public void GIVEN_a_spouse_taken_WHEN_read_THEN_the_one_left_falls_into_contempt_and_the_family_cools()
      {
         DeedRegard r = DeedRegardPolicy.For(PlayerDeed.StoleSpouse);

         r.Victim.Should().BeLessThanOrEqualTo(-20);
         r.Kin.Should().BeNegative();
      }

      // STAKE: Gabriel's exploit: a compliment, then a misdeed. Every misdeed against a living victim must undo far more
      // than an afternoon of compliments (+1 every few hours in conversation).
      [TestCase(PlayerDeed.StoleSpouse)]
      [TestCase(PlayerDeed.HandedToEnemy)]
      [TestCase(PlayerDeed.TookByForce)]
      [TestCase(PlayerDeed.Tortured)]
      [TestCase(PlayerDeed.RevokedFief)]
      [TestCase(PlayerDeed.Expelled)]
      public void GIVEN_a_misdeed_against_a_living_victim_WHEN_read_THEN_it_costs_tens_at_once(PlayerDeed deed)
      {
         DeedRegardPolicy.For(deed).Victim.Should().BeLessThanOrEqualTo(-20);
      }

      // STAKE: the dead feel nothing; an execution or a murder falls on the family, and a murder is no lighter.
      [Test]
      public void GIVEN_a_death_by_the_players_hand_WHEN_read_THEN_only_the_kin_pay_and_murder_weighs_as_execution()
      {
         DeedRegardPolicy.For(PlayerDeed.Executed).Victim.Should().Be(0);
         DeedRegardPolicy.For(PlayerDeed.Murdered).Victim.Should().Be(0);
         DeedRegardPolicy.For(PlayerDeed.Executed).Kin.Should().BeLessThanOrEqualTo(-50);
         DeedRegardPolicy.For(PlayerDeed.Murdered).Kin.Should().Be(DeedRegardPolicy.For(PlayerDeed.Executed).Kin);
      }

      // STAKE: a beating and a robbery already cost the victim on their own path (-50, -30); charging them again here would
      // double the hit.
      [TestCase(PlayerDeed.Beat)]
      [TestCase(PlayerDeed.Robbed)]
      public void GIVEN_a_deed_whose_path_already_charges_the_victim_WHEN_read_THEN_it_is_not_charged_twice(PlayerDeed deed)
      {
         DeedRegard r = DeedRegardPolicy.For(deed);

         r.VictimAlreadyApplied.Should().BeTrue();
         r.Victim.Should().Be(0);
      }

      // STAKE: the positive side too: mercy, marriage and a shared night warm the one who received them.
      [TestCase(PlayerDeed.FreedPrisoner)]
      [TestCase(PlayerDeed.Married)]
      [TestCase(PlayerDeed.SharedNight)]
      [TestCase(PlayerDeed.FriendlyRivalry)]
      public void GIVEN_a_kindness_WHEN_read_THEN_it_warms(PlayerDeed deed)
      {
         DeedRegardPolicy.For(deed).Victim.Should().BePositive();
      }

      // STAKE: a gift warms by its worth, and a second gift the same day buys nothing.
      [TestCase(100, false, 1)]
      [TestCase(500, false, 2)]
      [TestCase(2000, false, 4)]
      [TestCase(20000, false, 6)]
      [TestCase(20000, true, 0)]
      [TestCase(0, false, 0)]
      public void GIVEN_a_gift_WHEN_its_regard_is_read_THEN_it_scales_with_value_once_a_day(int value, bool alreadyToday, int expected)
      {
         DeedRegardPolicy.GiftRegard(value, alreadyToday).Should().Be(expected);
      }
   }
}
