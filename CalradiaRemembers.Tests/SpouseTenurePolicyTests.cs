// Code written by Gabriel Mailhot, 01/10/2026.
// raphareish (Nexus, 01/10/2026): a character just married (the notification was in the chat log) spoke of her spouse as
// if they had been married for years. The family line now says how recent the wedding is. If this rule is wrong, a bride
// of three days reminisces about years of marriage, or a couple wed long ago is called newlyweds (or, fkasad 10/10/2026,
// a mother of three thinks she wed last week).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SpouseTenurePolicyTests
    {
        // raphareish's case: wed days ago, and she is told so.
        [Test]
        public void GIVEN_a_wedding_days_ago_WHEN_the_spouse_is_named_THEN_they_are_newlyweds()
        {
            SpouseTenurePolicy.Clause(weddedOnDay: 100, today: 100).Should().Be(", wed today: you are newlyweds");
            SpouseTenurePolicy.Clause(weddedOnDay: 100, today: 101).Should().Be(", wed 1 day ago: you are newlyweds");
            SpouseTenurePolicy.Clause(weddedOnDay: 100, today: 103).Should().Be(", wed 3 days ago: you are newlyweds");
        }

        // Past a season they are no longer newlyweds, but within the year the marriage is still young.
        [Test]
        public void GIVEN_a_wedding_this_year_WHEN_the_spouse_is_named_THEN_it_is_still_young()
        {
            SpouseTenurePolicy.Clause(weddedOnDay: 100, today: 100 + SpouseTenurePolicy.NewlywedDays + 1).Should().Be(", wed less than a year ago");
        }

        // A marriage of long standing, or one whose day CR never saw (an old save, a campaign that starts married), reads
        // as it always did: no claim either way.
        [Test]
        public void GIVEN_an_old_or_unknown_wedding_WHEN_the_spouse_is_named_THEN_nothing_is_said()
        {
            SpouseTenurePolicy.Clause(weddedOnDay: 100, today: 100 + SpouseTenurePolicy.ThisYearDays + 1).Should().BeEmpty();
            SpouseTenurePolicy.Clause(weddedOnDay: null, today: 100).Should().BeEmpty();
            SpouseTenurePolicy.Clause(weddedOnDay: 120, today: 100).Should().BeEmpty();
        }

        // fkasad (Nexus, 10/10/2026): "a woman with three kids wouldn't believe she married a week ago". A marriage CR
        // never saw has no day, but the couple's eldest child says it is not new: the line gives that floor as a fact.
        [Test]
        public void GIVEN_an_unseen_wedding_and_a_child_of_some_years_WHEN_the_spouse_is_named_THEN_the_marriage_reads_as_years_old()
        {
            SpouseTenurePolicy.LongStanding("Elys", 12).Should().Be(", wed for years: Elys, your eldest together, is 12");
        }

        // An infant or a one-year-old proves no long marriage, and no child proves nothing: nothing is claimed.
        [Test]
        public void GIVEN_no_child_or_a_very_young_one_WHEN_the_spouse_is_named_THEN_no_length_is_claimed()
        {
            SpouseTenurePolicy.LongStanding("Elys", 1).Should().BeEmpty();
            SpouseTenurePolicy.LongStanding(null, 12).Should().BeEmpty();
        }
    }
}
