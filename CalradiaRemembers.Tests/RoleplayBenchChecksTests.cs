// Code written by Gabriel Mailhot, 12/08/2026.

// RoleplayBenchChecks decides the three ADVISORY signals printed at the top of each cr.rp_bench scene: the
// verbatim-repeat count, the refusal/broken-character flag, and the (soft) Earth-reference flag. They are what
// lets a user swapping the roleplay model in MCM rank models at a glance instead of re-reading every transcript.
// A wrong verdict here does not corrupt a save, but it misleads the human judge: a repeat missed, a censoring
// model not flagged at Hardcore (the whole point of scene 2), or a false Earth-reference on Calradia's own
// plural "gods". These tests pin the boundaries so those signals stay trustworthy.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RoleplayBenchChecksTests
   {
      // ── HasVerbatimRepeat ──────────────────────────────────────────────────

      // Verbatim repetition was the exact weak-model failure (glm-5.2). A good model must score 0, so a run of
      // all-distinct NPC lines has to read as zero repeats or the signal would flag every model as looping.
      [Test]
      public void GIVEN_all_distinct_npc_lines_WHEN_counting_repeats_THEN_it_is_zero()
      {
         var lines = new List<string> {"Well met, friend.", "What favour do you ask?", "I will not forget this."};

         RoleplayBenchChecks.HasVerbatimRepeat(lines).Should().Be(0);
      }

      // The signal must ignore case and whitespace: a model that re-emits the same sentence with a stray double
      // space or different capitalisation is still looping, and a stricter compare would let it slip through.
      [Test]
      public void GIVEN_a_line_repeated_with_different_case_and_spacing_WHEN_counting_repeats_THEN_it_counts_as_one()
      {
         var lines = new List<string> {"You have my word.", "you  have my   WORD.", "Farewell."};

         RoleplayBenchChecks.HasVerbatimRepeat(lines).Should().Be(1);
      }

      // Pins that the count is per-later-duplicate, not per-distinct-phrase: three copies of one line are two
      // repeats (the 2nd and 3rd), so a badly looping model reads as worse than a model that repeats once.
      [Test]
      public void GIVEN_the_same_line_three_times_WHEN_counting_repeats_THEN_it_counts_as_two()
      {
         var lines = new List<string> {"Aye.", "Aye.", "Aye."};

         RoleplayBenchChecks.HasVerbatimRepeat(lines).Should().Be(2);
      }

      // Empty and whitespace-only lines are absence-of-output, not repetition: counting them would punish a model
      // that legitimately produced only narration on some turns (scene 2 renders physical action as [NARRATION]).
      [Test]
      public void GIVEN_blank_lines_among_the_npc_lines_WHEN_counting_repeats_THEN_blanks_are_not_repeats()
      {
         var lines = new List<string> {"", "   ", "", "A single spoken line."};

         RoleplayBenchChecks.HasVerbatimRepeat(lines).Should().Be(0);
      }

      // A null list must be tolerated: the runner builds the list from parsed turns and a scene that errored out
      // could hand this nothing. It has to read as zero repeats, never throw and abort the whole bench.
      [Test]
      public void GIVEN_a_null_line_list_WHEN_counting_repeats_THEN_it_is_zero()
      {
         RoleplayBenchChecks.HasVerbatimRepeat(null).Should().Be(0);
      }

      // ── LooksLikeRefusal ───────────────────────────────────────────────────

      // The headline Hardcore signal: an out-of-fiction refusal ("I cannot continue this scene") must be caught
      // regardless of the surrounding sentence, or a censoring model would be mis-ranked as merely poor prose.
      [Test]
      public void GIVEN_an_out_of_fiction_refusal_WHEN_scanning_THEN_it_is_flagged()
      {
         RoleplayBenchChecks.LooksLikeRefusal("I'm sorry, but I cannot continue this scene.").Should().BeTrue();
      }

      // The scan is case-insensitive: a capitalised "As an AI" opener is the same broken-character tell as the
      // lower-case form, and a case-sensitive check would miss the most common phrasing of all.
      [Test]
      public void GIVEN_a_refusal_in_mixed_case_WHEN_scanning_THEN_it_is_still_flagged()
      {
         RoleplayBenchChecks.LooksLikeRefusal("As An AI language model, I must decline.").Should().BeTrue();
      }

      // In-fiction dialogue must NOT trip the flag. A lord saying "I cannot in honour back your claim" is exactly
      // the kind of grounded refusal scene 1 wants; only the out-of-fiction phrases count, so this is the guard
      // that keeps the signal from firing on good roleplay.
      [Test]
      public void GIVEN_an_in_character_reply_WHEN_scanning_THEN_it_is_not_flagged()
      {
         RoleplayBenchChecks.LooksLikeRefusal("I will not lend my voice to so reckless a claim, old friend.").Should().BeFalse();
      }

      // Regression from a REAL bench run: bare "i cannot" / "i won't" used to be on the list and false-flagged
      // these exact in-character lines (glm scene 2 and deepseek scene 1), reporting a censoring/refusing model
      // where there was none. The whole point of the headline signal is to trust it, so these must read as clean.
      [Test]
      public void GIVEN_the_real_false_positive_lines_from_the_log_WHEN_scanning_THEN_they_are_not_flagged()
      {
         RoleplayBenchChecks.LooksLikeRefusal("That night has lived in my blood like a fever I cannot break.").Should().BeFalse();
         RoleplayBenchChecks.LooksLikeRefusal("I won't pledge mine blind, not even for a comrade.").Should().BeFalse();
      }

      // An empty or whitespace reply is handled by the runner as its own "empty/errored" signal; the refusal scan
      // must return false on it rather than throw, so the two signals stay cleanly separate.
      [Test]
      public void GIVEN_empty_text_WHEN_scanning_for_refusal_THEN_it_is_not_flagged()
      {
         RoleplayBenchChecks.LooksLikeRefusal("   ").Should().BeFalse();
      }

      // Every phrase in the published refusal list must actually be detected. If someone adds a phrase to the
      // array but the scan logic diverges, this coverage test fails: it drives the list from the same internal
      // array the production scan reads, so the two can never silently disagree.
      [Test]
      public void GIVEN_each_published_refusal_phrase_WHEN_scanning_THEN_every_one_is_flagged()
      {
         foreach (string phrase in RoleplayBenchChecks.RefusalPhrases)
            RoleplayBenchChecks.LooksLikeRefusal("Preamble text " + phrase + " and more.").Should().BeTrue($"'{phrase}' is a published refusal phrase");
      }

      // ── MentionsEarthReference ─────────────────────────────────────────────

      // The reason the list is conservative: Calradia's own worship is plural "gods", and an NPC swearing "by the
      // gods" is correct in-world. A bare "god" must NOT fire, or the advisory would light up on nearly every
      // devout line and become noise the user learns to ignore.
      [Test]
      public void GIVEN_the_calradian_plural_gods_WHEN_scanning_THEN_it_is_not_flagged()
      {
         RoleplayBenchChecks.MentionsEarthReference("By the gods, you have some nerve.").Should().BeFalse();
      }

      // The word boundary is load-bearing: "roman" is on the list but "romantic" is ordinary roleplay vocabulary.
      // Without the boundary the advisory would fire on romance scenes (scene 2), which is precisely backwards.
      [Test]
      public void GIVEN_the_word_romantic_WHEN_scanning_THEN_roman_does_not_match_inside_it()
      {
         RoleplayBenchChecks.MentionsEarthReference("It was a romantic evening by the hearth.").Should().BeFalse();
      }

      // A real Earth-history slip ("marched on Rome") is exactly what the guideline forbids and what this advisory
      // exists to surface, so a whole-word occurrence has to be caught.
      [Test]
      public void GIVEN_a_real_world_place_as_a_whole_word_WHEN_scanning_THEN_it_is_flagged()
      {
         RoleplayBenchChecks.MentionsEarthReference("His legions once marched on Rome itself.").Should().BeTrue();
      }

      // The two multi-word phrases guard the exception to the bare-"god" rule: "oh my god" is an Earth idiom even
      // though "god" alone is allowed, so the phrase form must be detected while the bare word stays silent.
      [Test]
      public void GIVEN_the_phrase_oh_my_god_WHEN_scanning_THEN_it_is_flagged()
      {
         RoleplayBenchChecks.MentionsEarthReference("Oh my god, you actually did it.").Should().BeTrue();
      }

      // Coverage: every published Earth term must be detectable on its own, so a term added to the array is never
      // dead weight the scan cannot see. Driven from the same internal array the production scan reads.
      [Test]
      public void GIVEN_each_published_earth_term_WHEN_scanning_THEN_every_one_is_flagged()
      {
         foreach (string term in RoleplayBenchChecks.EarthReferenceTerms)
            RoleplayBenchChecks.MentionsEarthReference("Some text " + term + " and more.").Should().BeTrue($"'{term}' is a published Earth-reference term");
      }
   }
}
