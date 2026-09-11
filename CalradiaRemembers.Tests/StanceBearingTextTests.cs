// Code written by Gabriel Mailhot, 04/07/2026.
// The "Bearing" encyclopedia line: numberless Trust/Respect/Fear phrasing (NOT Affection — see
// StanceBearingText's own header for why).
//
// Feeds EncyclopediaHeroPageBearingMixin's "Bearing:" widget (AutoHideRichTextWidget, which collapses
// on an EMPTY STRING, not null): Compose must return "" rather than null when every axis is neutral,
// or the widget stays visibly blank instead of hiding. Numberless by design: the player earns a WORD
// here, never a digit (ROADMAP "Stance becomes VISIBLE": diegetically justified, "a man's bearing shows").

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

        // Pins the exact wording of the Strong-positive tier, the only trust phrase the encyclopedia
        // ever shows for a trusting NPC.
        [Test]
        public void GIVEN_strong_positive_trust_WHEN_phrasing_THEN_it_reads_trusts_your_word()
        {
            StanceBearingText.TrustPhrase(StanceBearingText.Strong).Should().Be("trusts your word");
        }

        // The negative mirror of the same tier; Trust is bipolar (unlike Fear), so this pole must exist
        // and read distinctly from mere neutrality.
        [Test]
        public void GIVEN_strong_negative_trust_WHEN_phrasing_THEN_it_reads_does_not_trust_your_word()
        {
            StanceBearingText.TrustPhrase(-StanceBearingText.Strong).Should().Be("does not trust your word");
        }

        // Below Strong magnitude (either sign), Trust stays UNVOICED: a mild, still-forming trust must
        // not leak onto the hero page before it is genuinely strong enough to describe in words.
        [Test]
        public void GIVEN_neutral_trust_WHEN_phrasing_THEN_it_is_null()
        {
            StanceBearingText.TrustPhrase(0).Should().BeNull();
            StanceBearingText.TrustPhrase(StanceBearingText.Strong - 1).Should().BeNull();
        }

        // ── RespectPhrase ────────────────────────────────────────────────────

        // Pins the exact wording of the Strong-positive Respect tier.
        [Test]
        public void GIVEN_strong_positive_respect_WHEN_phrasing_THEN_it_reads_respects_your_strength()
        {
            StanceBearingText.RespectPhrase(StanceBearingText.Strong).Should().Be("respects your strength");
        }

        // The negative mirror; open contempt must read distinctly from mere unfamiliarity.
        [Test]
        public void GIVEN_strong_negative_respect_WHEN_phrasing_THEN_it_reads_holds_you_in_contempt()
        {
            StanceBearingText.RespectPhrase(-StanceBearingText.Strong).Should().Be("holds you in contempt");
        }

        // Below Strong magnitude, Respect stays unvoiced, same threshold discipline as Trust.
        [Test]
        public void GIVEN_neutral_respect_WHEN_phrasing_THEN_it_is_null()
        {
            StanceBearingText.RespectPhrase(0).Should().BeNull();
        }

        // ── FearPhrase (graduated, 0..100 only) ─────────────────────────────

        // FearStrong is the top of the graduated ladder (Fear is 0..100, unipolar: it has no negative
        // pole to voice, unlike Trust/Respect).
        [Test]
        public void GIVEN_high_fear_WHEN_phrasing_THEN_it_reads_fears_you()
        {
            StanceBearingText.FearPhrase(StanceBearingText.FearStrong).Should().Be("fears you");
        }

        // The middle rung: pins that a real-but-not-total fear reads distinctly weaker than "fears you",
        // so the ladder actually graduates instead of jumping straight to the top phrase.
        [Test]
        public void GIVEN_moderate_fear_WHEN_phrasing_THEN_it_reads_is_wary_of_you()
        {
            StanceBearingText.FearPhrase(StanceBearingText.FearModerate).Should().Be("is wary of you");
        }

        // The lowest voiced rung (FearMild): pins where the ladder STARTS, below which fear is too faint
        // to put into words at all.
        [Test]
        public void GIVEN_mild_fear_WHEN_phrasing_THEN_it_reads_is_uneasy_around_you()
        {
            StanceBearingText.FearPhrase(StanceBearingText.FearMild).Should().Be("is uneasy around you");
        }

        // Below FearMild, Fear stays unvoiced entirely: an unafraid NPC's Bearing line says nothing
        // about fear rather than forcing a false "calm" phrase.
        [Test]
        public void GIVEN_no_fear_WHEN_phrasing_THEN_it_is_null()
        {
            StanceBearingText.FearPhrase(0).Should().BeNull();
        }

        // ── Compose ──────────────────────────────────────────────────────────

        // The exact contract the consuming widget depends on (see file header): Compose must return "",
        // never null, so AutoHideRichTextWidget can collapse on an empty string as designed.
        [Test]
        public void GIVEN_every_axis_wholly_neutral_WHEN_composing_THEN_it_is_an_empty_string_not_null()
        {
            StanceBearingText.Compose(new StanceBearingText.BearingFacts {Trust = 0, Respect = 0, Fear = 0}).Should().Be("");
        }

        // Guards that neutral axes are silently skipped rather than joined as empty fragments (which
        // would leave stray commas/spaces in the composed line).
        [Test]
        public void GIVEN_only_fear_stands_out_WHEN_composing_THEN_only_the_fear_phrase_appears()
        {
            StanceBearingText.Compose(new StanceBearingText.BearingFacts {Trust = 0, Respect = 0, Fear = StanceBearingText.FearStrong}).Should().Be("fears you");
        }

        // Pins the STABLE join order (trust, respect, fear): a reordering would read as a random
        // shuffle each time the hero page is opened instead of a consistent voice.
        [Test]
        public void GIVEN_every_axis_stands_out_WHEN_composing_THEN_all_three_join_in_trust_respect_fear_order()
        {
            string result = StanceBearingText.Compose(new StanceBearingText.BearingFacts {Trust = StanceBearingText.Strong, Respect = -StanceBearingText.Strong, Fear = StanceBearingText.FearStrong});

            result.Should().Be("trusts your word, holds you in contempt, fears you");
        }

        // The whole point of composing per-axis phrases: any believable combination (distrustful yet
        // respectful yet only mildly uneasy) must read as one coherent sentence, not contradict itself.
        [Test]
        public void GIVEN_a_believable_wary_but_unafraid_combination_WHEN_composing_THEN_it_reads_naturally()
        {
            string result = StanceBearingText.Compose(new StanceBearingText.BearingFacts {Trust = -StanceBearingText.Strong, Respect = StanceBearingText.Strong, Fear = StanceBearingText.FearMild});

            result.Should().Be("does not trust your word, respects your strength, is uneasy around you");
        }
    }
}
