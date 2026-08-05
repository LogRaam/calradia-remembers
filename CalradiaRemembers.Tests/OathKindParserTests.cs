// Code written by Gabriel Mailhot, 04/08/2026.
// OathKindParser is the ONE bridge between what the LLM writes in a swear_oath [RESOLUTION]'s oath_kind field
// and the real CommitmentKind CouncilLift.SettleSwearOath tracks. The stake: an oath kind must be resolved from
// a STRUCTURED whitelist, never a guess, and "move_against" must NEVER resolve here, since that is
// pledge_against's own separate resolution with its own scheme-launching executor, a different mechanism the
// model must not be able to invoke under swear_oath's name.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OathKindParserTests
    {
        // Baseline: every whitelisted oath kind must round-trip from its plain name, or the council could
        // never ground a swear_oath resolution CommitmentBehavior actually knows how to track.
        [TestCase("pay_gold", CommitmentKind.PayGold)]
        [TestCase("keep_peace", CommitmentKind.KeepPeace)]
        [TestCase("protect", CommitmentKind.Protect)]
        public void GIVEN_a_whitelisted_oath_kind_WHEN_parsed_THEN_it_resolves_to_the_matching_kind(string text, CommitmentKind expected)
        {
            OathKindParser.TryParse(text, out CommitmentKind kind).Should().BeTrue();
            kind.Should().Be(expected);
        }

        // The model is not guaranteed to match case exactly; a kind parsed only on exact case would silently
        // drop a perfectly good "PAY_GOLD" or "Pay_Gold" and refuse an oath the LLM and the player both intended.
        [TestCase("PAY_GOLD")]
        [TestCase("Pay_Gold")]
        [TestCase("pay_gold")]
        public void GIVEN_mixed_case_WHEN_parsed_THEN_it_still_resolves(string text)
        {
            OathKindParser.TryParse(text, out CommitmentKind kind).Should().BeTrue();
            kind.Should().Be(CommitmentKind.PayGold);
        }

        // Surrounding whitespace (a stray newline or padding from the [RESOLUTION] block's key-value parsing)
        // must not defeat the match, mirroring how every other TargetHint-style field is read tolerantly.
        [Test]
        public void GIVEN_surrounding_whitespace_WHEN_parsed_THEN_it_still_resolves()
        {
            OathKindParser.TryParse("  keep_peace  \n", out CommitmentKind kind).Should().BeTrue();
            kind.Should().Be(CommitmentKind.KeepPeace);
        }

        // The stake this whole parser exists for: MoveAgainst is pledge_against's OWN separate resolution
        // (a real scheme launched through SchemeStore.ForcePlot), never a second doorway into it under a
        // different name. A swear_oath naming "move_against" must fail to resolve, not silently become a pledge.
        [Test]
        public void GIVEN_move_against_WHEN_parsed_THEN_it_is_refused_because_that_is_pledge_against_own_kind()
        {
            OathKindParser.TryParse("move_against", out CommitmentKind kind).Should().BeFalse();
            kind.Should().Be(default(CommitmentKind));
        }

        // Blank or unrecognised text (never emitted, or a hallucinated kind outside the whitelist) must fail
        // CLEANLY, never guessed at the lift: CouncilLift.SettleSwearOath treats a failed parse as a guard
        // failure, refusing the oath rather than tracking an unverifiable promise.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("plead_for_peace")]
        [TestCase("grant_fief")]
        public void GIVEN_blank_or_unrecognised_text_WHEN_parsed_THEN_it_fails(string text)
        {
            OathKindParser.TryParse(text, out CommitmentKind kind).Should().BeFalse();
            kind.Should().Be(default(CommitmentKind));
        }
    }
}
