// Code written by Gabriel Mailhot, 22/07/2026.
// DuelVenuePolicy.Choose answers where a duel WOULD be held; this answers whether the game can actually open
// that fight right now. The two are separate on purpose: a venue the design has ratified is not the same thing
// as a venue with a built mission, and teaching the challenge_duel verb for a fight the game cannot open is
// exactly the empty-promise class of bug this codebase keeps closing (see join_party's ANTI-EMPTY-PROMISE rule
// in EncounterContextBuilder).
// Both real venues are now built: Wilderness through DuelMissionLauncher.StartWildernessDuel, and TownGates
// through TownDuelBehavior, which reuses the street ambush's proven town-centre injection recipe. NotNow is the
// player at sea, where there is no field to stand on at all, so it stays false by definition rather than by
// missing code.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>Whether a chosen <see cref="DuelVenue" /> can actually be staged by the game RIGHT NOW.</summary>
   public static class DuelStagingSupport
   {
      /// <summary>
      ///   True for both real venues (<see cref="DuelVenue.Wilderness" /> and <see cref="DuelVenue.TownGates" />),
      ///   each of which has a built mission path. <see cref="DuelVenue.NotNow" /> is false and always will be:
      ///   it does not mean "not built yet", it means the player is at sea and there is no ground to fight on.
      /// </summary>
      public static bool CanStage(DuelVenue venue)
         => venue == DuelVenue.Wilderness || venue == DuelVenue.TownGates;

      /// <summary>
      ///   Whether a TOWN duel can be held where the player stands. Both halves are required and only one of
      ///   them was ever asked.
      ///   <para>
      ///     yozakura12 (Nexus, 17 and 18/09/2026, still crashing on 2.5.10): his log reads
      ///     <c>venue=TownGates</c>, then "opening the town centre", then nothing. The conversation had ended
      ///     with "returned to campaign map": he was AT a town, on the map, not inside it. Both predicates in
      ///     play asked only whether a settlement was there and whether it owned a "center" location, and a
      ///     settlement the player is merely standing on answers yes to both. Opening a settlement LOCATION
      ///     mission from outside is a state the engine does not support, and it dies loading the scene.
      ///   </para>
      ///   <para>
      ///     The same lesson ChatSceneStager learned from the CarolusIV/Windmolen crashes: inside a settlement
      ///     and at a settlement are different places, and only PlayerEncounter.InsideSettlement tells them
      ///     apart. The duel path never got it.
      ///   </para>
      ///   When this is false at a town the answer is not "no duel": it is the open field, which is where a
      ///   man standing outside the walls would fight anyway.
      /// </summary>
      public static bool CanHoldATownDuel(bool playerIsInsideTheSettlement, bool theSettlementHasADuellingGround)
         => playerIsInsideTheSettlement && theSettlementHasADuellingGround;

      /// <summary>Frames to let the town menu settle after the walkabout mission ends, before the square is opened.</summary>
      public const int SettleFramesBeforeTheSquare = 30;

      /// <summary>
      ///   Whether the town square may be opened for a duel now (Zivkyn 23/09, Maifailun 29/09/2026: crashes inside some
      ///   settlements, not others). A duel called from a keep or a lord's hall ends that mission first; the square waits
      ///   until the town menu is really back and a few frames have passed, instead of opening on the first frame the
      ///   old mission is gone. Defensive: no log has yet confirmed this is the crash.
      /// </summary>
      ///   <para>
      ///     Thoragoros1 (08/10/2026), reproduced by Gabriel: the settling was counted from the last mission only, which
      ///     had ended long before a conversation held over the town menu, so a duel taken up there opened the square in
      ///     the very second the chat closed, while the game's map conversation was still unwinding, and the engine
      ///     crashed loading the scene. It now also waits for the game's own conversation to be over and counts from the
      ///     chat's close.
      ///   </para>
      public static bool ReadyToOpenTheSquare(SquareReadinessFacts f)
         => f != null && f.TownMenuActive && !f.GameConversationOpen
            && f.FramesSinceMissionEnded >= SettleFramesBeforeTheSquare && f.FramesSinceChatClosed >= SettleFramesBeforeTheSquare;

      /// <summary>
      ///   Whether an agent must stand aside when a town duel starts (Maifailun, 29/09/2026: "my companions were also in
      ///   the fight"). The game's fight handler makes the whole player team an enemy of the opponent's, and companions
      ///   following the player are on it: everyone on that team but the player stands aside.
      /// </summary>
      public static bool StandsAsideFromTheDuel(bool isHuman, bool isThePlayer, bool isTheOpponent, bool onThePlayersTeam)
         => isHuman && !isThePlayer && !isTheOpponent && onThePlayersTeam;
   }

   /// <summary>What the town square's opening waits on.</summary>
   public sealed class SquareReadinessFacts
   {
      /// <summary>The town menu is back on the map.</summary>
      public bool TownMenuActive { get; init; }

      /// <summary>Frames since the last mission (a walkabout ended for the duel) was gone.</summary>
      public int FramesSinceMissionEnded { get; init; }

      /// <summary>Frames since CR's chat closed.</summary>
      public int FramesSinceChatClosed { get; init; }

      /// <summary>The game's own conversation (the map conversation the chat was held over) is still in progress.</summary>
      public bool GameConversationOpen { get; init; }
   }
}
