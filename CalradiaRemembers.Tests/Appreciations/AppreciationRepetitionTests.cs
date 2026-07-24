// Code written by Gabriel Mailhot, 22/07/2026.
// Player-reported (screenshot, 2026-07-22): Corein's encyclopedia showed the SAME kindness three times,
// "Carries a small kindness remembered toward you, for A tender moment we shared." repeated verbatim.
// It was not a rendering fault. The ledger's only dedup was same-holder + same-source + same DAY (on both
// the live path and the backfill), and the cause text for a tender moment is a hard-coded constant, so three
// intimate moments on three different days became three byte-identical records. That is worse than ugly:
// SelectDominant truncates to PerNpcCap (3), so the clones filled the NPC's entire kindness budget and
// crowded out genuinely different deeds (a gift, a rescue), and ResolveAppreciationNote emits one sentence
// per live appreciation, so the model was told the same fact three times and weighted it accordingly.
// The fix COUNTS instead of discarding: a repeat deepens the record it already has (renewing EventDay, since
// decay runs from that day), and the count both reads in the prose and WEIGHS in the magnitude. These tests
// pin the three halves of that: the diminishing capped bonus (so a repeatable source can never become a
// farm), the numberless prose, and the one-shot consolidation that must silently repair saves that already
// contain the twins.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
   [TestFixture]
   public sealed class AppreciationRepetitionTests
   {
      private const string Cause = "A tender moment we shared.";

      private static Appreciation Twin(int day, int occurrences = 0, int magnitude = 30) => new() {
         HolderId = "npc_corein",
         TargetId = "main_hero",
         Source = AppreciationSource.KindnessInScene,
         CauseSummary = Cause,
         EventDay = day,
         Magnitude = magnitude,
         KnownToPlayer = true,
         Occurrences = occurrences
      };

      // ── The count itself ──────────────────────────────────────────────────

      // Every record saved before the field existed deserializes to 0, and the whole ledger would read as
      // "zero occurrences" if any caller trusted the raw value: a kindness that happened once would then
      // score as if it had never happened at all.
      [Test]
      public void GIVEN_a_record_saved_before_the_field_existed_WHEN_counting_THEN_it_reads_as_having_happened_once()
      {
         AppreciationPolicy.OccurrenceCount(Twin(day: 10, occurrences: 0)).Should().Be(1);
      }

      // The owner's call (2026-07-22): counting is better when it actually weighs, not merely when it shows.
      // A second occasion has to move the number, or the count is decoration.
      [Test]
      public void GIVEN_a_kindness_done_twice_WHEN_weighing_it_THEN_it_is_felt_more_strongly_than_once()
      {
         AppreciationPolicy.RepeatMagnitudeBonus(2).Should().BeGreaterThan(AppreciationPolicy.RepeatMagnitudeBonus(1));
      }

      // The guardrail on that call. A flat per-repeat bonus would turn any repeatable source into a farm:
      // one relationship's single repeated kindness would outrank every other NPC's real deeds, and since
      // magnitude drives both survival at the cap and tarnishing by grudges, it would quietly take over the
      // holder's whole ledger. The series must diminish and saturate.
      [Test]
      public void GIVEN_a_kindness_repeated_again_and_again_WHEN_weighing_it_THEN_each_repeat_adds_less_than_the_one_before()
      {
         int second = AppreciationPolicy.RepeatMagnitudeBonus(2) - AppreciationPolicy.RepeatMagnitudeBonus(1);
         int third = AppreciationPolicy.RepeatMagnitudeBonus(3) - AppreciationPolicy.RepeatMagnitudeBonus(2);
         int fourth = AppreciationPolicy.RepeatMagnitudeBonus(4) - AppreciationPolicy.RepeatMagnitudeBonus(3);

         third.Should().BeLessThan(second);
         fourth.Should().BeLessThan(third);
      }

      // The hard ceiling behind that curve: however many times a kindness is repeated, the total it can add
      // is bounded, so no amount of grinding one deed can ever reach the top warmth band on repetition alone.
      [Test]
      public void GIVEN_an_absurd_number_of_repeats_WHEN_weighing_it_THEN_the_bonus_stops_at_the_cap()
      {
         AppreciationPolicy.RepeatMagnitudeBonus(500).Should().Be(AppreciationPolicy.MaxRepeatBonus);
      }

      // The bonus is folded into the BASE before decay, not added after it, which is what makes a repeated
      // kindness take longer to fade to nothing as well as start warmer. Applied after decay it would be a
      // flat cosmetic offset that vanished at the same moment the original did.
      [Test]
      public void GIVEN_two_identical_kindnesses_one_repeated_WHEN_read_much_later_THEN_the_repeated_one_still_lingers_warmer()
      {
         Appreciation once = Twin(day: 0, occurrences: 1);
         Appreciation thrice = Twin(day: 0, occurrences: 3);

         int onceLater = AppreciationPolicy.EffectiveMagnitude(once, today: 60, traits: null);
         int thriceLater = AppreciationPolicy.EffectiveMagnitude(thrice, today: 60, traits: null);

         thriceLater.Should().BeGreaterThan(onceLater);
      }

      // ── The prose ─────────────────────────────────────────────────────────

      // This pillar's stated discipline (AppreciationNarrator's own header) is that the encyclopedia and the
      // prompt speak in words, never digits. The count must not be the one place a numeral leaks in.
      [Test]
      public void GIVEN_a_kindness_done_three_times_WHEN_written_THEN_the_count_is_spelled_in_words_not_digits()
      {
         string line = AppreciationNarrator.DisplayLine(Twin(day: 10, occurrences: 3), true, null, 30);

         line.Should().Contain("three times over");
         line.Should().NotContain("3");
      }

      // A single kindness must read exactly as it always did: the whole feature has to be invisible until
      // something is genuinely repeated, or every ordinary line in the game gains noise.
      [Test]
      public void GIVEN_a_kindness_done_once_WHEN_written_THEN_no_repetition_clause_appears()
      {
         AppreciationNarrator.RepetitionClause(1).Should().BeEmpty();
      }

      // Past a handful, the exact tally stops being something a person tracks, and printing "eleven times
      // over" would read as bookkeeping rather than memory.
      [Test]
      public void GIVEN_a_kindness_repeated_beyond_counting_WHEN_written_THEN_it_reads_as_many_rather_than_a_tally()
      {
         AppreciationNarrator.RepetitionClause(AppreciationNarrator.ManyTimesThreshold + 1).Should().Contain("many times");
      }

      // Straight from the player's screenshot: "for A tender moment we shared". Causes are authored as
      // complete first-person sentences because they double as prompt text, so pasting one after "for" put a
      // capital letter in the middle of a sentence, in plain view on the encyclopedia page.
      [Test]
      public void GIVEN_a_cause_stored_as_its_own_sentence_WHEN_embedded_mid_sentence_THEN_its_leading_capital_is_lowered()
      {
         string line = AppreciationNarrator.DisplayLine(Twin(day: 10, occurrences: 1), true, null, 30);

         line.Should().Contain("for a tender moment we shared");
         line.Should().NotContain("for A tender moment");
      }

      // The boundary that keeps that fix safe. Lowering the first letter is only ever correct for words that
      // cannot be a name: turning a cause that opens on someone's name into "gabriel's men freed me" would
      // be a worse and more visible bug than the one being fixed.
      [Test]
      public void GIVEN_a_cause_that_opens_on_a_name_WHEN_embedded_mid_sentence_THEN_the_name_keeps_its_capital()
      {
         Appreciation a = Twin(day: 10, occurrences: 1);
         a.CauseSummary = "Gabriel's men pulled me from the river.";

         AppreciationNarrator.DisplayLine(a, true, null, 30).Should().Contain("for Gabriel's men");
      }

      // ── Repairing saves that already hold the twins ───────────────────────

      // The save-migration hard rule: the reporting player's campaign ALREADY contains three identical
      // records, so a fix that only prevents new ones would leave their encyclopedia exactly as broken.
      // Nothing may be lost in the repair, which is the whole reason for counting rather than deleting.
      [Test]
      public void GIVEN_a_save_holding_three_identical_records_WHEN_consolidated_THEN_one_remains_carrying_the_full_tally()
      {
         var ledger = new List<Appreciation> {Twin(day: 10), Twin(day: 25), Twin(day: 40)};

         List<Appreciation> merged = AppreciationMigration.ConsolidateRepeats(ledger);

         merged.Should().HaveCount(1);
         AppreciationPolicy.OccurrenceCount(merged[0]).Should().Be(3);
      }

      // The survivor anchors on the LATEST occasion, because decay runs from EventDay: anchoring on the
      // oldest would have the merged memory fade as though the two later moments had never happened, which
      // would make the repair a downgrade for the player.
      [Test]
      public void GIVEN_twins_across_several_days_WHEN_consolidated_THEN_the_warmth_is_felt_from_the_most_recent_one()
      {
         List<Appreciation> merged = AppreciationMigration.ConsolidateRepeats(
            new List<Appreciation> {Twin(day: 10), Twin(day: 40), Twin(day: 25)});

         merged[0].EventDay.Should().Be(40);
      }

      // Different causes are different memories. If consolidation keyed on holder and source alone, a gift
      // and a rescue would be silently welded into one line, destroying real history rather than repairing
      // duplicated history.
      [Test]
      public void GIVEN_two_different_kindnesses_from_the_same_source_WHEN_consolidated_THEN_both_survive_untouched()
      {
         Appreciation other = Twin(day: 20);
         other.CauseSummary = "You sat with me through the fever.";

         AppreciationMigration.ConsolidateRepeats(new List<Appreciation> {Twin(day: 10), other}).Should().HaveCount(2);
      }

      // Consolidation runs on every session launch that finds the ledger behind the current version, so a
      // second pass over its own output must change nothing. If it re-counted, a player's tally would climb
      // by itself every time they loaded the game.
      [Test]
      public void GIVEN_an_already_consolidated_ledger_WHEN_consolidated_again_THEN_the_tally_does_not_climb()
      {
         List<Appreciation> once = AppreciationMigration.ConsolidateRepeats(
            new List<Appreciation> {Twin(day: 10), Twin(day: 25), Twin(day: 40)});

         List<Appreciation> twice = AppreciationMigration.ConsolidateRepeats(once);

         twice.Should().HaveCount(1);
         AppreciationPolicy.OccurrenceCount(twice[0]).Should().Be(3);
      }
   }
}
