// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterAgeDescriberTests
    {
        [Test]
        public void GIVEN_a_letter_written_today_WHEN_describing_its_age_THEN_it_reads_sent_today()
        {
            LetterAgeDescriber.Describe(100, 100).Should().Be("sent today");
        }

        [Test]
        public void GIVEN_a_letter_written_yesterday_WHEN_describing_its_age_THEN_it_reads_sent_yesterday()
        {
            LetterAgeDescriber.Describe(99, 100).Should().Be("sent yesterday");
        }

        [Test]
        public void GIVEN_a_letter_several_days_old_WHEN_describing_its_age_THEN_it_counts_the_days()
        {
            LetterAgeDescriber.Describe(93, 100).Should().Be("sent 7 days ago");
        }

        [Test]
        public void GIVEN_an_unknown_writing_day_WHEN_describing_THEN_nothing_is_said()
        {
            // Letters from older saves carry no SentOnDay: no age line rather than a wrong one.
            LetterAgeDescriber.Describe(0, 100).Should().BeNull();
            LetterAgeDescriber.Describe(-5, 100).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_writing_day_in_the_future_WHEN_describing_THEN_nothing_is_said()
        {
            LetterAgeDescriber.Describe(105, 100).Should().BeNull();
        }
    }
}
