// Code written by Gabriel Mailhot, 08/09/2026.
// Guards RoadAudienceSafetyPolicy.IsSafeForPrivateWord, the gate the NEW road trigger applies before offering a
// "private word" popup while travelling (player report, 2026-09-08: companions never speak up on the road,
// only on entering a settlement). Unlike the settlement trigger, a road check runs on a plain daily tick, not a
// deliberate arrival moment, so it must positively rule out every state Gabriel named as off-limits: a battle,
// a siege, an ongoing conversation or mission, a player who is a prisoner, or a party that is not actually
// travelling (sitting inside a settlement is the settlement trigger's own moment, never the road's).

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
        // A perfectly ordinary day on the open road, nothing contested, nobody talking: the whole point of the
        // road trigger is that THIS moment can offer a private word.
        [Test]
        public void GIVEN_free_travel_on_the_open_road_WHEN_gating_the_road_audience_THEN_the_offer_may_fire()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeTrue("an ordinary day of travel is exactly where the road trigger belongs");
        }

        // Gabriel: "at minimum do not interrupt a battle". A loaded 3D scene (battle, siege assault, duel) is
        // never a moment to pop an inquiry over.
        [Test]
        public void GIVEN_a_mission_scene_is_loaded_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: true, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();
        }

        // Gabriel: "do not interrupt ... an ongoing conversation". A companion popup queued behind a live
        // vanilla dialogue is the exact same failure the settlement path already fixed for a coinciding
        // conversation (2026-09-05): it reads as "nothing happened when I answered".
        [Test]
        public void GIVEN_a_vanilla_conversation_is_already_under_way_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: true, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();
        }

        // Gabriel: "do not interrupt ... a player who is a prisoner". No companion pulls the player aside for a
        // private word while both are held captive.
        [Test]
        public void GIVEN_the_player_is_a_prisoner_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: true, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();
        }

        // Sitting inside a settlement is not "the road": that is the settlement trigger's own moment
        // (CompanionAudienceTopicBehavior.OnSettlementEntered), and letting the road check ALSO fire there
        // would double up the same feature through two doors on the same visit.
        [Test]
        public void GIVEN_the_party_is_sitting_inside_a_settlement_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: true,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();
        }

        // Gabriel: "do not interrupt a battle, a siege". A map event (battle/raid) or an ongoing siege is a
        // hostile moment even before a 3D scene has loaded (e.g. the strategic siege state between assaults).
        [Test]
        public void GIVEN_a_map_event_or_a_siege_is_under_way_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: true, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: true, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();
        }

        // An encounter resolving, or a campaign menu open (village/prisoner/quest menus and the like), is a
        // transitional state where an inquiry popup on top would confuse or crash the underlying flow, exactly
        // the same class of hazard the settlement path's own timing guard exists for.
        [Test]
        public void GIVEN_an_encounter_is_resolving_or_a_menu_is_open_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: true, inMenu: false,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: true,
                    chatOpen: false, chatStaging: false)
                .Should().BeFalse();
        }

        // The CR chat is already open, or a CR conversation scene (or the council's own) is mid-stage: a second
        // popup competing for the same overlay is exactly the "million popups" class of bug the settlement path
        // guards against for its own conversation/mission coincidence.
        [Test]
        public void GIVEN_the_chat_is_already_open_or_staging_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: true, chatStaging: false)
                .Should().BeFalse();

            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: false, inConversation: false, playerIsPrisoner: false, inSettlement: false,
                    inMapEvent: false, inSiegeEvent: false, inEncounter: false, inMenu: false,
                    chatOpen: false, chatStaging: true)
                .Should().BeFalse();
        }

        // No AND-shaped hole where several hostilities would cancel out: every flag blocks independently.
        [Test]
        public void GIVEN_every_hazard_at_once_WHEN_gating_the_road_audience_THEN_the_offer_is_blocked()
        {
            RoadAudienceSafetyPolicy.IsSafeForPrivateWord(
                    inMission: true, inConversation: true, playerIsPrisoner: true, inSettlement: true,
                    inMapEvent: true, inSiegeEvent: true, inEncounter: true, inMenu: true,
                    chatOpen: true, chatStaging: true)
                .Should().BeFalse();
        }
    }
}
