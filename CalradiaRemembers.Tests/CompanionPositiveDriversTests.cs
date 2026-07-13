// Code written by Gabriel Mailhot, 13/07/2026.
// Documents the deeds of ordinary play now counting toward companion contentment: victories, the camp's morale,
// posts handed out, and being genuinely heard. Before this, a companion could win your wars and govern your city
// and still sour, because only the mod's own errands ever lifted them (player request, SuperGraham339).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionPositiveDriversTests
    {
        // ── The deeds of ordinary play ─────────────────────────────────────────

        [Test]
        public void GIVEN_a_battle_fought_at_your_side_WHEN_it_is_won_or_lost_THEN_the_companion_shares_the_outcome()
        {
            CompanionHappiness.DeltaFor(HappinessDriver.SharedVictory).Should().BePositive();
            CompanionHappiness.DeltaFor(HappinessDriver.SharedDefeat).Should().BeNegative();
        }

        [Test]
        public void GIVEN_a_defeat_WHEN_a_single_week_of_drift_passes_THEN_it_is_already_forgiven()
        {
            // Losing a battle must never start a death spiral: one week's drift erases it.
            int baseline = CompanionBaseline.From(3);
            int score = CompanionHappiness.ApplyDriver(baseline, HappinessDriver.SharedDefeat);

            CompanionHappiness.DriftToBaseline(score, CompanionHappiness.WeeklyDriftStep, baseline)
                .Should().Be(baseline);
        }

        [Test]
        public void GIVEN_a_companion_handed_a_post_WHEN_appointed_THEN_the_mark_of_trust_lifts_them_most()
        {
            // A governorship or a party to lead is the strongest ordinary gesture the player can make.
            CompanionHappiness.DeltaFor(HappinessDriver.Appointed)
                .Should().BeGreaterThan(CompanionHappiness.DeltaFor(HappinessDriver.SharedVictory));
            CompanionHappiness.DeltaFor(HappinessDriver.Appointed)
                .Should().BeGreaterThan(CompanionHappiness.DeltaFor(HappinessDriver.HeardOut));
        }

        [Test]
        public void GIVEN_a_victorious_and_well_led_warlord_WHEN_the_season_goes_well_THEN_companions_rise_ABOVE_their_clans_rest()
        {
            // Winning and keeping a happy camp should carry companions past the resting point their clan sets.
            int baseline = CompanionBaseline.From(3); // 70
            int score = baseline;

            for (var week = 0; week < 8; week++)
            {
                score = CompanionHappiness.ApplyDriver(score, HappinessDriver.SharedVictory);
                score = CompanionHappiness.ApplyDriver(score, HappinessDriver.SharedVictory);
                score = CompanionHappiness.ApplyDriver(score, HappinessDriver.HighMorale);
                score = CompanionHappiness.ApplyDriver(score, HappinessDriver.Idle);
                score = CompanionHappiness.DriftToBaseline(score, CompanionHappiness.WeeklyDriftStep, baseline);
            }

            score.Should().BeGreaterThan(baseline);
            CompanionHappiness.Band(score).Should().Be(HappinessBand.Devoted);
        }

        // ── The camp's morale ──────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_camp_in_good_spirits_WHEN_the_week_turns_THEN_the_mood_lifts_the_companions()
        {
            PartyMoraleMood.DriverFor(PartyMoraleMood.HighAt).Should().Be(HappinessDriver.HighMorale);
            PartyMoraleMood.DriverFor(95f).Should().Be(HappinessDriver.HighMorale);
        }

        [Test]
        public void GIVEN_a_sullen_hungry_camp_WHEN_the_week_turns_THEN_the_mood_dulls_the_companions()
        {
            PartyMoraleMood.DriverFor(PartyMoraleMood.LowAt).Should().Be(HappinessDriver.LowMorale);
            PartyMoraleMood.DriverFor(5f).Should().Be(HappinessDriver.LowMorale);
        }

        [Test]
        public void GIVEN_an_unremarkable_camp_WHEN_the_week_turns_THEN_the_mood_says_nothing_at_all()
        {
            PartyMoraleMood.DriverFor(50f).Should().BeNull();
        }

        // ── Being heard ────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_companion_never_talked_to_WHEN_the_player_sits_with_them_THEN_it_lifts_their_spirits()
        {
            CompanionConversationPacing.LiftsSpirits(CompanionConversationPacing.NeverHeard, 42).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_player_reopening_the_chat_at_once_WHEN_asked_THEN_it_cannot_be_farmed()
        {
            // Open-and-close the window ten times in a day: it counts once.
            CompanionConversationPacing.LiftsSpirits(lastHeardDay: 42, today: 42).Should().BeFalse();
            CompanionConversationPacing.LiftsSpirits(lastHeardDay: 42, today: 45).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_cooldown_fully_elapsed_WHEN_the_player_sits_with_them_again_THEN_it_lifts_them_again()
        {
            CompanionConversationPacing
                .LiftsSpirits(42, 42 + CompanionConversationPacing.HeardOutCooldownDays)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_clock_that_ran_backwards_WHEN_asked_THEN_the_companion_is_not_locked_out_forever()
        {
            // An earlier save loaded: never leave them permanently unliftable.
            CompanionConversationPacing.LiftsSpirits(lastHeardDay: 900, today: 42).Should().BeTrue();
        }
    }
}
