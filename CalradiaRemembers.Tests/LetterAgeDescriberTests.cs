// Code written by Gabriel Mailhot, 10/07/2026.
// Player report: a letter written before current events read as a fresh, incoherent message with no sense of
// when it was actually sent. This decides the age line LetterViewModel shows in the read window's heading
// ("From X, sent 3 days ago"), so a dispatch reads as the dated note it is. Get the boundary wrong and either
// a same-day letter falsely claims to be old, or a letter is silently dated into the future, both of which
// would misrepresent when the sender actually wrote it.

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
        // The zero-day boundary must read as the special-cased "today", not fall through to the generic
        // "sent 0 days ago" phrasing that would sound stilted next to a fresh letter.
        [Test]
        public void GIVEN_a_letter_written_today_WHEN_describing_its_age_THEN_it_reads_sent_today()
        {
            LetterAgeDescriber.Describe(100, 100).Should().Be("sent today");
        }

        // Same special-casing one day further out: "yesterday" reads naturally where "sent 1 days ago" (the
        // ungrammatical plural) would not.
        [Test]
        public void GIVEN_a_letter_written_yesterday_WHEN_describing_its_age_THEN_it_reads_sent_yesterday()
        {
            LetterAgeDescriber.Describe(99, 100).Should().Be("sent yesterday");
        }

        // Past the two special-cased days, the generic "sent N days ago" must take over and count correctly,
        // which is the whole point of the feature: a letter penned before current events must read as dated.
        [Test]
        public void GIVEN_a_letter_several_days_old_WHEN_describing_its_age_THEN_it_counts_the_days()
        {
            LetterAgeDescriber.Describe(93, 100).Should().Be("sent 7 days ago");
        }

        // Production doc: null "when the writing day is unknown (older saves)". A save from before this
        // feature existed has no SentOnDay recorded; guessing an age for it would show a confidently wrong
        // date, so the heading must omit the age line entirely instead.
        [Test]
        public void GIVEN_an_unknown_writing_day_WHEN_describing_THEN_nothing_is_said()
        {
            // Letters from older saves carry no SentOnDay: no age line rather than a wrong one.
            LetterAgeDescriber.Describe(0, 100).Should().BeNull();
            LetterAgeDescriber.Describe(-5, 100).Should().BeNull();
        }

        // A future SentOnDay is nonsensical (production doc), likely a data or clock inconsistency; the same
        // "say nothing rather than something wrong" rule applies as for an unknown day.
        [Test]
        public void GIVEN_a_writing_day_in_the_future_WHEN_describing_THEN_nothing_is_said()
        {
            LetterAgeDescriber.Describe(105, 100).Should().BeNull();
        }
    }
}
