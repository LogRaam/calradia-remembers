// Code written by Gabriel Mailhot, 21/06/2026.
// Protects the rule that bounds memory growth: only sieges and two-army battles get recorded. A drift
// either floods the save with skirmish noise, or stops recording the battles that should be remembered.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BattleNotabilityTests
    {
        // A siege must always be recorded regardless of army composition (many sieges involve a garrison,
        // not a formal "army" on either side); miss this and sieges silently drop out of the chronicle and
        // every present lord's memory.
        [Test]
        public void GIVEN_a_siege_WHEN_judging_THEN_it_is_notable_whatever_the_armies()
        {
            BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = true, AttackerHasArmy = false, DefenderHasArmy = false}).Should().BeTrue();
        }

        // The other half of the bar: two real armies clashing is exactly what lords and the chronicle
        // should remember. Pins that BOTH sides are required, not just one.
        [Test]
        public void GIVEN_a_clash_of_two_armies_WHEN_judging_THEN_it_is_notable()
        {
            BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = false, AttackerHasArmy = true, DefenderHasArmy = true}).Should().BeTrue();
        }

        // Guards against a full army crushing a lone party being logged as a real battle: it would flood
        // every present lord's memory and the world-event log with what is really a curb-stomp.
        [Test]
        public void GIVEN_only_one_side_fields_an_army_WHEN_judging_THEN_it_is_not_notable()
        {
            //Arrange — an army crushing a lone party is a skirmish, not a remembered battle.
            BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = false, AttackerHasArmy = true, DefenderHasArmy = false}).Should().BeFalse();
            BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = false, AttackerHasArmy = false, DefenderHasArmy = true}).Should().BeFalse();
        }

        // The base noise floor: without it, every minor field scrap (a bandit chase, a small patrol clash)
        // would bloat the save, which is the exact failure this policy exists to bound.
        [Test]
        public void GIVEN_a_plain_field_skirmish_WHEN_judging_THEN_it_is_not_notable()
        {
            BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = false, AttackerHasArmy = false, DefenderHasArmy = false}).Should().BeFalse();
        }

        // The 04/07 addendum: a big scrap the player personally led deserves a chronicle line even when
        // neither side was a formal army. Without this bypass, some of the player's own most memorable
        // fights would go unrecorded.
        [Test]
        public void GIVEN_a_player_battle_at_or_above_the_size_threshold_WHEN_judging_THEN_it_is_notable_without_armies()
        {
            BattleNotability.IsNotableForPlayer(new BattleNotability.BattleFacts {IsSiege = false, AttackerHasArmy = false, DefenderHasArmy = false, TotalCombatants = BattleNotability.PlayerBattleSizeThreshold}).Should().BeTrue();
        }

        // Pins the floor of the size bypass: one combatant under threshold and it reverts to the normal
        // siege/army rule, so a small player skirmish cannot sneak into the chronicle on size alone.
        [Test]
        public void GIVEN_a_player_battle_below_the_size_threshold_WHEN_judging_THEN_it_is_not_notable()
        {
            BattleNotability.IsNotableForPlayer(new BattleNotability.BattleFacts {IsSiege = false, AttackerHasArmy = false, DefenderHasArmy = false, TotalCombatants = BattleNotability.PlayerBattleSizeThreshold - 1}).Should().BeFalse();
        }

        // Confirms the size threshold is an ADDITIONAL bypass for field battles, never a requirement: a
        // small player siege must still be recorded through the plain siege rule regardless of headcount.
        [Test]
        public void GIVEN_a_player_siege_below_the_size_threshold_WHEN_judging_THEN_it_is_still_notable()
        {
            BattleNotability.IsNotableForPlayer(new BattleNotability.BattleFacts {IsSiege = true, AttackerHasArmy = false, DefenderHasArmy = false, TotalCombatants = 1}).Should().BeTrue();
        }
    }
}
