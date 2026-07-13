// Code written by Gabriel Mailhot, 12/07/2026.
// Documents how a presence tier becomes the NPC's "awareness of being" at the player's side, and the two
// thresholds it governs. The flagship case is the reported one: a crew member riding with the player between
// two ports should sound like a daily companion, never like someone abandoned for a season, so the co-travel
// tiers get a warm present-tense line AND suppress the elapsed-gap note entirely.

#region

using CalradiaRemembers.Logic.Social;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Social
{
    [TestFixture]
    public sealed class PresenceDescriberTests
    {
        [Test]
        public void GIVEN_a_companion_riding_in_the_party_on_land_WHEN_describing_THEN_a_side_by_side_land_line_and_no_gap()
        {
            // The reported crew case: with the player day after day, never "abandoned for decades between ports".
            PresenceDescriber.Describe(PresenceTier.InPlayerParty, null, false)
                .Should().Be("You travel at the player's side, sharing camp and road day after day.");
            PresenceDescriber.ShowsElapsedGap(PresenceTier.InPlayerParty).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_companion_in_the_party_at_sea_WHEN_describing_THEN_the_bond_is_phrased_in_sea_terms()
        {
            // A companion aboard ship must not speak of marching a road.
            PresenceDescriber.Describe(PresenceTier.InPlayerParty, null, true)
                .Should().Be("You sail at the player's side, sharing deck and voyage day after day.");
        }

        [Test]
        public void GIVEN_a_party_marching_in_the_players_army_on_land_WHEN_describing_THEN_an_army_line_and_no_gap()
        {
            PresenceDescriber.Describe(PresenceTier.InPlayerArmy, null, false)
                .Should().Be("You march in the player's army, and see them about the camp.");
            PresenceDescriber.ShowsElapsedGap(PresenceTier.InPlayerArmy).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_party_in_the_players_army_at_sea_WHEN_describing_THEN_a_fleet_line()
        {
            PresenceDescriber.Describe(PresenceTier.InPlayerArmy, null, true)
                .Should().Be("You sail in the player's fleet, and see them about the deck.");
        }

        [Test]
        public void GIVEN_a_shared_settlement_WHEN_describing_THEN_a_here_now_line_naming_the_place_and_a_gap_is_allowed()
        {
            // A chance co-location, not a shared road: they may still remark on the season apart. A settlement is
            // always ashore, so the sea flag does not change the wording.
            PresenceDescriber.Describe(PresenceTier.SamePlace, "Pravend", false)
                .Should().Be("You and the player are both in Pravend just now.");
            PresenceDescriber.Describe(PresenceTier.SamePlace, "Pravend", true)
                .Should().Be("You and the player are both in Pravend just now.");
            PresenceDescriber.ShowsElapsedGap(PresenceTier.SamePlace).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_shared_settlement_with_no_place_name_WHEN_describing_THEN_a_generic_here_now_line()
        {
            PresenceDescriber.Describe(PresenceTier.SamePlace, null, false)
                .Should().Be("You and the player are in the same place just now.");
            PresenceDescriber.Describe(PresenceTier.SamePlace, "   ", false)
                .Should().Be("You and the player are in the same place just now.");
        }

        [Test]
        public void GIVEN_an_npc_away_WHEN_describing_THEN_no_spatial_line_but_the_gap_note_is_allowed()
        {
            PresenceDescriber.Describe(PresenceTier.Away, null, false).Should().BeNull();
            PresenceDescriber.Describe(PresenceTier.Away, null, true).Should().BeNull();
            PresenceDescriber.ShowsElapsedGap(PresenceTier.Away).Should().BeTrue();
        }
    }
}
