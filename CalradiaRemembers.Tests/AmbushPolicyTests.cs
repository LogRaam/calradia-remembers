// Code written by Gabriel Mailhot, 27/06/2026.
// Documents when a town ambush springs (a roll inside the spring chance), how many companions stand with the
// player, and the attacking force it draws (one elite chief plus ~3 cutthroats per defender, nudged by ±1).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AmbushPolicyTests
    {
        [Test]
        public void GIVEN_a_roll_below_the_chance_WHEN_checking_THEN_the_ambush_springs()
        {
            AmbushPolicy.ShouldSpring(0).Should().BeTrue();
            AmbushPolicy.ShouldSpring(AmbushPolicy.SpringChancePercent - 1).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_roll_at_or_above_the_chance_WHEN_checking_THEN_the_streets_stay_quiet()
        {
            AmbushPolicy.ShouldSpring(AmbushPolicy.SpringChancePercent).Should().BeFalse();
            AmbushPolicy.ShouldSpring(99).Should().BeFalse();
        }

        // ── Companions at the player's side (every party companion stands, capped) ──

        [Test]
        public void GIVEN_no_companions_in_the_party_WHEN_counting_the_side_THEN_the_player_stands_alone()
        {
            AmbushPolicy.CompanionsAtSide(0).Should().Be(0);
            AmbushPolicy.CompanionsAtSide(-3).Should().Be(0);
        }

        [Test]
        public void GIVEN_companions_in_the_party_WHEN_counting_the_side_THEN_all_stand_up_to_the_cap()
        {
            AmbushPolicy.CompanionsAtSide(1).Should().Be(1);
            AmbushPolicy.CompanionsAtSide(3).Should().Be(3);
            AmbushPolicy.CompanionsAtSide(5).Should().Be(AmbushPolicy.MaxCompanionsAtSide); // never more than the cap (3)
        }

        // ── The attacking force: one chief + ~3 cutthroats per defender, nudged by ±1 ──

        [Test]
        public void GIVEN_the_player_alone_WHEN_sizing_the_force_THEN_a_chief_and_three_cutthroats()
        {
            AmbushForce force = AmbushPolicy.ComputeForce(defendersAtPlayerSide: 1, jitterRoll: 1); // jitter 0
            force.ChiefCount.Should().Be(1);
            force.BanditCount.Should().Be(3);   // (1+1)^2 = 4 total, minus the chief
            force.TotalAttackers.Should().Be(4);
        }

        [Test]
        public void GIVEN_a_larger_retinue_WHEN_sizing_the_force_THEN_the_total_scales_with_the_square_of_the_side()
        {
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 2, jitterRoll: 1).TotalAttackers.Should().Be(9);  // (2+1)^2
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 3, jitterRoll: 1).TotalAttackers.Should().Be(16); // (3+1)^2
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 4, jitterRoll: 1).TotalAttackers.Should().Be(25); // (4+1)^2, no cap
        }

        [Test]
        public void GIVEN_a_varying_roll_WHEN_sizing_the_force_THEN_the_count_is_nudged_by_one_either_way()
        {
            AmbushPolicy.ComputeForce(1, jitterRoll: 0).BanditCount.Should().Be(2); // -1
            AmbushPolicy.ComputeForce(1, jitterRoll: 1).BanditCount.Should().Be(3); //  0
            AmbushPolicy.ComputeForce(1, jitterRoll: 2).BanditCount.Should().Be(4); // +1
        }

        [Test]
        public void GIVEN_a_non_positive_side_WHEN_sizing_the_force_THEN_it_floors_to_a_lone_defender()
        {
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 0, jitterRoll: 1).BanditCount.Should().Be(3);
        }

        // ── The chief's tier scales to the player's clan, bounded so it is neither a recruit nor super-human ──

        [Test]
        public void GIVEN_a_mid_clan_WHEN_sizing_the_chief_THEN_he_matches_the_player_clan_tier()
        {
            AmbushPolicy.EliteChiefTier(3).Should().Be(3);
            AmbushPolicy.EliteChiefTier(5).Should().Be(5);
        }

        [Test]
        public void GIVEN_a_fledgling_or_great_clan_WHEN_sizing_the_chief_THEN_it_clamps_to_the_band()
        {
            AmbushPolicy.EliteChiefTier(0).Should().Be(AmbushPolicy.MinChiefTier); // never a mere recruit
            AmbushPolicy.EliteChiefTier(9).Should().Be(AmbushPolicy.MaxChiefTier); // never beyond the noble line
        }

        // ── The player-town-entry gating decision (protects the trigger path) ──

        [Test]
        public void GIVEN_no_one_has_an_attempt_armed_WHEN_entering_a_town_THEN_nothing_is_decided()
        {
            // The ordinary case for every player: even with a winning roll, no armed assassin → no ambush.
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: true, hasArmedAssassin: false, rollPercent: 0)
                .Should().Be(AmbushEntryOutcome.NoArmedAssassin);
        }

        [Test]
        public void GIVEN_an_armed_assassin_but_the_feature_is_off_WHEN_entering_a_town_THEN_it_is_disabled()
        {
            // The reported case: armed via console, but the experimental MCM toggle was off. Even a winning roll
            // must NOT spring it — and the host logs "Disabled" so this is diagnosable from now on.
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: false, hasArmedAssassin: true, rollPercent: 0)
                .Should().Be(AmbushEntryOutcome.Disabled);
        }

        [Test]
        public void GIVEN_armed_and_enabled_WHEN_the_roll_misses_THEN_it_fizzles_and_re_rolls_next_visit()
        {
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: true, hasArmedAssassin: true, rollPercent: AmbushPolicy.SpringChancePercent)
                .Should().Be(AmbushEntryOutcome.Fizzled);
        }

        [Test]
        public void GIVEN_armed_and_enabled_WHEN_the_roll_lands_THEN_the_ambush_springs()
        {
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: true, hasArmedAssassin: true, rollPercent: 0)
                .Should().Be(AmbushEntryOutcome.Spring);
        }
    }
}
