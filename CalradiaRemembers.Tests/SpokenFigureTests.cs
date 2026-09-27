// Code written by Gabriel Mailhot, 27/09/2026.
// The last sum a speaker named, read from what they said, in digits or in words.
//
// tashmetu (Nexus, 27/09/2026, on 2.5.25): an envoy haggled 8,105, then 10,000, then "a final offer of eleven
// thousand five hundred"; the player answered "i will return him", and the captive went home for nothing, because a
// reply naming no sum was read as a gift. An acceptance with no figure closes at the last offer on the table, and
// that offer is often written out in words. If this reads the wrong figure, the player is paid a sum nobody offered.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SpokenFigureTests
   {
      // tashmetu's own line: the final offer, in words.
      [Test]
      public void GIVEN_a_sum_written_in_words_WHEN_read_THEN_it_is_the_figure()
      {
         SpokenFigure.LastIn("My lord makes a final offer of eleven thousand five hundred denars.", 100).Should().Be(11500);
      }

      // Digits with thousands separators, as models usually write them.
      [Test]
      public void GIVEN_a_sum_written_in_digits_WHEN_read_THEN_it_is_the_figure()
      {
         SpokenFigure.LastIn("I can offer 3,400 denars, no more.", 100).Should().Be(3400);
      }

      // Several sums in one line: the LAST is the offer standing ("his authority extends to 4,839; I offer 3,400").
      [Test]
      public void GIVEN_several_sums_WHEN_read_THEN_the_last_one_is_the_offer()
      {
         SpokenFigure.LastIn("You asked 4,000; I offer three thousand four hundred.", 100).Should().Be(3400);
      }

      [TestCase("one thousand and fifty", 1050)]
      [TestCase("twenty-five hundred", 2500)]
      [TestCase("nine hundred and ninety-nine", 999)]
      public void GIVEN_common_spoken_sums_WHEN_read_THEN_each_is_the_figure(string words, int expected)
      {
         SpokenFigure.LastIn(words, 100).Should().Be(expected);
      }

      // Small numbers in ordinary speech ("two or three days", "no one") are not offers.
      [Test]
      public void GIVEN_only_small_numbers_WHEN_read_with_a_minimum_THEN_there_is_no_sum()
      {
         SpokenFigure.LastIn("Give it two or three days, no one will know.", 100).Should().BeNull();
      }
   }
}
