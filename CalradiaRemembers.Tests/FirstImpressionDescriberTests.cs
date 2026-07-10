// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FirstImpressionDescriberTests
    {
        [TestCase(FirstImpressionDisposition.None)]
        [TestCase(FirstImpressionDisposition.Neutral)]
        public void GIVEN_a_neutral_or_unknown_disposition_WHEN_describing_THEN_nothing_is_injected(FirstImpressionDisposition d)
        {
            FirstImpressionDescriber.Describe(d).Should().BeNull();
        }

        [TestCase(FirstImpressionDisposition.Loyal)]
        [TestCase(FirstImpressionDisposition.Cordial)]
        [TestCase(FirstImpressionDisposition.Cool)]
        [TestCase(FirstImpressionDisposition.Arrogant)]
        [TestCase(FirstImpressionDisposition.Suspicious)]
        [TestCase(FirstImpressionDisposition.Demeaning)]
        public void GIVEN_a_flavoured_disposition_WHEN_describing_THEN_a_non_empty_line_is_produced(FirstImpressionDisposition d)
        {
            FirstImpressionDescriber.Describe(d).Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void GIVEN_the_loyal_opening_WHEN_describing_THEN_it_speaks_of_the_players_own_clan_and_never_a_tone_caveat()
        {
            string line = FirstImpressionDescriber.Describe(FirstImpressionDisposition.Loyal);

            line.Should().Contain("OWN CLAN");
            line.Should().NotContain("only their deeds"); // the caveat is for prejudiced openings, not a loyal one
        }

        [TestCase(FirstImpressionDisposition.Cool)]
        [TestCase(FirstImpressionDisposition.Arrogant)]
        [TestCase(FirstImpressionDisposition.Suspicious)]
        [TestCase(FirstImpressionDisposition.Demeaning)]
        public void GIVEN_a_prejudiced_opening_WHEN_describing_THEN_it_carries_the_tone_only_caveat(FirstImpressionDisposition d)
        {
            // The prejudice must never read as a real grievance: deeds, not the first impression, move regard.
            FirstImpressionDescriber.Describe(d).Should().Contain("only their deeds");
        }
    }
}
