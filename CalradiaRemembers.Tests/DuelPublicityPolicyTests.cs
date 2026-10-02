// Code written by Gabriel Mailhot, 02/10/2026.
// fkasad (Nexus, 01/10/2026): a duel won against a faction's head in his own keep logged "0 witness(es)" with fifteen lords
// inside, and its news weighed 6 where any battle with a lord weighs 40. If these rules are wrong, a duel before a whole
// court counts as fought in an empty field, beating a ruler is smaller news than beating a nobody's levy, or a crowded
// keep makes one duel worth a war in renown and influence.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DuelPublicityPolicyTests
    {
        // fkasad's keep: nobody in the conversation's witness list, fifteen lords in the hall. They saw it.
        [Test]
        public void GIVEN_no_conversation_witnesses_but_a_full_hall_WHEN_the_audience_is_counted_THEN_the_hall_counts()
        {
            DuelPublicityPolicy.Audience(conversationWitnesses: 0, presentInPlace: 6).Should().Be(6);
            DuelPublicityPolicy.Audience(conversationWitnesses: 3, presentInPlace: 1).Should().Be(3);
        }

        // Past ten, a crowd is a crowd: renown, the loser's influence and the news all weigh at most ten.
        [Test]
        public void GIVEN_a_crowded_keep_WHEN_the_audience_is_counted_THEN_it_is_capped()
        {
            DuelPublicityPolicy.Audience(conversationWitnesses: 0, presentInPlace: 15).Should().Be(DuelPublicityPolicy.MaxWitnessesWeighed);
        }

        // Beating a ruler before his court is big news; beating a man of no house alone, a little.
        [Test]
        public void GIVEN_who_was_fought_and_before_whom_WHEN_the_news_is_weighed_THEN_station_and_audience_carry_it()
        {
            DuelPublicityPolicy.Magnitude(opponentStation: 0, audience: 0, fought: true).Should().Be(DuelPublicityPolicy.BaseMagnitude);
            DuelPublicityPolicy.Magnitude(opponentStation: 3, audience: 10, fought: true).Should().Be(65);
            DuelPublicityPolicy.Magnitude(opponentStation: 3, audience: 10, fought: true)
                               .Should().BeGreaterThan(40, "a ruler beaten before his court outweighs the floor of any battle with a lord");
        }

        // A challenge refused is talked of, but less than a fight.
        [Test]
        public void GIVEN_a_duel_refused_WHEN_the_news_is_weighed_THEN_it_travels_half_as_far()
        {
            DuelPublicityPolicy.Magnitude(opponentStation: 2, audience: 4, fought: false)
                               .Should().Be(DuelPublicityPolicy.Magnitude(opponentStation: 2, audience: 4, fought: true) / 2);
        }
    }
}
