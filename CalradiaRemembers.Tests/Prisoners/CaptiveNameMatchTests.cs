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
   }
}
