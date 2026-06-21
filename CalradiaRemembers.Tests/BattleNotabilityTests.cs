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
        [Test]
        public void GIVEN_a_siege_WHEN_judging_THEN_it_is_notable_whatever_the_armies()
        {
            BattleNotability.IsNotable(isSiege: true, attackerHasArmy: false, defenderHasArmy: false).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_clash_of_two_armies_WHEN_judging_THEN_it_is_notable()
        {
            BattleNotability.IsNotable(isSiege: false, attackerHasArmy: true, defenderHasArmy: true).Should().BeTrue();
        }

        [Test]
        public void GIVEN_only_one_side_fields_an_army_WHEN_judging_THEN_it_is_not_notable()
        {
            //Arrange — an army crushing a lone party is a skirmish, not a remembered battle.
            BattleNotability.IsNotable(isSiege: false, attackerHasArmy: true, defenderHasArmy: false).Should().BeFalse();
            BattleNotability.IsNotable(isSiege: false, attackerHasArmy: false, defenderHasArmy: true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_plain_field_skirmish_WHEN_judging_THEN_it_is_not_notable()
        {
            BattleNotability.IsNotable(isSiege: false, attackerHasArmy: false, defenderHasArmy: false).Should().BeFalse();
        }
    }
}
