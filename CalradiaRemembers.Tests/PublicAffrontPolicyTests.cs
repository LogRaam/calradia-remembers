// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the "insults travel" tuning: what becomes a public-disgrace rumour, and how far it
// carries. It breaks in silence — too sensitive and every cross word spreads, too coarse and grave
// affronts evaporate the moment the conversation closes.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PublicAffrontPolicyTests
    {
        // Baseline: ConversationEventFeeder scans every conversation's steepest relation drop and its
        // witness count, and feeds IsAffront straight into whether a WorldEvent gets recorded at all. A
        // grave drop before onlookers must register, or genuine public disgraces would never become gossip.
        [Test]
        public void GIVEN_a_grave_drop_before_witnesses_WHEN_judging_THEN_it_is_a_public_affront()
        {
            PublicAffrontPolicy.IsAffront(worstRelationDelta: -8, witnessCount: 2).Should().BeTrue();
        }

        // Pins AffrontThreshold as an INCLUSIVE bound (IsAffront uses <=): a drop landing exactly on -5
        // must already count, not require one point steeper.
        [Test]
        public void GIVEN_a_drop_exactly_at_the_threshold_WHEN_judging_THEN_it_still_counts()
        {
            //Arrange — the threshold is inclusive at -5.
            PublicAffrontPolicy.IsAffront(PublicAffrontPolicy.AffrontThreshold, witnessCount: 1).Should().BeTrue();
        }

        // The other side of that same line: a -4 cooling must NOT spread, or the threshold would be too
        // sensitive and every routine cross word would become a rumour, the exact failure mode the
        // production header warns against.
        [Test]
        public void GIVEN_a_drop_just_short_of_the_threshold_WHEN_judging_THEN_it_does_not_spread()
        {
            //Arrange — a -4 cooling is a slight, not a disgrace.
            PublicAffrontPolicy.IsAffront(-4, witnessCount: 3).Should().BeFalse();
        }

        // Even a steep, grave-worthy drop must not spread with zero witnesses: gossip needs someone to
        // carry it, so a private affront (however severe) stays a private matter between the player and
        // that one NPC.
        [Test]
        public void GIVEN_a_grave_drop_with_no_one_watching_WHEN_judging_THEN_it_does_not_spread()
        {
            //Arrange — a private affront leaves no rumour.
            PublicAffrontPolicy.IsAffront(-20, witnessCount: 0).Should().BeFalse();
        }

        // Guards the sign check: a relation GAIN or a no-op delta must never be read as an affront no
        // matter how many people are watching, or a warm exchange before a crowd could wrongly spawn a
        // disgrace rumour.
        [Test]
        public void GIVEN_a_positive_or_neutral_delta_WHEN_judging_THEN_it_is_never_an_affront()
        {
            PublicAffrontPolicy.IsAffront(0, witnessCount: 5).Should().BeFalse();
            PublicAffrontPolicy.IsAffront(3, witnessCount: 5).Should().BeFalse();
        }

        // Pins the growth curve itself: a larger audience must carry the disgrace further (feeds
        // WorldEvent.Magnitude directly), or the "how far it carries" half of the policy would be untested
        // even though IsAffront's gate above is.
        [Test]
        public void GIVEN_a_small_audience_WHEN_computing_magnitude_THEN_it_grows_with_the_witnesses()
        {
            //Arrange — 25 base + 2 witnesses x 5 = 35.
            PublicAffrontPolicy.Magnitude(witnessCount: 2).Should().Be(35);
        }

        // Uncapped growth would let one huge-audience scene outweigh every other world event forever
        // ("no single scene becomes legend" per the production doc): pins that a big crowd is bounded at
        // MaxMagnitude, not left to climb without limit.
        [Test]
        public void GIVEN_a_large_audience_WHEN_computing_magnitude_THEN_it_caps()
        {
            //Arrange — 25 + 10 x 5 = 75, capped at 50.
            PublicAffrontPolicy.Magnitude(witnessCount: 10).Should().Be(50);
        }

        // Pins the exact witness count where the linear curve first meets MaxMagnitude, the seam between
        // the growth test above and the cap test below: gets this one wrong and the curve either caps one
        // witness too early or overshoots the cap by five before the clamp kicks in.
        [Test]
        public void GIVEN_the_audience_size_that_lands_exactly_on_the_cap_WHEN_computing_magnitude_THEN_it_is_the_cap()
        {
            //Arrange — 25 + 5 x 5 = 50, exactly the cap.
            PublicAffrontPolicy.Magnitude(witnessCount: 5).Should().Be(50);
        }
    }
}
