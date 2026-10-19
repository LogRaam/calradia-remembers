// Code written by Gabriel Mailhot, 29/08/2026.
// Player report: a non-active witness gives a wordless gesture ("*smirk deepens, remains silent, fingers
// drumming*") and the active NPC's Sprint 15C auto-reply then treats it as being blown off, escalating
// against a silence the same model just invented. This classifier is what the 15C gate consults to decide
// whether a witness reaction actually SPOKE; a wrong answer here either lets a pure gesture keep driving
// the auto-reply loop (the bug persists) or silences a witness who genuinely spoke (a real reply vanishes).
// Each test names the case it pins.

#region

using CalradiaRemembers.Logic.Witnesses;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WitnessSpeechDetectorTests
   {
      // The exact bug string from the player report: one stage-direction span and nothing else. This must
      // never again be able to drive the 15C auto counter-reply.
      [Test]
      public void GIVEN_the_reported_bug_string_WHEN_checking_THEN_it_has_no_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("*smirk deepens, remains silent, fingers drumming*")
            .Should().BeFalse();
      }

      // The general shape of the bug: any single gesture-only span, not just the reported wording.
      [Test]
      public void GIVEN_a_gesture_only_reaction_WHEN_checking_THEN_it_has_no_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("*rolls her eyes*").Should().BeFalse();
      }

      // A bare quoted line with no asterisks at all is unambiguously spoken.
      [Test]
      public void GIVEN_a_quoted_line_WHEN_checking_THEN_it_has_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("\"Careful with that.\"").Should().BeTrue();
      }

      // The prompt explicitly allows a gesture to ACCOMPANY words ("a gesture may accompany words, never
      // replace them"): stripping the gesture must still leave the quoted line behind as spoken.
      [Test]
      public void GIVEN_a_gesture_plus_a_quoted_line_WHEN_checking_THEN_it_has_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("*smirks* \"Careful with that.\"").Should().BeTrue();
      }

      // Plain prose with no asterisk framing at all is treated as spoken (there is no stage-direction
      // marker to strip, so the whole line is the witness's own words).
      [Test]
      public void GIVEN_plain_prose_with_no_asterisks_WHEN_checking_THEN_it_has_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("That is not what I heard at all.").Should().BeTrue();
      }

      // Null and whitespace are never a reaction worth checking; both read as gesture-only (nothing said).
      [TestCase(null)]
      [TestCase("")]
      [TestCase("   ")]
      public void GIVEN_null_or_whitespace_WHEN_checking_THEN_it_has_no_spoken_words(string text)
      {
         WitnessSpeechDetector.HasSpokenWords(text).Should().BeFalse();
      }

      // Several separate gesture spans on one line, still with nothing spoken between or around them, must
      // not be misread as words once concatenated by stripping.
      [Test]
      public void GIVEN_multiple_gesture_spans_and_nothing_else_WHEN_checking_THEN_it_has_no_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("*smirks* *taps fingers on the table*").Should().BeFalse();
      }

      // Conservative on purpose: a malformed reaction (a stray unmatched '*') must never be misclassified as
      // silent when real words surround it, since that would silence a witness who genuinely spoke.
      [Test]
      public void GIVEN_a_stray_unmatched_asterisk_around_real_words_WHEN_checking_THEN_it_has_spoken_words()
      {
         WitnessSpeechDetector.HasSpokenWords("*narrows her eyes and says \"you lie\"").Should().BeTrue();
      }
   }
}
