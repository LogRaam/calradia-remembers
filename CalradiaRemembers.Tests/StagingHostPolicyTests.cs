// Code written by Gabriel Mailhot, 28/09/2026.
// Where a CR conversation opens. Gabriel's rule: never a screen over the LIVE map, which keeps ticking underneath (the
// siege-bombardment native crash, the stolen hotkeys, the VRAM leak). A conversation opens over a live scene, or a
// scene is staged for it. At sea no scene can load (the endless loading screen of 1.30.16), and until 2.6.7 the chat
// then opened straight over the naval map. Windmolen (Nexus, 28/09/2026): "when on the move, if I accept the envoy's
// invitation, the chat window opens but flickers a few seconds and then my game crashes. The last time it was a naval
// move." At sea, a conversation now waits for landfall; and an envoy, who rides, does not arrive until then.
// On land, an invitation answered while another conversation holds the screen now waits for it (Lordfadooboo,
// 21/10/2026: Zorika's word accepted while Radagos came up, "endless loading screen"; Maifailun, 22/10/2026: another
// mod's popup at the same time as CR's, "the loading screen is infinite"). If these rules break, the stager opens a
// second scene over the first again, or lets a stale invitation open in another town.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class StagingHostPolicyTests
   {
      // Windmolen's crash: at sea, with nothing live behind it, the chat must not open over the naval map.
      [Test]
      public void GIVEN_the_player_at_sea_with_no_scene_live_WHEN_a_conversation_is_asked_for_THEN_it_waits_for_landfall()
      {
         StagingHostPolicy.Route(new StagingHostFacts {PlayerAtSea = true}).Should().Be(StagingRoute.WaitForLandfall);
      }

      // On land a scene is staged, as it always was: nothing changes there.
      [Test]
      public void GIVEN_the_player_on_land_with_no_scene_live_WHEN_a_conversation_is_asked_for_THEN_a_scene_is_staged()
      {
         StagingHostPolicy.Route(new StagingHostFacts()).Should().Be(StagingRoute.StageAScene);
      }

      // A scene already live is a safe host anywhere, at sea included (a naval battle's mission): no reason to wait.
      [Test]
      public void GIVEN_a_scene_already_live_WHEN_a_conversation_is_asked_for_THEN_it_opens_over_it_even_at_sea()
      {
         StagingHostPolicy.Route(new StagingHostFacts {PlayerAtSea = true, MissionLive = true}).Should().Be(StagingRoute.OpenOverLiveHost);
         StagingHostPolicy.Route(new StagingHostFacts {PartnerConversationLive = true}).Should().Be(StagingRoute.OpenOverLiveHost);
      }

      // A waiting conversation is received once the player is ashore and nothing else holds the screen; received in
      // the middle of another scene or chat, it would collide with it (the "dropped" path the stager already reports).
      [Test]
      public void GIVEN_a_conversation_waiting_for_landfall_WHEN_asked_whether_it_may_be_received_THEN_only_ashore_and_with_the_screen_free()
      {
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {HoursAshore = StagingHostPolicy.LandfallSettleHours}).Should().BeTrue();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {PlayerAtSea = true}).Should().BeFalse();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {MissionLive = true}).Should().BeFalse();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {ChatOpen = true}).Should().BeFalse();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {AnotherSceneStaging = true}).Should().BeFalse();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {ConversationLive = true}).Should().BeFalse();
      }

      // CarolusIV (Nexus, 05/10/2026): crashes when a companion wants to talk while he sails into a port. The waiting
      // conversation was received the very frame the ship touched the quay, while the game was still starting the
      // town's encounter, the engine state that has crashed natively under staged audiences before. It now waits until
      // the player has been ashore a while, and never while an encounter is still being set up.
      [Test]
      public void GIVEN_the_ship_just_docked_WHEN_a_waiting_conversation_would_be_received_THEN_it_waits_for_the_landing_to_settle()
      {
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {HoursAshore = 0}).Should().BeFalse();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {HoursAshore = StagingHostPolicy.LandfallSettleHours}).Should().BeTrue();
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {HoursAshore = StagingHostPolicy.LandfallSettleHours, EncounterStarting = true}).Should().BeFalse();
      }

      // Lordfadooboo's endless loading screen: he answered Zorika's word while Radagos's conversation was coming up,
      // and the door staged its own scene over it. An answered invitation now waits while another conversation is live,
      // while another CR scene is staging, or while some other screen is on its way in.
      [Test]
      public void GIVEN_another_conversation_holding_the_screen_WHEN_the_player_answers_an_invitation_THEN_it_waits_for_the_screen()
      {
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true, OtherConversationLive = true}).Should().Be(StagingRoute.WaitForTheScreen);
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true, OtherConversationLive = true, MissionLive = true}).Should().Be(StagingRoute.WaitForTheScreen);
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true, AnotherSceneStaging = true}).Should().Be(StagingRoute.WaitForTheScreen);
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true, ScreenElsewhere = true}).Should().Be(StagingRoute.WaitForTheScreen);
      }

      // The screen free, an answered invitation opens as it always did: walking in town it opens over the scene, on the
      // map a scene is staged. And a conversation already live with the very person who asked is still its host.
      [Test]
      public void GIVEN_the_screen_free_or_the_partner_already_talking_WHEN_the_player_answers_an_invitation_THEN_it_opens_as_before()
      {
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true}).Should().Be(StagingRoute.StageAScene);
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true, MissionLive = true}).Should().Be(StagingRoute.OpenOverLiveHost);
         StagingHostPolicy.Route(new StagingHostFacts {Invited = true, PartnerConversationLive = true, OtherConversationLive = true}).Should().Be(StagingRoute.OpenOverLiveHost);
      }

      // A captive scene, a duel's aftermath or a briefed return comes from what just happened and cannot be put off: it
      // keeps the door's older answer, so this fix changes nothing for them.
      [Test]
      public void GIVEN_a_scene_that_is_not_an_invitation_WHEN_another_conversation_is_live_THEN_it_is_not_held()
      {
         StagingHostPolicy.Route(new StagingHostFacts {OtherConversationLive = true}).Should().Be(StagingRoute.StageAScene);
         StagingHostPolicy.Route(new StagingHostFacts {OtherConversationLive = true, MissionLive = true}).Should().Be(StagingRoute.OpenOverLiveHost);
      }

      // A held invitation is received only soon and where it was answered: in another town or hours later, the person
      // who asked is no longer beside the player, and the scene would open on a settlement they have left.
      [Test]
      public void GIVEN_an_invitation_held_for_the_screen_WHEN_the_screen_frees_THEN_it_is_received_only_soon_and_where_it_was_answered()
      {
         StagingHostPolicy.StillWorthReceiving(new ScreenHoldFacts {HoursHeld = 0, StillWhereAnswered = true}).Should().BeTrue();
         StagingHostPolicy.StillWorthReceiving(new ScreenHoldFacts {HoursHeld = StagingHostPolicy.ScreenHoldHours, StillWhereAnswered = true}).Should().BeTrue();
         StagingHostPolicy.StillWorthReceiving(new ScreenHoldFacts {HoursHeld = StagingHostPolicy.ScreenHoldHours + 0.5, StillWhereAnswered = true}).Should().BeFalse();
         StagingHostPolicy.StillWorthReceiving(new ScreenHoldFacts {HoursHeld = 0, StillWhereAnswered = false}).Should().BeFalse();
      }

      // A held conversation is not received while another screen is still on its way in.
      [Test]
      public void GIVEN_another_screen_on_its_way_in_WHEN_a_held_conversation_would_be_received_THEN_it_keeps_waiting()
      {
         StagingHostPolicy.MayReceiveWaiting(new LandfallFacts {HoursAshore = StagingHostPolicy.LandfallSettleHours, ScreenElsewhere = true}).Should().BeFalse();
      }

      // The envoy "rides up under a flag of truce": at sea he cannot, so he does not arrive until the player lands.
      [Test]
      public void GIVEN_the_player_at_sea_WHEN_an_envoy_would_arrive_THEN_he_waits_for_landfall()
      {
         StagingHostPolicy.AnEnvoyCanReachThePlayer(playerAtSea: true).Should().BeFalse();
         StagingHostPolicy.AnEnvoyCanReachThePlayer(playerAtSea: false).Should().BeTrue();
      }
   }
}
