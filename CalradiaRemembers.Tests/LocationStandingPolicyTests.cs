// Code written by Gabriel Mailhot, 16/11/2026.
// LocationStandingPolicy writes the WHERE YOU ARE note: where the conversation happens and the speaker's standing there.
// It exists so a lord visiting another lord's town does not claim the hall as his own; VonLemberg and his tester Goeth
// (Discord, 16/11/2026) found that it also called a merchant a guest in his own home town, and a governor a guest in the
// town he administers. Wrong one way, a visitor claims the hall; wrong the other, a resident speaks as a stranger at home.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LocationStandingPolicyTests
    {
        // STAKE: the note's first purpose (tester report): a lord visiting another lord's town must not call the hall his.
        [Test]
        public void GIVEN_a_visitor_in_another_lords_town_WHEN_noted_THEN_he_is_a_guest_there()
        {
            LocationStandingPolicy.Note(new LocationStandingFacts {PlaceName = "Pravend", OwnerName = "Derthert", OwnerClanName = "dey Meroc"})
                .Should().Contain("belongs to Derthert of clan dey Meroc").And.Contain("as a guest");
        }

        // STAKE: Goeth's merchant: a notable in his home town was told he was a guest there. He lives there, under its lord.
        [Test]
        public void GIVEN_a_notable_in_his_home_town_WHEN_noted_THEN_he_lives_there_under_its_lord_and_is_no_guest()
        {
            string note = LocationStandingPolicy.Note(new LocationStandingFacts {PlaceName = "Pravend", OwnerName = "Derthert", OwnerClanName = "dey Meroc", LivesHere = true});

            note.Should().Contain("your home").And.Contain("belongs to Derthert").And.Contain("not your holding").And.Contain("no guest");
            note.Should().NotContain("as a guest");
        }

        // STAKE: VonLemberg's contract: "a governor may truthfully administer someone else's holding without being treated
        // as a visitor", and without being made its owner.
        [Test]
        public void GIVEN_a_governor_of_another_clans_town_WHEN_noted_THEN_he_governs_it_for_its_owner()
        {
            string note = LocationStandingPolicy.Note(new LocationStandingFacts {PlaceName = "Pravend", OwnerName = "Derthert", Governs = true});

            note.Should().Contain("You govern it in their name").And.Contain("not your holding");
            note.Should().NotContain("guest");
        }

        // STAKE: "the actual owner/owning clan is recognized as such" (VonLemberg): nothing changes for the owner's kin.
        [Test]
        public void GIVEN_the_owning_clan_WHEN_noted_THEN_it_is_their_own_holding()
        {
            LocationStandingPolicy.Note(new LocationStandingFacts {PlaceName = "Pravend", OwnClanHolds = true, OwnerName = "Derthert"})
                .Should().Be("You are in Pravend, a holding of your own clan.");
        }

        // STAKE: in the player's own town the speaker must know the person before them is its lord (player report); a
        // governor there administers it in the player's name.
        [Test]
        public void GIVEN_the_player_holds_the_town_WHEN_noted_THEN_the_player_is_named_its_lord()
        {
            LocationStandingPolicy.Note(new LocationStandingFacts {PlaceName = "Pravend", PlayerHolds = true}).Should().Contain("they are its lord");
            LocationStandingPolicy.Note(new LocationStandingFacts {PlaceName = "Pravend", PlayerHolds = true, Governs = true}).Should().Contain("you govern for the player");
        }

        // STAKE: no settlement, no note: nothing is invented about a place the speaker is not in.
        [Test]
        public void GIVEN_no_settlement_WHEN_noted_THEN_nothing_is_said()
        {
            LocationStandingPolicy.Note(new LocationStandingFacts()).Should().BeNull();
        }
    }
}
