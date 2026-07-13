// Code written by Gabriel Mailhot, 27/06/2026.
// Documents when a town ambush springs (a roll inside the spring chance), how many companions stand with the
// player, and the attacking force it draws (one elite chief plus ~3 cutthroats per defender, nudged by ±1).
//
// WHY IT MATTERS: DecidePlayerTownEntry fixes the ORDER the host checks armed / enabled / roll, so its log
// always states the true reason nothing happened. The trigger path used to be silent, which made "no ambush
// ever fires" impossible to diagnose from a bug report; get the order wrong and that diagnosability is lost
// again. ComputeForce and EliteChiefTier decide the fight's difficulty: the force scales as the SQUARE of the
// defending side (not linearly) so a bigger retinue faces disproportionate odds, and the chief's tier is
// clamped to the player's own clan tier so an early clan still meets a credible soldier without ever facing a
// super-human veteran. Wrong here and the ambush is either a trivial formality or an unwinnable curbstomp.

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
        // SpringChancePercent (35) is the TUNE that sets how often an armed lord's ambush actually fires.
        // Pin the < boundary here so a future retune can never silently drift into an off-by-one that fires
        // the ambush more (or less) often than the intended chance.
        [Test]
        public void GIVEN_a_roll_below_the_chance_WHEN_checking_THEN_the_ambush_springs()
        {
            AmbushPolicy.ShouldSpring(0).Should().BeTrue();
            AmbushPolicy.ShouldSpring(AmbushPolicy.SpringChancePercent - 1).Should().BeTrue();
        }

        // The far side of the same boundary: a roll landing exactly ON the chance must NOT spring it, or the
        // real spring rate silently becomes SpringChancePercent+1 instead of the tuned value.
        [Test]
        public void GIVEN_a_roll_at_or_above_the_chance_WHEN_checking_THEN_the_streets_stay_quiet()
        {
            AmbushPolicy.ShouldSpring(AmbushPolicy.SpringChancePercent).Should().BeFalse();
            AmbushPolicy.ShouldSpring(99).Should().BeFalse();
        }

        // ── Companions at the player's side (every party companion stands, capped) ──

        // A negative party count should never happen, but this floors it to zero rather than letting it flow
        // into ComputeForce and produce a nonsensical (or negative) attacking force.
        [Test]
        public void GIVEN_no_companions_in_the_party_WHEN_counting_the_side_THEN_the_player_stands_alone()
        {
            AmbushPolicy.CompanionsAtSide(0).Should().Be(0);
            AmbushPolicy.CompanionsAtSide(-3).Should().Be(0);
        }

        // MaxCompanionsAtSide caps how many companions can fight alongside the player. Since the attacking
        // force scales as the SQUARE of the defending side, an uncapped retinue would make the ambush balloon
        // out of control instead of staying a real (if heavier) fight.
        [Test]
        public void GIVEN_companions_in_the_party_WHEN_counting_the_side_THEN_all_stand_up_to_the_cap()
        {
            AmbushPolicy.CompanionsAtSide(1).Should().Be(1);
            AmbushPolicy.CompanionsAtSide(3).Should().Be(3);
            AmbushPolicy.CompanionsAtSide(5).Should().Be(AmbushPolicy.MaxCompanionsAtSide); // never more than the cap (3)
        }

        // ── The attacking force: one chief + ~3 cutthroats per defender, nudged by ±1 ──

        // The baseline case pins the (defenders+1)^2 formula at its smallest input: get the exponent or the
        // "minus the chief" bookkeeping wrong here and every larger-retinue calculation below is wrong too.
        [Test]
        public void GIVEN_the_player_alone_WHEN_sizing_the_force_THEN_a_chief_and_three_cutthroats()
        {
            AmbushForce force = AmbushPolicy.ComputeForce(defendersAtPlayerSide: 1, jitterRoll: 1); // jitter 0
            force.ChiefCount.Should().Be(1);
            force.BanditCount.Should().Be(3);   // (1+1)^2 = 4 total, minus the chief
            force.TotalAttackers.Should().Be(4);
        }

        // The design intent (per AmbushPolicy's own comment) is SQUARE scaling, not linear: a bigger retinue
        // must face sharply heavier odds so two or three companions can't trivially cut their way out. A
        // regression to linear scaling here would quietly make large retinues too easy to fight through.
        [Test]
        public void GIVEN_a_larger_retinue_WHEN_sizing_the_force_THEN_the_total_scales_with_the_square_of_the_side()
        {
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 2, jitterRoll: 1).TotalAttackers.Should().Be(9);  // (2+1)^2
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 3, jitterRoll: 1).TotalAttackers.Should().Be(16); // (3+1)^2
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 4, jitterRoll: 1).TotalAttackers.Should().Be(25); // (4+1)^2, no cap
        }

        // The jitter exists so two ambushes never field the exact same headcount; pins the full -1/0/+1 range
        // the mod3 remainder is meant to produce, so a bug can't silently collapse the nudge to always-zero
        // (or push it outside the intended ±1 band).
        [Test]
        public void GIVEN_a_varying_roll_WHEN_sizing_the_force_THEN_the_count_is_nudged_by_one_either_way()
        {
            AmbushPolicy.ComputeForce(1, jitterRoll: 0).BanditCount.Should().Be(2); // -1
            AmbushPolicy.ComputeForce(1, jitterRoll: 1).BanditCount.Should().Be(3); //  0
            AmbushPolicy.ComputeForce(1, jitterRoll: 2).BanditCount.Should().Be(4); // +1
        }

        // A zero or negative side should never reach this method, but the fallback to "one defender" keeps
        // ComputeForce from producing a zero-or-negative bandit count that would make the ambush a no-op.
        [Test]
        public void GIVEN_a_non_positive_side_WHEN_sizing_the_force_THEN_it_floors_to_a_lone_defender()
        {
            AmbushPolicy.ComputeForce(defendersAtPlayerSide: 0, jitterRoll: 1).BanditCount.Should().Be(3);
        }

        // ── The chief's tier scales to the player's clan, bounded so it is neither a recruit nor super-human ──

        // The chief is meant to scale to the PLAYER's own progression; if this stopped tracking clan tier
        // 1:1 in the mid-band, the fight would silently decouple from the player's own strength.
        [Test]
        public void GIVEN_a_mid_clan_WHEN_sizing_the_chief_THEN_he_matches_the_player_clan_tier()
        {
            AmbushPolicy.EliteChiefTier(3).Should().Be(3);
            AmbushPolicy.EliteChiefTier(5).Should().Be(5);
        }

        // The clamp is what keeps the fight fair at both extremes: an early clan must still meet a credible
        // soldier (never MinChiefTier's floor breached downward into a mere recruit), and a great house must
        // never be handed a super-human chief beyond MaxChiefTier. Losing either bound breaks that balance.
        [Test]
        public void GIVEN_a_fledgling_or_great_clan_WHEN_sizing_the_chief_THEN_it_clamps_to_the_band()
        {
            AmbushPolicy.EliteChiefTier(0).Should().Be(AmbushPolicy.MinChiefTier); // never a mere recruit
            AmbushPolicy.EliteChiefTier(9).Should().Be(AmbushPolicy.MaxChiefTier); // never beyond the noble line
        }

        // ── The player-town-entry gating decision (protects the trigger path) ──

        // Pins that the ARMED check runs first: this is the overwhelming majority of every town visit for
        // every player, and it must resolve to a distinct, named outcome rather than silently falling through
        // to whatever the next check happens to return.
        [Test]
        public void GIVEN_no_one_has_an_attempt_armed_WHEN_entering_a_town_THEN_nothing_is_decided()
        {
            // The ordinary case for every player: even with a winning roll, no armed assassin → no ambush.
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: true, hasArmedAssassin: false, rollPercent: 0)
                .Should().Be(AmbushEntryOutcome.NoArmedAssassin);
        }

        // The trigger path used to be silent (per AmbushPolicy's own doc), so "no ambush ever fires" could not
        // be diagnosed. Pins that a winning roll is still overridden by the disabled toggle, AND that the
        // outcome is the distinct Disabled value (not lumped with Fizzled) so the host log states the true
        // reason instead of a generic "nothing happened".
        [Test]
        public void GIVEN_an_armed_assassin_but_the_feature_is_off_WHEN_entering_a_town_THEN_it_is_disabled()
        {
            // The reported case: armed via console, but the experimental MCM toggle was off. Even a winning roll
            // must NOT spring it — and the host logs "Disabled" so this is diagnosable from now on.
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: false, hasArmedAssassin: true, rollPercent: 0)
                .Should().Be(AmbushEntryOutcome.Disabled);
        }

        // A miss must NOT consume the arming: an armed lord re-rolls every visit until it springs (per the
        // policy's own doc), so Fizzled has to be distinct from a permanent refusal, and the roll boundary
        // must line up exactly with ShouldSpring's own boundary or the two policies would disagree.
        [Test]
        public void GIVEN_armed_and_enabled_WHEN_the_roll_misses_THEN_it_fizzles_and_re_rolls_next_visit()
        {
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: true, hasArmedAssassin: true, rollPercent: AmbushPolicy.SpringChancePercent)
                .Should().Be(AmbushEntryOutcome.Fizzled);
        }

        // The positive path: with every gate open, the ambush must actually fire. Without this, a bug that
        // makes every check fall through to a refusal would go unnoticed since the failure looks identical to
        // the ordinary "nothing happens" case a player sees on every other town visit.
        [Test]
        public void GIVEN_armed_and_enabled_WHEN_the_roll_lands_THEN_the_ambush_springs()
        {
            AmbushPolicy.DecidePlayerTownEntry(featureEnabled: true, hasArmedAssassin: true, rollPercent: 0)
                .Should().Be(AmbushEntryOutcome.Spring);
        }
    }
}
