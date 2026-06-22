// Code written by Gabriel Mailhot, 22/06/2026.
// Guards the deliver-a-prisoner deed's matching: the right captive satisfies it, a named hero wins over a
// faction demand, and "do I already hold one" (case a vs b) is read correctly — a drift hands over or
// credits the wrong prisoner.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerDeedTargetTests
    {
        [Test]
        public void GIVEN_a_named_hero_demand_WHEN_matching_THEN_only_that_hero_satisfies()
        {
            var target = new PrisonerDeedTarget(heroId: "lord_caladog", factionId: null);
            target.Matches("lord_caladog", "battania").Should().BeTrue();
            target.Matches("lord_someone_else", "battania").Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_faction_demand_WHEN_matching_THEN_any_captive_of_that_faction_satisfies()
        {
            var target = new PrisonerDeedTarget(heroId: null, factionId: "vlandia");
            target.Matches("lord_a", "vlandia").Should().BeTrue();
            target.Matches("lord_b", "VLANDIA").Should().BeTrue();   // case-insensitive
            target.Matches("lord_c", "sturgia").Should().BeFalse();
        }

        [Test]
        public void GIVEN_both_a_hero_and_a_faction_WHEN_matching_THEN_the_named_hero_takes_precedence()
        {
            var target = new PrisonerDeedTarget(heroId: "lord_caladog", factionId: "battania");
            // A different Battanian does NOT satisfy a demand that named Caladog specifically.
            target.Matches("lord_other_battanian", "battania").Should().BeFalse();
            target.Matches("lord_caladog", "battania").Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_targetless_demand_WHEN_asked_THEN_it_is_invalid_and_matches_nothing()
        {
            var target = new PrisonerDeedTarget(heroId: " ", factionId: null);
            target.IsValid.Should().BeFalse();
            target.Matches("lord_a", "vlandia").Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_player_holds_a_matching_captive_WHEN_classifying_THEN_it_is_case_a()
        {
            var target = new PrisonerDeedTarget(heroId: null, factionId: "vlandia");
            var held = new[] { ("lord_x", "sturgia"), ("lord_y", "vlandia") };
            target.AnyHeldSatisfies(held).Should().BeTrue();   // holds a Vlandian → hand-over now
        }

        [Test]
        public void GIVEN_the_player_holds_no_matching_captive_WHEN_classifying_THEN_it_is_case_b()
        {
            var target = new PrisonerDeedTarget(heroId: "lord_caladog", factionId: null);
            var held = new[] { ("lord_x", "sturgia"), ("lord_y", "vlandia") };
            target.AnyHeldSatisfies(held).Should().BeFalse();  // must go capture Caladog
            target.AnyHeldSatisfies(null).Should().BeFalse();
        }
    }
}
