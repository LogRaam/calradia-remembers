// Code written by Gabriel Mailhot, 08/09/2026.
// What this pins: what a hired companion actually costs once a price has been agreed in conversation.
//
// Why it exists. Gabriel was charged 446 denars by a wanderer who had just said, twice, that his price was 595.
// The action carried 95 (he had counted aloud, "Five... ninety-five", and the fragment after the pause is what
// reached the game), and a clamp that pulled any figure into 75% to 125% of the asking price pulled 95 up to the
// floor of 446. The player paid a number nobody had ever said.
//
// Gabriel's ruling, which is the rule these tests keep: "une négociation c'est une entente sur ce qui est dit. Si
// il a été dit 595, il faut payer 595. Pas de bargainage après la conversation car ça fait pas de sens. L'entente
// est dite et acceptée, rien de moins. Et si le joueur ou le NPC a pas assez, la transaction ne peut se faire."
//
// The band was itself the bargaining-after-the-fact he rejects. Removing it makes the price honest by
// construction and moves the burden onto the model naming a sum it means, which is a prompt and bench problem
// (see the join_party_price_spoken_in_two_parts bench case) rather than something to paper over with arithmetic.
//
// If these tests fail, a player is about to be charged a figure that was never spoken aloud.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class HireAgreementPolicyTests
    {
        // THE ruling. Whatever was settled is what changes hands, even when it sits far from what the game would
        // have asked: a player who genuinely talked a sellsword down has earned that price, and one who agreed to
        // more has said so out loud.
        [TestCase(595, 595, 595)]
        [TestCase(200, 595, 200)]
        [TestCase(900, 595, 900)]
        public void GIVEN_a_price_was_agreed_WHEN_charging_THEN_exactly_that_price_is_charged(int proposed, int asking, int expected)
        {
            HireAgreementPolicy.AgreedPrice(proposed, asking).Should().Be(expected);
        }

        // The precise case that exposed the old rule: 95 reached the game where 595 had been spoken. It must now
        // be charged as it stands rather than silently corrected to 446, because a correction is what hid the
        // fault for as long as it did. The number being wrong is a matter for the bench, not for arithmetic here.
        [Test]
        public void GIVEN_the_misread_figure_that_started_this_WHEN_charging_THEN_it_is_no_longer_quietly_rewritten()
        {
            HireAgreementPolicy.AgreedPrice(95, 595).Should().Be(95).And.NotBe(446);
        }

        // No sum named at all is not a bargain, so nothing was negotiated away: the game's own asking price
        // stands in. This is the one case where a figure the conversation never produced may be used.
        [Test]
        public void GIVEN_no_price_was_ever_named_WHEN_charging_THEN_the_asking_price_stands_in()
        {
            HireAgreementPolicy.AgreedPrice(0, 595).Should().Be(595);
            HireAgreementPolicy.AgreedPrice(-40, 595).Should().Be(595);
        }

        // Degrades to something payable rather than to zero: a free companion handed over by a broken asking
        // price would be a worse failure than a token one.
        [Test]
        public void GIVEN_neither_a_price_nor_an_asking_figure_WHEN_charging_THEN_it_never_falls_to_nothing()
        {
            HireAgreementPolicy.AgreedPrice(0, 0).Should().BeGreaterThan(0);
        }

        // The second half of the ruling: a purse that cannot cover the bargain does not buy a cheaper version of
        // it. The deal simply does not close, which is also what the prompt tells the NPC to expect.
        [Test]
        public void GIVEN_the_purse_cannot_cover_the_bargain_WHEN_asked_THEN_the_deal_cannot_close()
        {
            HireAgreementPolicy.CanAfford(594, 595).Should().BeFalse();
            HireAgreementPolicy.CanAfford(595, 595).Should().BeTrue();
        }
    }
}
