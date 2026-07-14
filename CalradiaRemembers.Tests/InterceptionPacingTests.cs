// Code written by Gabriel Mailhot, 13/07/2026.
// How often lords ride out to have a word with the player. A player asked for the dial outright, and named both
// reasons in the same breath: "I like NPCs approaching me to kick off conversations" and "I don't want to be
// forced into a bunch of conversations" (Fakade, 2026-07-13). One player, two opposite needs. That is a setting.
//
// WHY IT MATTERS: this governs how often the game interrupts the player, which is the most intrusive thing a mod
// can do to someone's evening. Get it wrong in one direction and the feature is dead; wrong in the other and it
// becomes harassment. The rule these tests guard above all is that the DEFAULT changes nothing: a player who
// never opens the menu must get precisely the numbers that shipped.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class InterceptionPacingTests
    {
        [Test]
        public void GIVEN_a_player_who_never_opens_the_menu_WHEN_lords_ride_out_THEN_nothing_whatsoever_changes()
        {
            // The load-bearing guarantee of any new setting: the default must be the status quo, exactly. If this
            // drifts, every existing player's game changes because we added an option they never asked for.
            InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Occasionally)
                .Should().Be(InterceptionPacingRules.DefaultDailyChancePercent);
            InterceptionPacingRules.CooldownDays(InterceptionPacing.Occasionally)
                .Should().Be(InterceptionPacingRules.DefaultCooldownDays);
        }

        [Test]
        public void GIVEN_a_player_who_wants_the_road_to_themselves_WHEN_set_to_Never_THEN_no_lord_ever_rides_out()
        {
            // Never must mean never, not merely seldom. A player who does not want to be stopped on the road
            // should not be stopped at all, and the behaviour skips its work entirely rather than rolling a die
            // it has already lost.
            InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Never).Should().Be(0);
            InterceptionPacingRules.IsOff(InterceptionPacing.Never).Should().BeTrue();
        }

        [Test]
        public void GIVEN_any_setting_that_is_not_Never_WHEN_asked_THEN_the_feature_is_live()
        {
            InterceptionPacingRules.IsOff(InterceptionPacing.Rarely).Should().BeFalse();
            InterceptionPacingRules.IsOff(InterceptionPacing.Occasionally).Should().BeFalse();
            InterceptionPacingRules.IsOff(InterceptionPacing.Often).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_four_settings_WHEN_ordered_THEN_each_one_up_genuinely_means_more_company()
        {
            // A dial the player cannot feel is a lie. Each step must actually raise the odds.
            InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Never)
                .Should().BeLessThan(InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Rarely));
            InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Rarely)
                .Should().BeLessThan(InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Occasionally));
            InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Occasionally)
                .Should().BeLessThan(InterceptionPacingRules.DailyChancePercent(InterceptionPacing.Often));
        }

        [Test]
        public void GIVEN_more_company_is_asked_for_WHEN_the_cooldown_is_read_THEN_it_moves_WITH_the_chance_not_against_it()
        {
            // The two levers are one feeling. A high chance paired with a long per-lord cooldown still starves the
            // player of company (the same few lords are always barred), and a low chance paired with none still
            // lets one face nag them. Turning one without the other would give a dial that does not do what it says.
            InterceptionPacingRules.CooldownDays(InterceptionPacing.Often)
                .Should().BeLessThan(InterceptionPacingRules.CooldownDays(InterceptionPacing.Occasionally));
            InterceptionPacingRules.CooldownDays(InterceptionPacing.Occasionally)
                .Should().BeLessThan(InterceptionPacingRules.CooldownDays(InterceptionPacing.Rarely));
        }
    }
}
