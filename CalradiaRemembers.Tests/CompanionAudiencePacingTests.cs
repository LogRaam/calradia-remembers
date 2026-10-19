// Code written by Gabriel Mailhot, 08/09/2026.
// Player report (Nexus, 2026-09-08): companions travelling with the player never say anything unless the
// player remembers to routinely engage them, because the found-topic audience only ever offered on
// SettlementEntered. Gabriel's ruling adds a road trigger and makes the whole pacing player-tunable, mirroring
// InterceptionPacingRules (see that class's own tests for the lord equivalent).
//
// WHY IT MATTERS: this governs how often the game interrupts the player mid-journey, the same intrusiveness
// concern the lord dial exists for. The rule these tests guard above all is that an EXISTING player's feel does
// not change: Occasionally must keep returning the shipped 14-day global cooldown
// (CompanionAudienceTopicPolicy.GlobalCooldownDays), never the lord dial's 30-day number, because the two
// features never shared a cooldown value in the first place.

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionAudiencePacingTests
    {
        [Test]
        public void GIVEN_a_player_who_never_opens_the_menu_WHEN_the_cooldown_is_read_THEN_it_is_the_shipped_14_days()
        {
            // The load-bearing guarantee of any new setting: the default must be the status quo, exactly. If this
            // drifts, every existing player's companions suddenly go quiet for twice as long (or half as long)
            // as they used to, for a dial that player never touched.
            CompanionAudiencePacingRules.CooldownDays(CompanionAudiencePacing.Occasionally)
                .Should().Be(CompanionAudienceTopicPolicy.GlobalCooldownDays);
            CompanionAudiencePacingRules.CooldownDays(CompanionAudiencePacing.Occasionally)
                .Should().Be(CompanionAudiencePacingRules.DefaultCooldownDays);
        }

        [Test]
        public void GIVEN_a_player_who_wants_silence_WHEN_set_to_Never_THEN_neither_the_road_nor_the_settlement_trigger_ever_fires()
        {
            // Gabriel's ruling: "Never must switch the whole thing off, road and settlement alike." Zero percent
            // on the road AND IsOff() true is what lets the behaviour skip BOTH triggers' work entirely, rather
            // than merely making a companion audience rare.
            CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Never).Should().Be(0);
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Never).Should().BeTrue();
        }

        [Test]
        public void GIVEN_any_setting_that_is_not_Never_WHEN_asked_THEN_the_feature_is_live()
        {
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Rarely).Should().BeFalse();
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Occasionally).Should().BeFalse();
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Often).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_four_settings_WHEN_ordered_THEN_each_one_up_genuinely_means_a_chattier_road()
        {
            // A dial the player cannot feel is a lie (the same rule InterceptionPacingRules pins for lords).
            // Each step must actually raise the odds of the road trigger.
            CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Never)
                .Should().BeLessThan(CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Rarely));
            CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Rarely)
                .Should().BeLessThan(CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Occasionally));
            CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Occasionally)
                .Should().BeLessThan(CompanionAudiencePacingRules.DailyChancePercent(CompanionAudiencePacing.Often));
        }

        [Test]
        public void GIVEN_more_company_is_asked_for_WHEN_the_cooldown_is_read_THEN_it_moves_WITH_the_chance_not_against_it()
        {
            // The two levers are one feeling (InterceptionPacingRules' own rationale, reused here verbatim): a
            // high road chance paired with a long shared cooldown still starves the player (the popup can never
            // land again for weeks once it does), and a low chance paired with none still lets a lucky roll nag
            // right after the last one. Turning one without the other would give a dial that does not do what
            // it says.
            CompanionAudiencePacingRules.CooldownDays(CompanionAudiencePacing.Often)
                .Should().BeLessThan(CompanionAudiencePacingRules.CooldownDays(CompanionAudiencePacing.Occasionally));
            CompanionAudiencePacingRules.CooldownDays(CompanionAudiencePacing.Occasionally)
                .Should().BeLessThan(CompanionAudiencePacingRules.CooldownDays(CompanionAudiencePacing.Rarely));
        }
         // The gap that shipped in the first draft: the off switch was checked in the road tick alone, so a player
        // who set Never kept being asked for a word on entering a town, which is the very place the setting was
        // meant to reach. A dial that does not do what it says is worse than no dial, because the player stops
        // trusting the whole options screen.
        [Test]
        public void GIVEN_the_dial_is_set_to_never_WHEN_asked_THEN_the_feature_reports_itself_off()
        {
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Never).Should().BeTrue();

            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Rarely).Should().BeFalse();
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Occasionally).Should().BeFalse();
            CompanionAudiencePacingRules.IsOff(CompanionAudiencePacing.Often).Should().BeFalse();
        }
    }
}
