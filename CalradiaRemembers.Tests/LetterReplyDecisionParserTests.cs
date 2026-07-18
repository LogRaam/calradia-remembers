// Code written by Gabriel Mailhot, 17/07/2026.
// This parser turns an LLM's terse control line into the decision "does this NPC answer the player's letter, and
// when". It is the ONLY thing standing between a weak model's loosely-formatted output and a dispatched courier,
// so each branch here maps to a concrete failure the parser must prevent: a reply mis-read as a pass leaves the
// player's letter forever unanswered; a pass mis-read as a reply dispatches a blank courier; an out-of-range delay
// parks (or races) the courier on a nonsense schedule. The overriding rule the tests pin is TOLERANCE: anything
// that is not unmistakably a pass must still reach the player as a letter, because a model that ignored the format
// should never cost the player a reply.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterReplyDecisionParserTests
    {
        // The happy PASS path: an NPC who genuinely declines must produce NO courier. Read this as a reply and a
        // blank letter is sent from someone who chose silence.
        [Test]
        public void GIVEN_a_PASS_line_WHEN_parsing_THEN_the_npc_does_not_reply()
        {
            LetterReplyDecision d = LetterReplyDecisionParser.Parse("PASS: too busy campaigning to answer");

            d.Kind.Should().Be(LetterReplyKind.Pass);
            d.PassReason.Should().Be("too busy campaigning to answer");
        }

        // The reason is logged, not shown, but it must survive parsing so the log explains WHY an expected reply
        // never came: a pass with a lost reason turns a deliberate silence into an indistinguishable bug report.
        [Test]
        public void GIVEN_a_bare_PASS_with_no_reason_WHEN_parsing_THEN_it_still_passes_with_an_empty_reason()
        {
            LetterReplyDecision d = LetterReplyDecisionParser.Parse("PASS");

            d.Kind.Should().Be(LetterReplyKind.Pass);
            d.PassReason.Should().BeEmpty();
        }

        // Weak models answer the yes/no question in plain words instead of the token; "no" and "no reply" must
        // count as a pass, or a one-word refusal is mistaken for a one-word letter.
        [TestCase("no")]
        [TestCase("No Reply")]
        public void GIVEN_a_plain_language_refusal_WHEN_parsing_THEN_it_passes(string raw)
        {
            LetterReplyDecisionParser.Parse(raw).Kind.Should().Be(LetterReplyKind.Pass);
        }

        // The core tolerance guarantee: a bare letter body with no control line at all is the SHAPE a model that
        // ignored the format returns, and it must be delivered as a reply on the default delay, never dropped.
        [Test]
        public void GIVEN_a_bare_letter_body_WHEN_parsing_THEN_it_is_a_reply_on_the_default_delay()
        {
            LetterReplyDecision d = LetterReplyDecisionParser.Parse("My dearest, your letter reached me at Sargot.");

            d.Kind.Should().Be(LetterReplyKind.Reply);
            d.DelayDays.Should().Be(LetterReplyDecisionParser.DefaultDelayDays);
            d.Body.Should().Be("My dearest, your letter reached me at Sargot.");
        }

        // The whole point of the DELAY line is to let the NPC answer after a believable interval; the number must
        // be honoured and the control line must NOT leak into the delivered letter body.
        [Test]
        public void GIVEN_a_DELAY_line_then_a_body_WHEN_parsing_THEN_the_delay_is_read_and_stripped_from_the_body()
        {
            LetterReplyDecision d = LetterReplyDecisionParser.Parse("DELAY:5\nI have thought long on your words.");

            d.Kind.Should().Be(LetterReplyKind.Reply);
            d.DelayDays.Should().Be(5);
            d.Body.Should().Be("I have thought long on your words.");
        }

        // A courier cannot depart in the past or the same instant, and a stray large number must not park a reply
        // for months: an out-of-range delay falls back to the default rather than being trusted blindly.
        [TestCase("DELAY:0\nbody", TestName = "below floor falls back to default")]
        [TestCase("DELAY:99\nbody", TestName = "above ceiling falls back to default")]
        [TestCase("DELAY:soon\nbody", TestName = "non-numeric falls back to default")]
        public void GIVEN_an_out_of_range_or_garbled_delay_WHEN_parsing_THEN_it_uses_the_default_delay(string raw)
        {
            LetterReplyDecision d = LetterReplyDecisionParser.Parse(raw);

            d.Kind.Should().Be(LetterReplyKind.Reply);
            d.DelayDays.Should().Be(LetterReplyDecisionParser.DefaultDelayDays);
            d.Body.Should().Be("body");
        }

        // A DELAY line with nothing after it is a reply with no letter: the host reads an empty body as "nothing to
        // send" and dispatches no courier. It must NOT surface as a pass (the reason log would lie) nor as a blank
        // delivered letter.
        [Test]
        public void GIVEN_a_DELAY_line_with_no_body_WHEN_parsing_THEN_it_is_a_reply_with_an_empty_body()
        {
            LetterReplyDecision d = LetterReplyDecisionParser.Parse("DELAY:3");

            d.Kind.Should().Be(LetterReplyKind.Reply);
            d.Body.Should().BeEmpty();
        }

        // The DELAY boundaries are INCLUSIVE: the exact min and max are legitimate schedules the NPC may pick, so
        // clamping must not reject the very endpoints it advertises.
        [TestCase("DELAY:1\nbody", 1)]
        [TestCase("DELAY:14\nbody", 14)]
        public void GIVEN_a_delay_exactly_on_a_boundary_WHEN_parsing_THEN_it_is_accepted(string raw, int expected)
        {
            LetterReplyDecisionParser.Parse(raw).DelayDays.Should().Be(expected);
        }

        // Degrades to a pass rather than throwing: the caller guards null already, but a parser that crashed on
        // empty input would turn one bad LLM turn into a swallowed exception that aborts the surrounding tick.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GIVEN_empty_or_null_input_WHEN_parsing_THEN_it_passes_without_throwing(string raw)
        {
            LetterReplyDecisionParser.Parse(raw).Kind.Should().Be(LetterReplyKind.Pass);
        }
    }
}
