// Code written by Gabriel Mailhot, 22/06/2026.
// Guards the declare-war deed: it completes only when the PLAYER declared war on the demanded faction —
// never when the player was attacked, and never on an unrelated war. A drift would credit (or deny) the
// deed for the wrong war.

#region

using CalradiaRemembers.Logic.War;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class WarDeedMatcherTests
    {
        // The baseline case this deed exists for: the player's own declaration against the exact demanded
        // faction must register as satisfied.
        [Test]
        public void GIVEN_the_player_declares_war_on_the_demanded_faction_WHEN_judging_THEN_it_is_satisfied()
        {
            WarDeedMatcher.IsSatisfied(new WarSides("player_kingdom", "vlandia"), deedTargetFactionId: "vlandia", playerFactionId: "player_kingdom").Should().BeTrue();
        }

        // The deed is self-balancing (the player's own act, the player's own consequences): being attacked
        // must never count as fulfilling it, or the player could farm a lord's favor for free by doing
        // nothing while an enemy declares on them.
        [Test]
        public void GIVEN_war_was_declared_ON_the_player_WHEN_judging_THEN_it_is_NOT_satisfied()
        {
            // Vlandia declared war on the player — the opposite act; the deed asked the PLAYER to declare.
            WarDeedMatcher.IsSatisfied(new WarSides("vlandia", "player_kingdom"), deedTargetFactionId: "vlandia", playerFactionId: "player_kingdom").Should().BeFalse();
        }

        // Declaring war on the wrong target must not satisfy a deed that named a specific faction, or a
        // lord's request ("rid me of the Vlandians") could be honoured by attacking an unrelated rival.
        [Test]
        public void GIVEN_war_with_a_different_faction_WHEN_judging_THEN_it_is_NOT_satisfied()
        {
            WarDeedMatcher.IsSatisfied(new WarSides("player_kingdom", "sturgia"), deedTargetFactionId: "vlandia", playerFactionId: "player_kingdom").Should().BeFalse();
        }

        // Faction ids surface from different sources (saved deed data vs. live game events) and their casing
        // is not guaranteed to match; a case-sensitive bug here would silently fail a legitimately satisfied
        // deed.
        [Test]
        public void GIVEN_case_differences_WHEN_judging_THEN_matching_is_case_insensitive()
        {
            WarDeedMatcher.IsSatisfied(new WarSides("Player_Kingdom", "VLANDIA"), "Vlandia", "player_kingdom").Should().BeTrue();
        }

        // Blank or unresolved ids (a malformed deed, or a faction lookup that came back empty) must fail
        // closed, never accidentally read as a satisfied deed.
        [Test]
        public void GIVEN_missing_ids_WHEN_judging_THEN_it_is_NOT_satisfied()
        {
            WarDeedMatcher.IsSatisfied(new WarSides("player_kingdom", "vlandia"), null, "player_kingdom").Should().BeFalse();
            WarDeedMatcher.IsSatisfied(new WarSides("player_kingdom", "vlandia"), "vlandia", "  ").Should().BeFalse();
        }
    }
}
