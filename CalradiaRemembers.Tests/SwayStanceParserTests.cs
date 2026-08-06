// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 resilience: sway_opinion moves an NPC's regard for a named THIRD PARTY at the player's word. It used to
// read its stance as "against = anything that is not the literal word 'for'", so a malformed, blank, or
// hallucinated stance token silently became the HARMFUL direction: a -6 relation hit, a Rivalry rumour, and a
// grudge exposure aimed at someone the model never clearly meant to turn the NPC against. This parser is the
// strict whitelist that replaces that default-to-harm: only a clear "for"/"against" is honoured, everything else
// is Unrecognized so the verb REFUSES. Get it wrong and a garbled token wounds an innocent third party, or a real
// "against" is dropped and the persuasion silently does nothing.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SwayStanceParserTests
   {
      // The plain "for" case must resolve to the WARM direction: a genuine sway toward the target is a kindness,
      // and reading it as anything else would either drop it or, worse, invert it into a relation hit.
      [Test]
      public void GIVEN_the_word_for_WHEN_parsing_THEN_it_is_For()
      {
         SwayStanceParser.Parse("for").Should().Be(SwayStance.For);
      }

      // The plain "against" case must resolve to the HARMFUL direction so a legitimately-turned opinion still
      // lands its -6 hit and Rivalry rumour. This is the ONLY input that may ever produce harm.
      [Test]
      public void GIVEN_the_word_against_WHEN_parsing_THEN_it_is_Against()
      {
         SwayStanceParser.Parse("against").Should().Be(SwayStance.Against);
      }

      // The LLM does not emit a fixed case; " AGAINST " with padding and capitals is the same instruction. A
      // strict-but-case-sensitive parser would misread it as garbage and refuse a real sway, so trimming and
      // case-folding are part of the contract, not leniency toward genuinely malformed tokens.
      [Test]
      public void GIVEN_mixed_case_and_surrounding_whitespace_WHEN_parsing_THEN_the_stance_is_recognized()
      {
         SwayStanceParser.Parse("  FoR ").Should().Be(SwayStance.For);
         SwayStanceParser.Parse(" AGAINST ").Should().Be(SwayStance.Against);
      }

      // The whole point of finding 2: an ABSENT or BLANK stance must NOT default to "against". A missing token is
      // the model failing to name a direction, which must refuse, never silently turn the NPC against the target.
      [Test]
      public void GIVEN_a_blank_or_null_stance_WHEN_parsing_THEN_it_is_Unrecognized()
      {
         SwayStanceParser.Parse(null).Should().Be(SwayStance.Unrecognized);
         SwayStanceParser.Parse("").Should().Be(SwayStance.Unrecognized);
         SwayStanceParser.Parse("   ").Should().Be(SwayStance.Unrecognized);
      }

      // Any value that is not clearly "for"/"against" (a hallucinated synonym, a stray phrase, a typo) must also
      // refuse rather than fall through to harm. "for the record" is the trap the old !equals("for") check would
      // still have read as "against", wounding a third party on a token that was never a stance at all.
      [Test]
      public void GIVEN_a_garbage_or_near_miss_token_WHEN_parsing_THEN_it_is_Unrecognized()
      {
         SwayStanceParser.Parse("maybe").Should().Be(SwayStance.Unrecognized);
         SwayStanceParser.Parse("pour").Should().Be(SwayStance.Unrecognized);
         SwayStanceParser.Parse("for the record").Should().Be(SwayStance.Unrecognized);
         SwayStanceParser.Parse("againstt").Should().Be(SwayStance.Unrecognized);
      }
   }
}
