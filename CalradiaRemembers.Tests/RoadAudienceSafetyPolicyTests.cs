// Code written by Gabriel Mailhot, 08/09/2026, reworked 10/09/2026.
// What this pins: when a travelling companion may interrupt the player to ask for a private word, and, far more
// often, when they may not.
//
// Why it exists. A player (Nexus, 2026-09-08) said their companions never spoke up unless a settlement was
// entered, so anyone who lives on the road heard nothing from them, ever. Gabriel's ruling added a ROAD trigger.
// But a road check runs on a plain daily tick rather than a deliberate arrival, so it can land in the middle of
// far more states than "just walked into a town" ever could: mid-battle, mid-siege, inside a menu, in a cell.
// Every rule below is one of those moments.
//
// WHY EACH TEST NOW NAMES ONE FACT. Until today this policy took ELEVEN booleans in a row, and every test spelled
// out all eleven to say something about one of them. That is unsafe (swap any two same-typed arguments and
// nothing complains) and it is unreadable (ten irrelevant falses per rule). Gabriel, 10/09/2026: "Le code doit
// pouvoir changer, evoluer, s'ameliorer, mais le comportement attendu ne doit jamais se degrader." A facts object
// buys exactly that: each test states the single hazard it is about, so the twelfth hazard will cost none of them
// anything.
//
// If these tests fail, a companion is about to tap the player on the shoulder in the middle of a siege.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RoadAudienceSafetyPolicyTests
    {
        /// <summary>An ordinary day of travel: nothing contested, nobody talking. Each test spoils exactly one fact.</summary>
        private static RoadAudienceFacts AnOrdinaryDayOnTheRoad() => new();

        // The whole point of the road trigger: THIS moment can offer a private word. Everything else here is a
        // moment that cannot, so this is the one case proving they are not all blocked.
        [Test]
        public void GIVEN_free_travel_on_the_open_road_WHEN_gating_the_road_audience_THEN_the_offer_may_fire()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(AnOrdinaryDayOnTheRoad())
                .Should().BeTrue("an ordinary day of travel is exactly where the road trigger belongs");
        }

        // Gabriel: "at minimum do not interrupt a battle." A loaded 3D scene (battle, siege assault, duel) is
        // never a moment to pop an inquiry over.
        [Test]
        public void GIVEN_a_scene_is_loaded_WHEN_gating_the_road_audience_THEN_nobody_interrupts_it()
        {
            RoadAudienceFacts facts = AnOrdinaryDayOnTheRoad();
            facts.InMission = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(facts).Should().BeFalse();
        }

        // Somebody is already talking to the player. A second conversation offered over the first is the popup
        // cascade the deferred-open mechanism exists to prevent, arriving by another door.
        [Test]
        public void GIVEN_a_conversation_is_already_under_way_WHEN_gating_THEN_nobody_talks_over_it()
        {
            RoadAudienceFacts facts = AnOrdinaryDayOnTheRoad();
            facts.InConversation = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(facts).Should().BeFalse();
        }

        // A captive has no private word to give: there is a captor in the room, and the whole premise of the
        // offer is privacy.
        [Test]
        public void GIVEN_the_player_is_somebody_s_prisoner_WHEN_gating_THEN_no_private_word_is_possible()
        {
            RoadAudienceFacts facts = AnOrdinaryDayOnTheRoad();
            facts.PlayerIsPrisoner = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(facts).Should().BeFalse();
        }

        // Inside a settlement is the SETTLEMENT trigger's moment. Letting the road path fire there too would
        // double every offer and burn the shared cooldown twice on one arrival.
        [Test]
        public void GIVEN_the_player_is_inside_a_settlement_WHEN_gating_THEN_the_road_path_leaves_it_alone()
        {
            RoadAudienceFacts facts = AnOrdinaryDayOnTheRoad();
            facts.InSettlement = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(facts).Should().BeFalse();
        }

        // The three ways the player can already be committed to something on the map. Each blocks on its own,
        // because any one of them means the next thing they expect to see is not an inquiry about feelings.
        [Test]
        public void GIVEN_the_player_is_committed_to_something_on_the_map_WHEN_gating_THEN_the_offer_waits()
        {
            RoadAudienceFacts inBattle = AnOrdinaryDayOnTheRoad();
            inBattle.InMapEvent = true;
            RoadAudienceFacts besieging = AnOrdinaryDayOnTheRoad();
            besieging.InSiegeEvent = true;
            RoadAudienceFacts meeting = AnOrdinaryDayOnTheRoad();
            meeting.InEncounter = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(inBattle).Should().BeFalse();
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(besieging).Should().BeFalse();
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(meeting).Should().BeFalse();
        }

        // A campaign menu is open, so the player is reading something. An inquiry raised over it lands on top of
        // their own choice.
        [Test]
        public void GIVEN_a_campaign_menu_is_open_WHEN_gating_THEN_nothing_is_raised_over_it()
        {
            RoadAudienceFacts facts = AnOrdinaryDayOnTheRoad();
            facts.InMenu = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(facts).Should().BeFalse();
        }

        // The mod's own two states. A chat already open, or one being staged, means a conversation is happening
        // or about to; a second offer on top of it is how two windows end up on screen together.
        [Test]
        public void GIVEN_the_mod_is_already_running_a_conversation_WHEN_gating_THEN_it_does_not_start_another()
        {
            RoadAudienceFacts chatting = AnOrdinaryDayOnTheRoad();
            chatting.ChatOpen = true;
            RoadAudienceFacts staging = AnOrdinaryDayOnTheRoad();
            staging.ChatStaging = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(chatting).Should().BeFalse();
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(staging).Should().BeFalse();
        }

        // Crash report, 2026-09-10: "My companion wanted a word at sea and it kept open the window to choose not
        // now or have a word while simultaneously opening the chat window. When we had our conversation, i ended
        // it, and it caused a crash." Not one fact above is true on open water, so the offer sailed through every
        // guard there was and staged a conversation on a screen no CR conversation has ever been staged on.
        [Test]
        public void GIVEN_the_player_is_sailing_WHEN_gating_THEN_no_companion_asks_for_a_private_word()
        {
            RoadAudienceFacts facts = AnOrdinaryDayOnTheRoad();
            facts.AtSea = true;

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(facts).Should().BeFalse();
        }

        // Facts we could not read are not permission. This runs unattended every day, so a broken read costs one
        // quiet day, whereas failing open risks popping an inquiry mid-battle on the one day the read breaks.
        [Test]
        public void GIVEN_nothing_could_be_read_about_the_moment_WHEN_gating_THEN_the_offer_is_withheld()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(null).Should().BeFalse();
        }
    }
}
