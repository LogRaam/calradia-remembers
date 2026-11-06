// Code written by Gabriel Mailhot, 04/11/2026.
// CaptiveNameMatch decides whether a name written in conversation points at a captive. froggyluv (Nexus, 04/11/2026):
// "Eilidh holds no captive called Elthild of dey Tihr. Nothing is paid." The model wrote the name with the house, the game
// knows her as "Elthild". If this is wrong, a sale both sides agreed fails on a name, or the wrong captive changes hands.

#region

using CalradiaRemembers.Logic.Prisoners;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Prisoners
{
   [TestFixture]
   public sealed class CaptiveNameMatchTests
   {
      // STAKE: froggyluv's sale: the captive named with her house is the captive the player holds.
      [TestCase("Elthild", "Elthild of dey Tihr")]
      [TestCase("Elthild", "Lady Elthild")]
      [TestCase("Elthild", "elthild")]
      public void GIVEN_the_name_written_with_a_house_or_title_WHEN_matched_THEN_it_is_the_captive(string hero, string written)
      {
         CaptiveNameMatch.Matches(hero, written).Should().BeTrue();
      }

      // STAKE: what already worked keeps working: a part of the name still finds the captive.
      [Test]
      public void GIVEN_part_of_the_name_WHEN_matched_THEN_it_is_the_captive()
      {
         CaptiveNameMatch.Matches("Elthild of dey Tihr", "Elthild").Should().BeTrue();
      }

      // STAKE: a short name inside a longer one is someone else; the wrong captive must never change hands.
      [TestCase("Ali", "Alinka of the Battanians")]
      [TestCase("Elthild", "Gareth")]
      [TestCase("Elthild", "")]
      public void GIVEN_another_name_WHEN_matched_THEN_it_is_not_the_captive(string hero, string written)
      {
         CaptiveNameMatch.Matches(hero, written).Should().BeFalse();
      }

      // STAKE: RaveN017Mod (Nexus, 05/11/2026), several sales in a row: the name spelled exactly is that captive, even when it
      // is also part of another captive's name.
      [Test]
      public void GIVEN_one_captive_spelled_exactly_and_another_containing_it_WHEN_the_best_is_chosen_THEN_it_is_the_exact_one()
      {
         CaptiveNameMatch.Best(new[] {"Irana", "Ira"}, "ira").Should().Be(1);
      }

      // STAKE: the house around the name still finds the one captive it can be (froggyluv's sale keeps working).
      [Test]
      public void GIVEN_the_name_with_a_house_WHEN_the_best_is_chosen_THEN_it_is_that_captive()
      {
         CaptiveNameMatch.Best(new[] {"Monchug", "Elthild"}, "Elthild of dey Tihr").Should().Be(1);
      }

      // STAKE: a name that could be two captives must sell neither: the old first-in-the-roster pick sold the wrong man.
      [Test]
      public void GIVEN_a_name_two_captives_could_be_WHEN_the_best_is_chosen_THEN_none_is()
      {
         CaptiveNameMatch.Best(new[] {"Irana", "Iranel"}, "Iran").Should().Be(-1);
      }

      // STAKE: a name nobody holds is refused, not matched to whoever comes first.
      [Test]
      public void GIVEN_a_name_nobody_holds_WHEN_the_best_is_chosen_THEN_none_is()
      {
         CaptiveNameMatch.Best(new[] {"Monchug"}, "Gareth").Should().Be(-1);
      }

      // STAKE: 3chmidt (05/11/2026): "I have this captive, I want to give him to you". With one captive held, the gift needs no name.
      [Test]
      public void GIVEN_one_captive_held_WHEN_a_gift_names_no_one_THEN_that_captive_is_given()
      {
         CaptiveNameMatch.ForGift(new[] {"Monchug"}, null).Should().Be(0);
      }

      // STAKE: with several held and no name, a gift never picks a man the player did not mean.
      [Test]
      public void GIVEN_several_captives_held_WHEN_a_gift_names_no_one_THEN_none_is_given()
      {
         CaptiveNameMatch.ForGift(new[] {"Monchug", "Ira"}, "").Should().Be(-1);
      }

      // STAKE: a named gift reads the name as a sale does, house and all.
      [Test]
      public void GIVEN_several_captives_held_WHEN_a_gift_names_one_with_his_house_THEN_that_one_is_given()
      {
         CaptiveNameMatch.ForGift(new[] {"Ira", "Monchug"}, "Monchug of the Arkits").Should().Be(1);
      }
   }
}
