// Code written by Gabriel Mailhot, 08/10/2026.
// Whether closing a CR conversation on the map also finishes the player's encounter (so no army menu lingers). Four
// guards stood in ChatManager; Maifailun's crash (Nexus, 08/10/2026) added a fifth: a battle under way in the encounter.
// Finishing it settled the fight mid-close, and the game then drew its map conversation with a leader no longer there.

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>What the close knows of the player's encounter.</summary>
   public sealed class MapEncounterCloseFacts
   {
      /// <summary>The player has an encounter open.</summary>
      public bool HasEncounter { get; init; }

      /// <summary>A scene or mission is running: not a map talk.</summary>
      public bool InMission { get; init; }

      /// <summary>A town or castle encounter: left intact.</summary>
      public bool InsideSettlement { get; init; }

      /// <summary>This close is a companion briefing, a step inside a meeting that goes on.</summary>
      public bool BriefingReturnPending { get; init; }

      /// <summary>A battle is under way in the encounter (the player's, or the party met): the game's to settle.</summary>
      public bool BattleUnderWay { get; init; }
   }

   /// <summary>Whether the close may finish the encounter. Pure.</summary>
   public static class MapEncounterClosePolicy
   {
      public static bool MayFinish(MapEncounterCloseFacts f)
         => f != null && f.HasEncounter && !f.InMission && !f.InsideSettlement && !f.BriefingReturnPending && !f.BattleUnderWay;
   }
}
