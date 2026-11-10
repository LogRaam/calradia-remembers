// Code written by Gabriel Mailhot, 28/09/2026.
// Where a CR conversation opens (ChatSceneStager's door). Never over the LIVE map, which keeps ticking underneath: over
// a scene already live, or over one staged for it. At sea no scene can load, so the conversation WAITS FOR LANDFALL
// rather than opening over the naval map, as it did until 2.6.7 (Windmolen, Nexus, 28/09/2026: the envoy's chat
// flickered a few seconds on a naval move, then the game crashed). An envoy, who rides to the player, does not arrive
// at sea at all.
// On land, an invitation the player accepts WAITS FOR THE SCREEN while another conversation holds it (Lordfadooboo,
// Nexus, 21/10/2026: in Sibir "a sturgian noble zorika tried to talk to me and so did radagos. this triggered an
// endless loading screen when i hit the button to talk to zorika"; Maifailun, 22/10/2026: "When another mod pops
// something up (in this case Affairs of Calradia's - this companion wants to talk to you) and CR's own popup shows up
// together, the loading screen is infinite"). The door checked for a live conversation only when the popup was
// raised, never when it was answered, and staged its scene over the other one. Gabriel, 09/11/2026: "vas-y".

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
      WaitForLandfall,

      /// <summary>
      ///   An invitation the player accepted while another conversation or scene holds the screen: hold it until the
      ///   screen is free, rather than staging a second scene over the first.
      /// </summary>
      WaitForTheScreen
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

      /// <summary>
      ///   The conversation is one the player was invited to and answered (a word asked in town, a companion's audience,
      ///   an envoy, a tale's meeting): it can wait its turn. A captive scene, a duel's aftermath or a briefed return
      ///   cannot, and keeps the door's older answer.
      /// </summary>
      public bool Invited { get; set; }

      /// <summary>A conversation with someone other than this conversation's partner is live or starting.</summary>
      public bool OtherConversationLive { get; set; }

      /// <summary>Another CR conversation is being staged right now.</summary>
      public bool AnotherSceneStaging { get; set; }

      /// <summary>The screen is neither the map nor a live scene: another scene or screen is on its way in.</summary>
      public bool ScreenElsewhere { get; set; }
   }

   /// <summary>Whether a conversation held for the screen is still worth receiving.</summary>
   public sealed class ScreenHoldFacts
   {
      /// <summary>Campaign hours since it was held.</summary>
      public double HoursHeld { get; set; }

      /// <summary>The player is still where they were when they answered (the same settlement, or still outside one).</summary>
      public bool StillWhereAnswered { get; set; }
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

      /// <summary>
      ///   Campaign hours since the player's party was last seen at sea (CarolusIV, 05/10/2026: received the frame the
      ///   ship docked, while the port's encounter was still starting, the audience crashed).
      /// </summary>
      public double HoursAshore { get; set; }

      /// <summary>An encounter is under way and the player is not yet inside the settlement (a landing, a meeting on the road).</summary>
      public bool EncounterStarting { get; set; }

      /// <summary>The screen is neither the map nor a live scene: another scene or screen is on its way in.</summary>
      public bool ScreenElsewhere { get; set; }
   }

   /// <summary>The door's rules. Pure.</summary>
   public static class StagingHostPolicy
   {
      /// <summary>Where a conversation asked for now should open.</summary>
      public static StagingRoute Route(StagingHostFacts facts)
      {
         if (facts == null) return StagingRoute.StageAScene;
         if (facts.PartnerConversationLive) return StagingRoute.OpenOverLiveHost;
         if (facts.Invited && (facts.OtherConversationLive || facts.AnotherSceneStaging || facts.ScreenElsewhere)) return StagingRoute.WaitForTheScreen;
         if (facts.MissionLive) return StagingRoute.OpenOverLiveHost;

         return facts.PlayerAtSea ? StagingRoute.WaitForLandfall : StagingRoute.StageAScene;
      }

      /// <summary>Whether a conversation held for landfall may be received now: ashore, and nothing else holding the screen.</summary>
      public static bool MayReceiveWaiting(LandfallFacts facts)
         => facts != null && !facts.PlayerAtSea && !facts.MissionLive && !facts.ConversationLive && !facts.ChatOpen && !facts.AnotherSceneStaging
            && facts.HoursAshore >= LandfallSettleHours && !facts.EncounterStarting && !facts.ScreenElsewhere;

      /// <summary>
      ///   Whether a conversation held for the screen is still received once the screen is free: soon, and where the
      ///   player answered. Received in another town or a day later, the person who asked is no longer beside them.
      /// </summary>
      public static bool StillWorthReceiving(ScreenHoldFacts facts)
         => facts != null && facts.StillWhereAnswered && facts.HoursHeld <= ScreenHoldHours;

      /// <summary>TUNE: campaign hours an accepted invitation waits for the screen before it is let go (it is asked again later).</summary>
      public const int ScreenHoldHours = 2;

      /// <summary>TUNE: campaign hours ashore before a conversation held for landfall is received, so the landing settles first.</summary>
      public const int LandfallSettleHours = 1;

      /// <summary>Whether an envoy can ride up to the player now: not while the player is at sea.</summary>
      public static bool AnEnvoyCanReachThePlayer(bool playerAtSea) => !playerAtSea;
   }
}
