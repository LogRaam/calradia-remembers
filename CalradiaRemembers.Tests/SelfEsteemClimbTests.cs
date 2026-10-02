// Code written by Gabriel Mailhot, 02/10/2026.
// fkasad (Nexus, 01/10/2026): "I'm not sure that any little battle won like fighting half a dozen looters should grants
// self-esteem. Vacuum the house and take out the trash shouldn't grant so much. Almost everyone in my faction is at 100
// just by doing chores during peace time... it should get harder beyond a certain score. Or perhaps the classic
// polynomial progression/diminishing returns/S-Curve rather than a fixed added value." Gabriel: a slider in MCM to
// calibrate it. If these rules are wrong, every lord who clears looters struts at the top of the scale again, a real
// victory against the odds counts no more than a skirmish, or a man brought low cannot climb back.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SelfEsteemClimbTests
    {
        private static readonly SelfEsteemTemper Plain = new SelfEsteemTemper();

        // ── What a won battle is worth ────────────────────────────────────────────────────────────

        // "Half a dozen looters": bandits he outnumbered are no feat at all.
        [Test]
        public void GIVEN_a_handful_of_bandits_he_outnumbered_WHEN_he_wins_THEN_his_pride_does_not_move()
        {
            SelfEsteemPolicy.ClassifyWin(new BattleWorthFacts {EnemyHadLord = false, StrengthRatio = 0.2f}).Should().BeNull();
        }

        // A real band of bandits is some credit, little; a lord's party is a victory; a stronger lord's force, more. The
        // lines are the letter weights' own (no real fight below half; an underdog above even), so a lord's pride and
        // the letters he writes about the same battle agree.
        [Test]
        public void GIVEN_who_he_beat_WHEN_he_wins_THEN_the_credit_follows_what_it_was_worth()
        {
            SelfEsteemPolicy.ClassifyWin(new BattleWorthFacts {EnemyHadLord = false, StrengthRatio = 0.8f}).Should().Be(SelfEsteemEvent.SkirmishWon);
            SelfEsteemPolicy.ClassifyWin(new BattleWorthFacts {EnemyHadLord = true, StrengthRatio = 0.8f}).Should().Be(SelfEsteemEvent.BattleWon);
            SelfEsteemPolicy.ClassifyWin(new BattleWorthFacts {EnemyHadLord = true, StrengthRatio = 1.5f}).Should().Be(SelfEsteemEvent.BattleWonAgainstOdds);

            SelfEsteemPolicy.Delta(SelfEsteemEvent.SkirmishWon, Plain, 0).Should().BeLessThan(SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleWon, Plain, 0));
            SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleWonAgainstOdds, Plain, 0).Should().BeGreaterThan(SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleWon, Plain, 0));
        }

        // ── It gets harder near the top ───────────────────────────────────────────────────────────

        // A man at or below his middle takes a gain whole; half way up, a quarter of it (the shipped exponent); near the
        // top, almost nothing. Exponent 0 is the old fixed gain, for a player who wants it back.
        [Test]
        public void GIVEN_how_high_he_already_stands_WHEN_a_gain_lands_THEN_it_shrinks_toward_the_top()
        {
            SelfEsteemPolicy.GainFactor(current: -20, exponent: 2).Should().Be(1.0);
            SelfEsteemPolicy.GainFactor(current: 50, exponent: 2).Should().BeApproximately(0.25, 1e-9);
            SelfEsteemPolicy.GainFactor(current: 90, exponent: 2).Should().BeApproximately(0.01, 1e-9);
            SelfEsteemPolicy.GainFactor(current: 90, exponent: 0).Should().Be(1.0);
        }

        // fkasad's faction: a lord already proud gains next to nothing from another win, while a lord brought low climbs
        // back at full pace ("I understand it helps those who were defeated to regain SE").
        [Test]
        public void GIVEN_a_proud_lord_and_a_humbled_one_WHEN_each_wins_THEN_only_the_humbled_one_climbs_much()
        {
            var climb = new SelfEsteemClimb {Baseline = 40, Exponent = SelfEsteemPolicy.DefaultClimbExponent};

            SelfEsteemState proud = SelfEsteemPolicy.Record(new SelfEsteemState {Shift = 45, ShiftDay = 10}, SelfEsteemEvent.BattleWon, Plain, 10, climb);
            SelfEsteemState humbled = SelfEsteemPolicy.Record(new SelfEsteemState {Shift = -60, ShiftDay = 10}, SelfEsteemEvent.BattleWon, Plain, 10, climb);

            (proud.Shift - 45).Should().Be(0, "at 85 the headroom is 15%, and 5 x 0.0225 rounds to nothing");
            (humbled.Shift - -60).Should().Be(5, "at -20 a gain lands whole");
        }

        // A loss always lands whole: the falloff is for climbing, not for falling.
        [Test]
        public void GIVEN_a_proud_lord_WHEN_he_loses_THEN_the_loss_lands_whole()
        {
            var climb = new SelfEsteemClimb {Baseline = 40, Exponent = 4};

            SelfEsteemPolicy.Record(new SelfEsteemState {Shift = 45, ShiftDay = 10}, SelfEsteemEvent.BattleLost, Plain, 10, climb)
                            .Shift.Should().Be(45 + SelfEsteemPolicy.Delta(SelfEsteemEvent.BattleLost, Plain, 0));
        }
    }
}
