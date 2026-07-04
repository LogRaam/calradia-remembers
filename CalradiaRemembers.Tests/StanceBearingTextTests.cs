// Code written by Gabriel Mailhot, 04/07/2026.
// The "Bearing" encyclopedia line: numberless Trust/Respect/Fear phrasing (NOT Affection — see
// StanceBearingText's own header for why).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceBearingTextTests
    {
        // ── TrustPhrase ──────────────────────────────────────────────────────

        [Test]
        public void GIVEN_strong_positive_trust_WHEN_phrasing_THEN_it_reads_trusts_your_word()
        {
            StanceBearingText.TrustPhrase(StanceBearingText.Strong).Should().Be("trusts your word");
        }

        [Test]
        public void GIVEN_strong_negative_trust_WHEN_phrasing_THEN_it_reads_does_not_trust_your_word()
        {
            StanceBearingText.TrustPhrase(-StanceBearingText.Strong).Should().Be("does not trust your word");
        }

        [Test]
        public void GIVEN_neutral_trust_WHEN_phrasing_THEN_it_is_null()
        {
            StanceBearingText.TrustPhrase(0).Should().BeNull();
            StanceBearingText.TrustPhrase(StanceBearingText.Strong - 1).Should().BeNull();
        }

        // ── RespectPhrase ────────────────────────────────────────────────────

        [Test]
        public void GIVEN_strong_positive_respect_WHEN_phrasing_THEN_it_reads_respects_your_strength()
        {
            StanceBearingText.RespectPhrase(StanceBearingText.Strong).Should().Be("respects your strength");
        }

        [Test]
        public void GIVEN_strong_negative_respect_WHEN_phrasing_THEN_it_reads_holds_you_in_contempt()
        {
            StanceBearingText.RespectPhrase(-StanceBearingText.Strong).Should().Be("holds you in contempt");
        }

        [Test]
        public void GIVEN_neutral_respect_WHEN_phrasing_THEN_it_is_null()
        {
            StanceBearingText.RespectPhrase(0).Should().BeNull();
        }

        // ── FearPhrase (graduated, 0..100 only) ─────────────────────────────

        [Test]
        public void GIVEN_high_fear_WHEN_phrasing_THEN_it_reads_fears_you()
        {
            StanceBearingText.FearPhrase(StanceBearingText.FearStrong).Should().Be("fears you");
        }

        [Test]
        public void GIVEN_moderate_fear_WHEN_phrasing_THEN_it_reads_is_wary_of_you()
        {
            StanceBearingText.FearPhrase(StanceBearingText.FearModerate).Should().Be("is wary of you");
        }

        [Test]
        public void GIVEN_mild_fear_WHEN_phrasing_THEN_it_reads_is_uneasy_around_you()
        {
            StanceBearingText.FearPhrase(StanceBearingText.FearMild).Should().Be("is uneasy around you");
        }

        [Test]
        public void GIVEN_no_fear_WHEN_phrasing_THEN_it_is_null()
        {
            StanceBearingText.FearPhrase(0).Should().BeNull();
        }

        // ── Compose ──────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_every_axis_wholly_neutral_WHEN_composing_THEN_it_is_an_empty_string_not_null()
        {
            StanceBearingText.Compose(0, 0, 0).Should().Be("");
        }

        [Test]
        public void GIVEN_only_fear_stands_out_WHEN_composing_THEN_only_the_fear_phrase_appears()
        {
            StanceBearingText.Compose(0, 0, StanceBearingText.FearStrong).Should().Be("fears you");
        }

        [Test]
        public void GIVEN_every_axis_stands_out_WHEN_composing_THEN_all_three_join_in_trust_respect_fear_order()
        {
            string result = StanceBearingText.Compose(StanceBearingText.Strong, -StanceBearingText.Strong, StanceBearingText.FearStrong);

            result.Should().Be("trusts your word, holds you in contempt, fears you");
        }

        [Test]
        public void GIVEN_a_believable_wary_but_unafraid_combination_WHEN_composing_THEN_it_reads_naturally()
        {
            string result = StanceBearingText.Compose(-StanceBearingText.Strong, StanceBearingText.Strong, StanceBearingText.FearMild);

            result.Should().Be("does not trust your word, respects your strength, is uneasy around you");
        }
    }
}
