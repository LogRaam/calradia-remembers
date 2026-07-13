// Code written by Gabriel Mailhot, 10/07/2026.
// Pins how a resolved FirstImpressionDisposition is rendered into the prompt's "first impression" guidance
// line, consumed by FirstImpressionResolver.Resolve and injected as EncounterContext.OpeningDisposition.
//
// WHY IT MATTERS (player report, ProudSeal 2026-07-10): nobles were opening too neutral and reasonable, with
// no rivalry unless the player offended first. This feature lets a barely-known lord's traits/standing colour
// their OPENING TONE. The one rule that must never break: it colours tone ONLY, never the relation numbers
// (per the production header's own explicit contract, "nothing more"). Every prejudiced or reserved line
// carries a caveat saying the NPC has done nothing to wrong the player and only deeds move regard, specifically
// so the model never mistakes a rolled first impression for a real grievance and applies an automatic malus.

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
        // None means no ruling was made; Neutral is the ordinary default an already-known NPC keeps. Neither
        // should inject anything: per FirstImpressionResolver, an already-known NPC keeps their existing
        // stance, so a stray line here would contradict that and needlessly spend prompt budget besides.
        [TestCase(FirstImpressionDisposition.None)]
        [TestCase(FirstImpressionDisposition.Neutral)]
        public void GIVEN_a_neutral_or_unknown_disposition_WHEN_describing_THEN_nothing_is_injected(FirstImpressionDisposition d)
        {
            FirstImpressionDescriber.Describe(d).Should().BeNull();
        }

        // Every flavoured disposition (the whole point of the feature, per the ProudSeal report) must actually
        // produce guidance text; a silently-null case here is a lord whose rolled disposition never reaches
        // the prompt at all, and the feature does nothing for that NPC.
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

        // A same-clan NPC is explicitly exempt from prejudice (FirstImpressionResolver: "a lord in the
        // player's OWN clan is exempt, he opens loyal"), and the caveat below exists only to stop a rolled
        // prejudice from reading as a real grievance; a loyal opening was never a grievance in the first place,
        // so tacking the caveat onto it would be a non-sequitur in the prompt text the model reads.
        [Test]
        public void GIVEN_the_loyal_opening_WHEN_describing_THEN_it_speaks_of_the_players_own_clan_and_never_a_tone_caveat()
        {
            string line = FirstImpressionDescriber.Describe(FirstImpressionDisposition.Loyal);

            line.Should().Contain("OWN CLAN");
            line.Should().NotContain("only their deeds"); // the caveat is for prejudiced openings, not a loyal one
        }

        // The one rule that must never break, stated in both the production header and this file's own
        // header: first impressions colour TONE only, never relation. Every reserved or prejudiced opening
        // must carry the caveat, or the model could read a rolled dislike as a real, relation-moving grievance
        // the NPC never actually suffered.
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
