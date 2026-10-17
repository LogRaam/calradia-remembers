// Code written by Gabriel Mailhot, 08/10/2026.
// Whether closing a CR conversation on the map also finishes the player's encounter (so no army menu lingers). Four
// guards stood in ChatManager; Maifailun's crash (Nexus, 08/10/2026) added a fifth: a battle under way in the encounter.
// Finishing it settled the fight mid-close, and the game then drew its map conversation with a leader no longer there.
// Zivkyn (10/10/2026): a raid counted as that battle and left the raider on the encounter menu; a raid is finished as
// before 2.7.5.
// Wallace1067 (Nexus, 13/10/2026): a friendly lord standing alone, no battle anywhere, still kept him on the vanilla
// encounter menu after every chat. The guard counted any attached MapEvent, live or settled; the likely cause (not
// reproduced, his log said only "a battle is under way") is one already finalized or already won when the chat
// closed. BattleUnderWay now asks IsLive, not merely whether an event is attached, and the log names which one.

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

      /// <summary>
      ///   That battle is the player's raid on a village (Zivkyn, 10/10/2026): no fight to settle, and the game's own raid
      ///   branch ends it cleanly, so the close finishes the encounter as it did before 2.7.5.
      /// </summary>
      public bool BattleIsRaid { get; init; }
   }

   /// <summary>A map event as the close saw it, for one of the three places a battle can hide (the encounter itself,
   /// the player's own party, or the party met). Built even when there is no event at all, so a missing one reads as
   /// facts rather than a null check scattered at every call site.</summary>
   public sealed class MapEventSighting
   {
      /// <summary>A MapEvent is actually attached here (null otherwise: nothing to settle).</summary>
      public bool Exists { get; init; }

      /// <summary>The event reached WaitingRemoval: the game is done with it, only the cleanup is pending.</summary>
      public bool IsFinalized { get; init; }

      /// <summary>The event already has a winner: the fight is decided, nothing left for the game to settle.</summary>
      public bool HasWinner { get; init; }
   }

   /// <summary>
   ///   What the close knows when it ends the conversation, to tell the game whether the meeting is over (Wallace1067,
   ///   17/10/2026, see <see cref="MapEncounterClosePolicy.LeavesTheMeeting" />).
   /// </summary>
   public sealed class MeetingLeaveFacts
   {
      /// <summary>The player has an encounter open.</summary>
      public bool HasEncounter { get; init; }

      /// <summary>The encounter is at a settlement (a town, castle or village and its menus): not a meeting on the road.</summary>
      public bool AtASettlement { get; init; }

      /// <summary>This close is a companion briefing, a step inside a meeting that goes on.</summary>
      public bool BriefingReturnPending { get; init; }

      /// <summary>The conversation was with the party the player met (the engine's ConversationParty is the encountered party).</summary>
      public bool TalkedWithTheMetParty { get; init; }

      /// <summary>The party met no longer exists or is no longer on the map (disbanded mid-talk, as a recalled errand party was).</summary>
      public bool MetPartyGone { get; init; }

      /// <summary>The party met is at war with the player's side: the player may still want to attack.</summary>
      public bool AtWarWithTheMetParty { get; init; }
   }

   /// <summary>Whether the close may finish the encounter. Pure.</summary>
   public static class MapEncounterClosePolicy
   {
      /// <summary>
      ///   Whether a sighted MapEvent is still a live fight the game must be left to settle. Wallace1067 (Nexus,
      ///   13/10/2026): a friendly lord standing alone on open ground left him on the vanilla encounter menu after
      ///   every chat, because an event that had already finalized (or already had a winner) still read as "a battle
      ///   under way". Maifailun (08/10/2026) is the other side of the same line: a fight genuinely under way, even
      ///   one the player's own party was not part of, must still be left for the game to settle, or finishing the
      ///   encounter settles it mid-close.
      /// </summary>
      public static bool IsLive(MapEventSighting e) => e != null && e.Exists && !e.IsFinalized && !e.HasWinner;

      /// <summary>
      ///   Whether ending the conversation also ends the meeting, as the game's own "leave" line does
      ///   (LordConversationsCampaignBehavior.conversation_lord_leave_on_consequence sets PlayerEncounter.LeaveEncounter).
      ///   Wallace1067 (Nexus, 17/10/2026, on 2.7.12): CR never told the game the meeting was over, so when the game
      ///   returned to its meeting menu it started a battle with the party met (EncounterGameMenuBehavior,
      ///   encounter_meeting on init: meeting done, not leaving, so StartBattle). With a friendly lord that left him on the
      ///   encounter menu after every chat; with a companion recalled from an errand, whose party was gone, the battle had
      ///   nobody in it and no way out. A party at war is left to the encounter menu, where the player can still attack.
      /// </summary>
      public static bool LeavesTheMeeting(MeetingLeaveFacts f)
         => f != null && f.HasEncounter && !f.AtASettlement && !f.BriefingReturnPending
            && (f.MetPartyGone || (f.TalkedWithTheMetParty && !f.AtWarWithTheMetParty));

      public static bool MayFinish(MapEncounterCloseFacts f)
         => f != null && f.HasEncounter && !f.InMission && !f.InsideSettlement && !f.BriefingReturnPending && (!f.BattleUnderWay || f.BattleIsRaid);
   }
}
