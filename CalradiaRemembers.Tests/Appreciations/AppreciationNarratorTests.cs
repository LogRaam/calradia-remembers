// Code written by Gabriel Mailhot, 04/07/2026.
// AppreciationNarrator turns an Appreciation's raw numbers (Magnitude, decayed EffectiveMagnitude) into
// the two pieces of prose the mod ships: the encyclopedia's "Kindnesses" sentence and the one-liner
// injected straight into the NPC's LLM prompt. The pillar is the positive mirror of Grudges and shares its
// hard surfacing rule, narrative text, no numbers, ever, but carries its OWN twist: unlike a grudge there
// is no concealment register, an unvoiced appreciation is a quiet warmth, never hidden coldness. A stray
// digit here breaks the numberless-narrative rule; a wrong knownness branch either blurts out a kindness
// the NPC has not earned the right to name, or mutes warmth that should be voiced. These tests pin the
// exact wording and gating so a tweak cannot silently reintroduce either failure.

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
      // Stake: causes are stored as full first-person sentences with their own punctuation, and DisplayLine
      // appends more text after them; the same double-period bug GrudgeNarrator had to fix would show
      // "...unasked.." on the encyclopedia page if the trailing punctuation were not trimmed first.
      [Test]
      public void GIVEN_a_cause_that_ends_in_a_period_WHEN_building_the_display_line_THEN_the_sentence_ends_in_a_single_period()
      {
         Appreciation appreciation = NewAppreciation("The player paid my ransom unasked.");

         string line = AppreciationNarrator.DisplayLine(appreciation, true, null, 20);

         line.Should().EndWith("unasked.");
         line.Should().NotContain("..");
      }

      // Stake: effectiveMagnitude <= 0 must stop the encyclopedia line, or a kindness the design says has
      // fully decayed away would keep showing on the Kindnesses page forever, contradicting decay itself
      // and turning the pillar into the "bookkeeper's ledger" it was explicitly designed not to be.
      [Test]
      public void GIVEN_a_fully_faded_appreciation_WHEN_building_the_display_line_THEN_nothing_is_surfaced()
      {
         Appreciation appreciation = NewAppreciation();

         AppreciationNarrator.DisplayLine(appreciation, true, null, 0).Should().BeNull();
      }

      // Stake: same gate, for the LLM prompt this time; without it a faded kindness would keep colouring
      // the NPC's tone turn after turn long after nothing should be tinting the exchange.
      [Test]
      public void GIVEN_a_fully_faded_appreciation_WHEN_building_the_prompt_line_THEN_nothing_is_surfaced()
      {
         Appreciation appreciation = NewAppreciation();

         AppreciationNarrator.PromptLine(appreciation, true, 0).Should().BeNull();
      }

      // Stake: pins the GenuineDebtCeiling boundary exactly one point past "a genuine debt of gratitude";
      // an off-by-one here would undersell how warmly the NPC actually regards the player in the prompt
      // and on the encyclopedia page, both of which speak only in these qualitative bands, never numbers.
      [Test]
      public void GIVEN_a_high_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_deep_and_lasting_gratitude()
      {
         AppreciationNarrator.WarmthWord(AppreciationNarrator.GenuineDebtCeiling + 1).Should().Be("a deep and lasting gratitude");
      }

      // ── Prompt line: the known-vs-quiet mirror ──────────────────────────

      // Stake: a KNOWN appreciation (the player plainly witnessed the kindness) must be allowed to surface
      // in dialogue, or the NPC would stay inexplicably silent about a debt of gratitude the player already
      // knows exists between them.
      [Test]
      public void GIVEN_a_known_appreciation_WHEN_building_the_prompt_line_THEN_the_npc_may_name_it_openly()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.PromptLine(appreciation, true, 30);

         line.Should().Contain("openly");
         line.Should().Contain(appreciation.CauseSummary);
      }

      // Stake: pins the FleetingCeiling boundary between "a fleeting warmth" and "a small kindness
      // remembered", the band a routine favour will typically land in during a normal playthrough.
      [Test]
      public void GIVEN_a_low_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_small_kindness_remembered()
      {
         AppreciationNarrator.WarmthWord(AppreciationNarrator.FleetingCeiling + 1).Should().Be("a small kindness remembered");
      }

      // Stake: pins the SmallKindnessCeiling boundary between "a small kindness remembered" and "a genuine
      // debt of gratitude"; misplacing it would call a substantial favour a trifle, underselling how much
      // it should warm the NPC's tone.
      [Test]
      public void GIVEN_a_mid_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_genuine_debt_of_gratitude()
      {
         AppreciationNarrator.WarmthWord(AppreciationNarrator.SmallKindnessCeiling + 1).Should().Be("a genuine debt of gratitude");
      }

      // Stake: a stale TargetId (the target hero unresolved after a save, per
      // EncyclopediaHeroPageKindnessesMixin's own tolerance note) must still render as "them" rather than
      // blank text; without this fallback the encyclopedia would show a grammatically broken line.
      [Test]
      public void GIVEN_a_missing_target_name_WHEN_building_the_display_line_THEN_it_falls_back_to_them_rather_than_blank()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.DisplayLine(appreciation, false, "", 20);

         line.Should().Contain("toward them");
      }

      // Stake: DisplayLine is called once per appreciation on every encyclopedia refresh; a throw here
      // (instead of null) would blank the whole Kindnesses section for one bad entry, not just skip it.
      [Test]
      public void GIVEN_a_null_appreciation_WHEN_building_the_display_line_THEN_it_returns_null_not_throw()
      {
         AppreciationNarrator.DisplayLine(null, true, null, 20).Should().BeNull();
      }

      // Stake: PromptLine runs on every chat turn a holder feels a kindness toward the player; a throw
      // here would only be caught by the caller's broad try/catch, silently dropping the entire prompt
      // note for the turn instead of just this one line.
      [Test]
      public void GIVEN_a_null_appreciation_WHEN_building_the_prompt_line_THEN_it_returns_null_not_throw()
      {
         AppreciationNarrator.PromptLine(null, true, 50).Should().BeNull();
      }

      // ── Warmth band words ────────────────────────────────────────────────

      // Stake: pins the floor of the band ladder (magnitude 0), so a barely-formed kindness reads as
      // minor rather than an overstated debt of gratitude.
      [Test]
      public void GIVEN_a_very_low_magnitude_WHEN_naming_the_band_THEN_it_reads_as_a_fleeting_warmth()
      {
         AppreciationNarrator.WarmthWord(0).Should().Be("a fleeting warmth");
      }

      // Stake: pins the pillar's key difference from Grudges (this file's header): an unvoiced appreciation
      // is a quiet warmth, not concealed coldness, so it must still colour the NPC's manner ("warms")
      // while never claiming the NPC may name it ("openly"): the two registers must never cross.
      [Test]
      public void GIVEN_an_unvoiced_appreciation_WHEN_building_the_prompt_line_THEN_it_still_warms_without_being_named()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.PromptLine(appreciation, false, 30);

         line.Should().Contain("warms");
         line.Should().NotContain("openly");
      }

      // Stake: "narrative text, no numbers" is the pillar's ratified surfacing rule, shared with Grudges; a
      // leaked digit on the encyclopedia page would expose the internal Magnitude the qualitative-band
      // design exists to hide from the player.
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

      // Stake: same no-digits rule for the LLM prompt; a leaked number here risks the model parroting it
      // back in dialogue ("my gratitude is 73"), breaking the numberless chronicler voice both surfaces share.
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

      // Stake: an empty CauseSummary (e.g. a backfilled appreciation whose source has no recoverable text)
      // must fall back to "a kindness from before" rather than produce a broken sentence like "toward you,
      // for ." on the player's encyclopedia page.
      [Test]
      public void GIVEN_no_cause_summary_WHEN_building_the_display_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
      {
         Appreciation appreciation = NewAppreciation("");

         string line = AppreciationNarrator.DisplayLine(appreciation, true, null, 20);

         line.Should().Contain("a kindness from before");
      }

      // Stake: same fallback, for the prompt line; an empty cause would otherwise hand the LLM a sentence
      // ending "...for , and..." (a grammar break that risks confusing the model's own output).
      [Test]
      public void GIVEN_no_cause_summary_WHEN_building_the_prompt_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
      {
         Appreciation appreciation = NewAppreciation("");

         string line = AppreciationNarrator.PromptLine(appreciation, true, 30);

         line.Should().NotBeNullOrWhiteSpace();
      }

      // Stake: TargetId is general, not always the player (a live plant can feel a kindness toward another
      // NPC); naming the actual target keeps an NPC-vs-NPC appreciation from misreading as felt toward the
      // player on the player's own encyclopedia screen.
      [Test]
      public void GIVEN_the_appreciation_targets_a_named_npc_WHEN_building_the_display_line_THEN_the_npc_is_named_by_name()
      {
         Appreciation appreciation = NewAppreciation();

         string line = AppreciationNarrator.DisplayLine(appreciation, false, "Derthert", 20);

         line.Should().Contain("toward Derthert");
         line.Should().NotContain("toward you");
      }

      // ── Display line: the encyclopedia's third-person chronicler sentence ──

      // Stake: pins the majority-case branch (most CR appreciations target the player), so it must read
      // naturally in second person rather than the third-person "toward Derthert" form used for others.
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