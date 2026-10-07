// Code written by Gabriel Mailhot, 07/10/2026.
// How many men a tale's band musters. fkasad (Nexus, 07/10/2026): "last encounter difficulty should scale with
// rank/army size. Otherwise, there's no climax scene to end the quest." A band the file sizes at 12 to 20 is a fight for
// a lone rider and nothing for a lord leading a hundred. A band that scales meets the player at their size: never below
// the file's roll, a share of the player's fighting men above it, and a ceiling so one wood never hides a horde.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What a band's size is judged on.</summary>
   public sealed class AuthoredBandSizeFacts
   {
      /// <summary>The size rolled within the file's [fewest, most].</summary>
      public int Rolled { get; init; }

      /// <summary>The file asks for the band to grow with the player.</summary>
      public bool ScalesWithPlayer { get; init; }

      /// <summary>The healthy men in the player's party, the player included.</summary>
      public int PlayerFighters { get; init; }
   }

   /// <summary>The size of a tale's band. Pure.</summary>
   public static class AuthoredBandSizePolicy
   {
      /// <summary>TUNE: the band a scaling tale musters, as a share of the player's fighting men.</summary>
      public const double ShareOfPlayerFighters = 0.6;

      /// <summary>TUNE: the most men a scaling band ever musters (unless the file itself asks for more).</summary>
      public const int MaxScaledBand = 120;

      /// <summary>The band's size: the file's roll, or, when it scales, the larger of that and a share of the player's men, up to the ceiling.</summary>
      public static int Size(AuthoredBandSizeFacts f)
      {
         if (f == null) return 0;
         if (!f.ScalesWithPlayer) return f.Rolled;

         int matched = (int) (f.PlayerFighters * ShareOfPlayerFighters + 0.5);
         if (matched > MaxScaledBand) matched = MaxScaledBand;

         return matched > f.Rolled ? matched : f.Rolled;
      }
   }
}
