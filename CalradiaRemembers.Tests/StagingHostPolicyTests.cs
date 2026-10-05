// Code written by Gabriel Mailhot, 28/09/2026.
// Where a CR conversation opens. Gabriel's rule: never a screen over the LIVE map, which keeps ticking underneath (the
// siege-bombardment native crash, the stolen hotkeys, the VRAM leak). A conversation opens over a live scene, or a
// scene is staged for it. At sea no scene can load (the endless loading screen of 1.30.16), and until 2.6.7 the chat
// then opened straight over the naval map. Windmolen (Nexus, 28/09/2026): "when on the move, if I accept the envoy's
// invitation, the chat window opens but flickers a few seconds and then my game crashes. The last time it was a naval
// move." At sea, a conversation now waits for landfall; and an envoy, who rides, does not arrive until then.

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

      // The envoy "rides up under a flag of truce": at sea he cannot, so he does not arrive until the player lands.
      [Test]
      public void GIVEN_the_player_at_sea_WHEN_an_envoy_would_arrive_THEN_he_waits_for_landfall()
      {
         StagingHostPolicy.AnEnvoyCanReachThePlayer(playerAtSea: true).Should().BeFalse();
         StagingHostPolicy.AnEnvoyCanReachThePlayer(playerAtSea: false).Should().BeTrue();
      }
   }
}
