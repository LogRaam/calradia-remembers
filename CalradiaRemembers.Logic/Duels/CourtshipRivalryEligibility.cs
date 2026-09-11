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
      /// <summary>
      ///   What has to be true before a rival draws steel over a courtship. Three booleans in a row about
      ///   three different people's states of mind.
      ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
      ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
      ///   that names only what it is about reads as the rule it protects.
      /// </summary>
      public sealed class RivalryFacts
      {
         /// <summary>The player is actually courting her.</summary>
         public bool PlayerIsCourtingHer { get; set; }

         /// <summary>The rival genuinely wants her too.</summary>
         public bool RivalDesiresHer { get; set; }

         /// <summary>The rival is bold enough to force the matter with steel.</summary>
         public bool RivalIsBoldEnough { get; set; }
      }

      /// <summary>A rival mounts a courtship duel only when all three hold: the player is actually courting
      /// her, the rival genuinely desires her too, and the rival is bold enough to force the matter with
      /// steel.</summary>
      public static bool ShouldChallenge(RivalryFacts facts)
      {
         if (facts == null) return false;

         bool playerIsCourtingHer = facts.PlayerIsCourtingHer;
         bool rivalDesiresHer = facts.RivalDesiresHer;
         bool rivalIsBoldEnough = facts.RivalIsBoldEnough;

         return playerIsCourtingHer && rivalDesiresHer && rivalIsBoldEnough;
      }
   }
}
