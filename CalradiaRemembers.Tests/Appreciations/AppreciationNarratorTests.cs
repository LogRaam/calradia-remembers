// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System.Text.RegularExpressions;
using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
   [TestFixture]
   public sealed class AppreciationNarratorTests
   {
      [Test]
      public void GIVEN_a_cause_that_ends_in_a_period_WHEN_building_the_display_line_THEN_the_sentence_ends_in_a_single_period()
      {
         Appreciation appreciation = NewAppreciation("The player paid my ransom unasked.");

         string line = AppreciationNarrator.DisplayLine(appreciation, true, null, 20);

         line.Should().EndWith("unasked.");
         line.Should().NotContain("..");
      }

      [Test]
      public void GIVEN_a_fully_faded_appreciation_WHEN_building_the_display_line_THEN_nothing_is_surfaced()
      {
         Appreciation appreciation = NewAppreciation();

         AppreciationNarrator.DisplayLine(appreciation, true, null, 0).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_fully_faded_appreciation_WHEN_building_the_prompt_line_THEN_nothing_is_surfaced()
      {
         Appreciation appreciation = NewAppreciation();

         AppreciationNarrator.PromptLine(appreciation, true, 0).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_high_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_deep_and_lasting_gratitude()
      {
         AppreciationNarrator.WarmthWord(AppreciationNarrator.GenuineDebtCeiling + 1).Should().Be("a deep and lasting gratitude");
      }

      // ── Prompt line: the known-vs-quiet mirror ──────────────────────────

      [Test]
      public void GIVEN_a_known_appreciation_WHEN_building_the_prompt_line_THEN_the_npc_may_name_it_openly()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.PromptLine(appreciation, true, 30);

         line.Should().Contain("openly");
         line.Should().Contain(appreciation.CauseSummary);
      }

      [Test]
      public void GIVEN_a_low_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_small_kindness_remembered()
      {
         AppreciationNarrator.WarmthWord(AppreciationNarrator.FleetingCeiling + 1).Should().Be("a small kindness remembered");
      }

      [Test]
      public void GIVEN_a_mid_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_genuine_debt_of_gratitude()
      {
         AppreciationNarrator.WarmthWord(AppreciationNarrator.SmallKindnessCeiling + 1).Should().Be("a genuine debt of gratitude");
      }

      [Test]
      public void GIVEN_a_missing_target_name_WHEN_building_the_display_line_THEN_it_falls_back_to_them_rather_than_blank()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.DisplayLine(appreciation, false, "", 20);

         line.Should().Contain("toward them");
      }

      [Test]
      public void GIVEN_a_null_appreciation_WHEN_building_the_display_line_THEN_it_returns_null_not_throw()
      {
         AppreciationNarrator.DisplayLine(null, true, null, 20).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_null_appreciation_WHEN_building_the_prompt_line_THEN_it_returns_null_not_throw()
      {
         AppreciationNarrator.PromptLine(null, true, 50).Should().BeNull();
      }

      // ── Warmth band words ────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_very_low_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_fleeting_warmth()
      {
         AppreciationNarrator.WarmthWord(0).Should().Be("a fleeting warmth");
      }

      [Test]
      public void GIVEN_an_unvoiced_appreciation_WHEN_building_the_prompt_line_THEN_it_still_warms_without_being_named()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.PromptLine(appreciation, false, 30);

         line.Should().Contain("warms");
         line.Should().NotContain("openly");
      }

      [Test]
      public void GIVEN_every_warmth_band_WHEN_building_the_display_line_THEN_no_digits_ever_appear_in_the_generated_text()
      {
         Appreciation appreciation = NewAppreciation();

         foreach (int magnitude in new[] {1, 15, 40, 80, 100})
         {
            string line = AppreciationNarrator.DisplayLine(appreciation, true, null, magnitude);

            Regex.IsMatch(line, @"\d").Should().BeFalse($"'{line}' must carry no digits");
         }
      }

      [Test]
      public void GIVEN_every_warmth_band_WHEN_building_the_prompt_line_THEN_no_digits_ever_appear_in_the_generated_text()
      {
         Appreciation appreciation = NewAppreciation();

         foreach (int magnitude in new[] {1, 15, 40, 80, 100})
         {
            string known = AppreciationNarrator.PromptLine(appreciation, true, magnitude);
            string quiet = AppreciationNarrator.PromptLine(appreciation, false, magnitude);

            Regex.IsMatch(known, @"\d").Should().BeFalse($"'{known}' must carry no digits");
            Regex.IsMatch(quiet, @"\d").Should().BeFalse($"'{quiet}' must carry no digits");
         }
      }

      [Test]
      public void GIVEN_no_cause_summary_WHEN_building_the_display_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
      {
         Appreciation appreciation = NewAppreciation("");

         string line = AppreciationNarrator.DisplayLine(appreciation, true, null, 20);

         line.Should().Contain("a kindness from before");
      }

      [Test]
      public void GIVEN_no_cause_summary_WHEN_building_the_prompt_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
      {
         Appreciation appreciation = NewAppreciation("");

         string line = AppreciationNarrator.PromptLine(appreciation, true, 30);

         line.Should().NotBeNullOrWhiteSpace();
      }

      [Test]
      public void GIVEN_the_appreciation_targets_a_named_npc_WHEN_building_the_display_line_THEN_the_npc_is_named_by_name()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.DisplayLine(appreciation, false, "Derthert", 20);

         line.Should().Contain("toward Derthert");
         line.Should().NotContain("toward you");
      }

      // ── Display line: the encyclopedia's third-person chronicler sentence ──

      [Test]
      public void GIVEN_the_appreciation_targets_the_player_WHEN_building_the_display_line_THEN_the_player_is_named_you()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.DisplayLine(appreciation, true, null, 20);

         line.Should().Contain("toward you");
         line.Should().Contain(appreciation.CauseSummary);
      }

      #region private

      private static Appreciation NewAppreciation(string cause = "you paid my ransom without being asked twice")
         => new() {
            Id = "holder|2|100",
            HolderId = "holder",
            TargetId = "player",
            Source = AppreciationSource.RansomedOrFreed,
            CauseSummary = cause,
            EventDay = 100,
            Magnitude = 50
         };

      #endregion
   }
}