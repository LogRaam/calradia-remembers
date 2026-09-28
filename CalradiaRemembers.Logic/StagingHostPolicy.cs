// Code written by Gabriel Mailhot, 28/09/2026.
// Where a CR conversation opens (ChatSceneStager's door). Never over the LIVE map, which keeps ticking underneath: over
// a scene already live, or over one staged for it. At sea no scene can load, so the conversation WAITS FOR LANDFALL
// rather than opening over the naval map, as it did until 2.6.7 (Windmolen, Nexus, 28/09/2026: the envoy's chat
// flickered a few seconds on a naval move, then the game crashed). An envoy, who rides to the player, does not arrive
// at sea at all.

namespace CalradiaRemembers.Logic
{
   /// <summary>Where the door sends a conversation.</summary>
   public enum StagingRoute
   {
      /// <summary>A scene (or the partner's own conversation) is already live: open over it.</summary>
      OpenOverLiveHost,

      /// <summary>Nothing live and the player on land: stage a conversation scene first.</summary>
      StageAScene,

      /// <summary>Nothing live and the player at sea, where no scene can load: hold it until landfall.</summary>
      WaitForLandfall
   }

   /// <summary>What the door sees when a conversation is asked for.</summary>
   public sealed class StagingHostFacts
   {
      /// <summary>A mission is running (a staged scene, a town interior, a naval battle).</summary>
      public bool MissionLive { get; set; }

      /// <summary>A map conversation with the very person this conversation is for is already live.</summary>
      public bool PartnerConversationLive { get; set; }

      /// <summary>The player's party is at sea.</summary>
      public bool PlayerAtSea { get; set; }
   }

   /// <summary>What decides whether a conversation waiting for landfall may be received now.</summary>
   public sealed class LandfallFacts
   {
      /// <summary>The player's party is still at sea.</summary>
      public bool PlayerAtSea { get; set; }

      /// <summary>A mission is running: the player is busy in a scene.</summary>
      public bool MissionLive { get; set; }

      /// <summary>Any conversation is live.</summary>
      public bool ConversationLive { get; set; }

      /// <summary>A CR chat is already open.</summary>
      public bool ChatOpen { get; set; }

      /// <summary>Another conversation is being staged right now.</summary>
      public bool AnotherSceneStaging { get; set; }
   }

   /// <summary>The door's rules. Pure.</summary>
   public static class StagingHostPolicy
   {
      /// <summary>Where a conversation asked for now should open.</summary>
      public static StagingRoute Route(StagingHostFacts facts)
      {
         if (facts == null) return StagingRoute.StageAScene;
         if (facts.MissionLive || facts.PartnerConversationLive) return StagingRoute.OpenOverLiveHost;

         return facts.PlayerAtSea ? StagingRoute.WaitForLandfall : StagingRoute.StageAScene;
      }

      /// <summary>Whether a conversation held for landfall may be received now: ashore, and nothing else holding the screen.</summary>
      public static bool MayReceiveWaiting(LandfallFacts facts)
         => facts != null && !facts.PlayerAtSea && !facts.MissionLive && !facts.ConversationLive && !facts.ChatOpen && !facts.AnotherSceneStaging;

      /// <summary>Whether an envoy can ride up to the player now: not while the player is at sea.</summary>
      public static bool AnEnvoyCanReachThePlayer(bool playerAtSea) => !playerAtSea;
   }
}
