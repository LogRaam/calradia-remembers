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
   }
}
