// Code written by Gabriel Mailhot, 07/10/2026.
// What a tale's band is made of. fkasad (Nexus, 07/10/2026): "65 looters vs 110 veterans Dawi. I could have auto
// resolved it at no cost." A band that grows with the player's numbers (AuthoredBandSizePolicy) is still no climax if
// every man is the weakest troop in the game. A "local" band is made of the region's bandits, and a share of it is
// seasoned (its troop upgraded once, or twice) as the player's own company is seasoned.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What a band's makeup is judged on.</summary>
   public sealed class AuthoredBandMakeupFacts
   {
      /// <summary>How many men the band musters.</summary>
      public int Size { get; init; }

      /// <summary>The average tier of the regular troops in the player's party (heroes left out).</summary>
      public double PlayerAverageTier { get; init; }

      /// <summary>How many times the band's basic troop can be upgraded (0 when it never improves).</summary>
      public int UpgradeSteps { get; init; }
   }

   /// <summary>How many men of each step of the band's troop line. Pure.</summary>
   public static class AuthoredBandMakeupPolicy
   {
      /// <summary>The template a file writes for the bandits of the region around the tale's place.</summary>
      public const string LocalTemplate = "local";

      /// <summary>TUNE: below this average tier, the player's men are recruits and the band is all plain bandits.</summary>
      public const double SeasonedFromTier = 1.5;

      /// <summary>TUNE: tiers above that mark over which the seasoned share climbs to its ceiling.</summary>
      public const double TiersToFullShare = 4.0;

      /// <summary>TUNE: the most of a band that is ever seasoned.</summary>
      public const double MaxSeasonedShare = 0.6;

      /// <summary>TUNE: from this average tier, half the seasoned men are upgraded twice.</summary>
      public const double VeteranFromTier = 4.0;

      /// <summary>Men per step of the troop line: [plain, upgraded once, upgraded twice...], adding up to the size.</summary>
      public static int[] Split(AuthoredBandMakeupFacts f)
      {
         int steps = f == null || f.UpgradeSteps < 0 ? 0 : f.UpgradeSteps;
         var split = new int[steps + 1];
         int size = f == null || f.Size < 0 ? 0 : f.Size;
         if (steps == 0)
         {
            split[0] = size;

            return split;
         }

         double share = (f.PlayerAverageTier - SeasonedFromTier) / TiersToFullShare;
         if (share < 0) share = 0;
         if (share > MaxSeasonedShare) share = MaxSeasonedShare;

         int seasoned = (int) (size * share + 0.5);
         int twice = steps >= 2 && f.PlayerAverageTier >= VeteranFromTier ? seasoned / 2 : 0;

         split[0] = size - seasoned;
         split[1] = seasoned - twice;
         if (steps >= 2) split[2] = twice;

         return split;
      }
   }
}
