// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A (pure): whether a courtship-rivalry duel is even a real triangle worth staging. This is
// a pure predicate: the host resolves each fact from the live courtship, jealousy and personality systems, and
// this policy only judges. All three facts must hold at once, or the duel is nonsense: no courtship means
// nobody to fight over, no genuine rival desire means no rival, and no boldness means a timid rival would
// never force the matter with steel in the first place.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>Whether a rival mounts a courtship-rivalry duel over a hero the player courts.</summary>
   public static class CourtshipRivalryEligibility
   {
      /// <summary>A rival mounts a courtship duel only when all three hold: the player is actually courting
      /// her, the rival genuinely desires her too, and the rival is bold enough to force the matter with
      /// steel.</summary>
      public static bool ShouldChallenge(bool playerIsCourtingHer, bool rivalDesiresHer, bool rivalIsBoldEnough)
         => playerIsCourtingHer && rivalDesiresHer && rivalIsBoldEnough;
   }
}
