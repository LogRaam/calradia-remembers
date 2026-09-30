// Code written by Gabriel Mailhot, 30/09/2026.
// Self-esteem, increment E1 (ROADMAP, SELF-ESTEEM, ruled 30/09/2026). Lordfadooboo (Nexus, 29/09/2026): "a lord who
// has been humbled regularly might have it lowered, and one who is doing well might have it higher. starting self
// esteem could be affected by station as well... randomized by character... vanilla personality traits might also
// play a part... determining whether a character is easier to make feel worse or... better." Maifailun: "regressing to
// traumas (too long captive wouldn't be so bold when talking)". If these rules are wrong, every lord carries himself
// alike whatever befell him, or one defeat breaks a man for good, or a king and a hedge knight start in the same place.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Mood;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SelfEsteemPolicyTests
    {
        private static SelfEsteemTemper Plain() => new SelfEsteemTemper();

        // ── Where a man starts ─────────────────────────────────────────────────────────────────────

        // "Starting self esteem could be affected by station": a ruler starts above a man of no house.
        [Test]
        public void GIVEN_a_ruler_and_a_man_of_no_house_alike_otherwise_WHEN_their_baselines_are_set_THEN_the_ruler_starts_higher()
        {
            int ruler = SelfEsteemPolicy.Baseline(new SelfEsteemBaselineFacts {Station = 3, PersonalDraw = 0});
            int nobody = SelfEsteemPolicy.Baseline(new SelfEsteemBaselineFacts {Station = 0, PersonalDraw = 0});

            ruler.Should().BeGreaterThan(nobody);
        }

        // "Randomized by character": two men of one station need not start alike, and one man always starts the same.
        [Test]
        public void GIVEN_two_men_of_one_station_WHEN_their_personal_draws_differ_THEN_they_start_apart_and_each_is_stable()
        {
            SelfEsteemPolicy.PersonalDraw("lord_1_1").Should().Be(SelfEsteemPolicy.PersonalDraw("lord_1_1"));
            SelfEsteemPolicy.PersonalDraw("lord_1_1").Should().BeInRange(-SelfEsteemPolicy.PersonalDrawRange, SelfEsteemPolicy.PersonalDrawRange);
        }

        // Traits play a part: a valorous man carries himself higher than a timid one.
        [Test]
        public void GIVEN_a_valorous_and_a_timid_man_of_one_station_WHEN_set_THEN_the_valorous_starts_higher()
        {
            SelfEsteemPolicy.Baseline(new SelfEsteemBaselineFacts {Station = 1, Valor = 2})
                            .Should().BeGreaterThan(SelfEsteemPolicy.Baseline(new SelfEsteemBaselineFacts {Station = 1, Valor = -2}));
        }

        // ── What moves it ──────────────────────────────────────────────────────────────────────────

        // "A lord who has been humbled regularly might have it lowered, and one who is doing well higher."
        [Test]
        public void GIVEN_a_lost_battle_and_a_won_one_WHEN_recorded_THEN_the_loss_lowers_and_the_win_raises()
        {
            SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleLost, Plain(), 0).Should().BeNegative();
            SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleWon, Plain(), 0).Should().BePositive();
        }

        // Maifailun's trauma: each captivity in recent memory makes the next one land harder.
        [Test]
        public void GIVEN_a_man_captured_again_and_again_WHEN_captured_once_more_THEN_it_lands_harder_than_the_first_time()
        {
            int first = SelfEsteemPolicy.Delta(SelfEsteemEvent.Captured, Plain(), 0);
            int third = SelfEsteemPolicy.Delta(SelfEsteemEvent.Captured, Plain(), 2);

            third.Should().BeLessThan(first);
        }

        // "Whether a character is easier to make feel worse": a valorous man takes a defeat less to heart.
        [Test]
        public void GIVEN_a_valorous_man_WHEN_he_loses_a_battle_THEN_it_costs_him_less_than_a_timid_one()
        {
            int brave = SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleLost, new SelfEsteemTemper {Valor = 2}, 0);
            int timid = SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleLost, new SelfEsteemTemper {Valor = -2}, 0);

            brave.Should().BeGreaterThan(timid);
        }

        // A calculating mind swings less, both ways.
        [Test]
        public void GIVEN_a_calculating_man_WHEN_he_wins_or_loses_THEN_he_swings_less_than_a_plain_one()
        {
            var cold = new SelfEsteemTemper {Calculating = 2};

            SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleWon, cold, 0).Should().BeLessThan(SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleWon, Plain(), 0));
            SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleLost, cold, 0).Should().BeGreaterThan(SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleLost, Plain(), 0));
        }

        // ── Recovery ───────────────────────────────────────────────────────────────────────────────

        // Time mends: a man knocked down drifts back toward himself, and one defeat does not break him for good.
        [Test]
        public void GIVEN_a_shift_left_alone_WHEN_time_passes_THEN_it_drifts_back_toward_nothing_and_never_past_it()
        {
            SelfEsteemPolicy.Decay(-20, 0).Should().Be(-20);
            SelfEsteemPolicy.Decay(-20, SelfEsteemPolicy.RecoveryDaysPerPoint * 5).Should().Be(-15);
            SelfEsteemPolicy.Decay(-20, 10000).Should().Be(0);
            SelfEsteemPolicy.Decay(12, SelfEsteemPolicy.RecoveryDaysPerPoint * 20).Should().Be(0);
        }

        // Recording an event first lets the old shift mend, then adds the new one, and remembers a captivity.
        [Test]
        public void GIVEN_an_old_shift_and_a_new_captivity_WHEN_recorded_THEN_the_old_mends_the_new_lands_and_the_captivity_is_remembered()
        {
            var state = new SelfEsteemState {Shift = -10, ShiftDay = 100, CapturedOnDays = new List<int>()};

            SelfEsteemState after = SelfEsteemPolicy.Record(state, SelfEsteemEvent.Captured, Plain(), 100 + SelfEsteemPolicy.RecoveryDaysPerPoint * 4);

            after.Shift.Should().Be(-6 + SelfEsteemPolicy.Delta(SelfEsteemEvent.Captured, Plain(), 0));
            after.CapturedOnDays.Should().HaveCount(1);
        }

        // An old captivity fades from the count that makes the next one worse.
        [Test]
        public void GIVEN_a_captivity_long_past_WHEN_the_next_is_recorded_THEN_the_old_one_no_longer_compounds_it()
        {
            var state = new SelfEsteemState {CapturedOnDays = new List<int> {10}};

            SelfEsteemPolicy.Record(state, SelfEsteemEvent.Captured, Plain(), 10 + SelfEsteemPolicy.TraumaMemoryDays + 1)
                            .CapturedOnDays.Should().HaveCount(1);
        }

        // ── Bands, never numbers ───────────────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_values_across_the_range_WHEN_banded_THEN_each_reads_as_its_word()
        {
            SelfEsteemPolicy.BandOf(-70).Should().Be(SelfEsteemBand.Broken);
            SelfEsteemPolicy.BandOf(-25).Should().Be(SelfEsteemBand.Shaken);
            SelfEsteemPolicy.BandOf(0).Should().Be(SelfEsteemBand.Steady);
            SelfEsteemPolicy.BandOf(25).Should().Be(SelfEsteemBand.Assured);
            SelfEsteemPolicy.BandOf(70).Should().Be(SelfEsteemBand.Proud);
        }

        // The whole reading: a man of no house, captured three times lately, is shaken or worse, not steady.
        [Test]
        public void GIVEN_a_man_of_no_house_captured_three_times_lately_WHEN_read_THEN_he_is_not_steady()
        {
            var state = new SelfEsteemState {CapturedOnDays = new List<int>()};
            for (int day = 100; day <= 120; day += 10) state = SelfEsteemPolicy.Record(state, SelfEsteemEvent.Captured, Plain(), day);

            int baseline = SelfEsteemPolicy.Baseline(new SelfEsteemBaselineFacts {Station = 0});
            SelfEsteemPolicy.BandOf(SelfEsteemPolicy.Current(baseline, state, 121)).Should().BeOneOf(SelfEsteemBand.Shaken, SelfEsteemBand.Broken);
        }
    }
}
