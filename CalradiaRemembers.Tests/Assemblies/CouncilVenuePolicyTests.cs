// Code written by Gabriel Mailhot, 31/07/2026.
// Documents the venue pick for a settlement-hosted council: which interior the player actually walks into when
// convening. Wrong here means the player either gets locked out of a settlement that DOES have a usable room, or
// gets shoved into a hall they do not own, which would read as a bug in the "reuse the proven duel scene" recipe.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilVenuePolicyTests
    {
        // The owning clan presides in its own house: this is the ordinary case (player convenes at their own
        // fief) and must land in the Hall, not the common room.
        [Test]
        public void GIVEN_the_player_owns_the_settlement_and_it_has_a_lordshall_WHEN_choosing_a_venue_THEN_the_hall_is_chosen()
        {
            CouncilVenue venue = CouncilVenuePolicy.Choose(new CouncilVenuePolicy.VenueFacts {PlayerClanOwnsSettlement = true, HasLordshall = true, HasTavern = true});

            venue.Should().Be(CouncilVenue.Hall);
        }

        // A guest has no hall to preside in, so they gather in the common room instead: convening while visiting
        // an ally's or a stranger's town must not put the player at the head of a table that is not theirs.
        [Test]
        public void GIVEN_the_player_is_a_guest_and_the_settlement_has_a_tavern_WHEN_choosing_a_venue_THEN_the_tavern_is_chosen()
        {
            CouncilVenue venue = CouncilVenuePolicy.Choose(new CouncilVenuePolicy.VenueFacts {PlayerClanOwnsSettlement = false, HasLordshall = true, HasTavern = true});

            venue.Should().Be(CouncilVenue.Tavern);
        }

        // Fallback: the player owns the settlement but it has no lordshall (a small village-like fief). Without
        // this fallback an owned settlement with no hall would refuse the council outright even though a usable
        // room (the tavern) exists right there.
        [Test]
        public void GIVEN_the_player_owns_the_settlement_but_it_has_no_lordshall_WHEN_choosing_a_venue_THEN_the_tavern_is_chosen_as_fallback()
        {
            CouncilVenue venue = CouncilVenuePolicy.Choose(new CouncilVenuePolicy.VenueFacts {PlayerClanOwnsSettlement = true, HasLordshall = false, HasTavern = true});

            venue.Should().Be(CouncilVenue.Tavern);
        }

        // Fallback the other way: a guest settlement with no tavern but a lordshall. Without this fallback the
        // player would be turned away from a settlement that plainly has an interior to stand in.
        [Test]
        public void GIVEN_the_player_is_a_guest_and_the_settlement_has_no_tavern_but_has_a_lordshall_WHEN_choosing_a_venue_THEN_the_hall_is_chosen_as_fallback()
        {
            CouncilVenue venue = CouncilVenuePolicy.Choose(new CouncilVenuePolicy.VenueFacts {PlayerClanOwnsSettlement = false, HasLordshall = true, HasTavern = false});

            venue.Should().Be(CouncilVenue.Hall);
        }

        // Neither interior exists: this is what makes the caller keep the SAFE map overlay instead of entering a
        // settlement with no interior at all, which would otherwise throw or silently do nothing in-scene.
        [Test]
        public void GIVEN_the_settlement_has_neither_a_lordshall_nor_a_tavern_WHEN_choosing_a_venue_THEN_none_is_chosen()
        {
            CouncilVenue venue = CouncilVenuePolicy.Choose(new CouncilVenuePolicy.VenueFacts {PlayerClanOwnsSettlement = true, HasLordshall = false, HasTavern = false});

            venue.Should().Be(CouncilVenue.None);
        }

        // The Location id is what the mod actually hands to GetLocationWithId: a typo or mismatch here would
        // silently fail to find the scene at the engine boundary, so the mapping is pinned exactly.
        [Test]
        public void GIVEN_each_venue_WHEN_asked_for_its_location_id_THEN_the_engine_ids_map_correctly()
        {
            CouncilVenuePolicy.LocationId(CouncilVenue.Hall).Should().Be("lordshall");
            CouncilVenuePolicy.LocationId(CouncilVenue.Tavern).Should().Be("tavern");
            CouncilVenuePolicy.LocationId(CouncilVenue.None).Should().BeNull();
        }
    }
}
