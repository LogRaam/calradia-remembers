// Code written by Gabriel Mailhot, 22/06/2026.
// Guards the declare-war deed: it completes only when the PLAYER declared war on the demanded faction —
// never when the player was attacked, and never on an unrelated war. A drift would credit (or deny) the
// deed for the wrong war.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class WarDeedMatcherTests
    {
        [Test]
        public void GIVEN_the_player_declares_war_on_the_demanded_faction_WHEN_judging_THEN_it_is_satisfied()
        {
            WarDeedMatcher.IsSatisfied(
                deedTargetFactionId: "vlandia",
                aggressorFactionId: "player_kingdom",
                defenderFactionId: "vlandia",
                playerFactionId: "player_kingdom").Should().BeTrue();
        }

        [Test]
        public void GIVEN_war_was_declared_ON_the_player_WHEN_judging_THEN_it_is_NOT_satisfied()
        {
            // Vlandia declared war on the player — the opposite act; the deed asked the PLAYER to declare.
            WarDeedMatcher.IsSatisfied(
                deedTargetFactionId: "vlandia",
                aggressorFactionId: "vlandia",
                defenderFactionId: "player_kingdom",
                playerFactionId: "player_kingdom").Should().BeFalse();
        }

        [Test]
        public void GIVEN_war_with_a_different_faction_WHEN_judging_THEN_it_is_NOT_satisfied()
        {
            WarDeedMatcher.IsSatisfied(
                deedTargetFactionId: "vlandia",
                aggressorFactionId: "player_kingdom",
                defenderFactionId: "sturgia",
                playerFactionId: "player_kingdom").Should().BeFalse();
        }

        [Test]
        public void GIVEN_case_differences_WHEN_judging_THEN_matching_is_case_insensitive()
        {
            WarDeedMatcher.IsSatisfied("Vlandia", "Player_Kingdom", "VLANDIA", "player_kingdom").Should().BeTrue();
        }

        [Test]
        public void GIVEN_missing_ids_WHEN_judging_THEN_it_is_NOT_satisfied()
        {
            WarDeedMatcher.IsSatisfied(null, "player_kingdom", "vlandia", "player_kingdom").Should().BeFalse();
            WarDeedMatcher.IsSatisfied("vlandia", "player_kingdom", "vlandia", "  ").Should().BeFalse();
        }
    }
}
