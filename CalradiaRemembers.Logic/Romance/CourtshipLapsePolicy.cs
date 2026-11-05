// Code written by Gabriel Mailhot, 04/11/2026.
// avalanche24 (Nexus, 04/11/2026): Queen Rhagaea, courted by the player since winter 1085, was wed by the game to
// another lord; her Heart page still read "You and her have been courting", her letters stayed romantic, and in chat she
// denied the courtship had ever been ("Our paths have never been those of courtship"). Nothing in CR ended a courtship
// when the courted one married someone else. Gabriel (04/11/2026, "1: oui"): the bond falls to Estranged ("trust broken,
// but feeling remains"), not Broken, and she remembers that she chose another and never denies what was.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>What becomes of a character's bond with the player when they marry someone else.</summary>
   public static class CourtshipLapsePolicy
   {
      /// <summary>
      ///   The bond after the character weds <paramref name="spouseIsPlayer" /> false: an open courtship or deeper falls
      ///   to Estranged; a secret lover stays one (that bond already lives beside a marriage); anything else is untouched.
      /// </summary>
      public static RomanticStatus After(RomanticStatus current, bool spouseIsPlayer)
      {
         if (spouseIsPlayer) return current;

         switch (current)
         {
            case RomanticStatus.Courting:
            case RomanticStatus.Intimate:
            case RomanticStatus.Committed:
               return RomanticStatus.Estranged;
            default:
               return current;
         }
      }

      /// <summary>
      ///   Whether a bond still running beside a marriage to someone else ends now, for a wedding already past (avalanche24's
      ///   Rhagaea was wed before this existed). Only when CR saw the wedding and it came on or after the bond's own day: a
      ///   courtship begun after her wedding, or revived since, is the player's own doing and is left alone.
      /// </summary>
      public static bool EndsForAPastWedding(bool wedToAnother, int? weddedOnDay, int? bondSinceDay)
         => wedToAnother && weddedOnDay is int wed && bondSinceDay is int since && wed >= since;

      /// <summary>What the character remembers of it, in their own memory (first person, as their own life is kept).</summary>
      public static string Memory(string spouseName)
         => $"Wed {spouseName ?? "another"}, though the player had been courting me. What was between the player and me ended there; " +
            "it was real, and I never deny it.";
   }
}
