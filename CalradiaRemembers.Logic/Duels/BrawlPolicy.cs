// Code written by Gabriel Mailhot, 28/10/2026.
// The street brawl beside the duel. Gabriel (28/10/2026): "je ferais la difference entre le duel qui est noble et la bagarre
// de rue qui est un reglement de compte", and "bagarre seulement pour le compagnon". avalanche24 (Nexus, 28/10/2026): a
// companion hired hours before called him out "just because I told her [...] she should act like one and follow orders",
// and a landowner "with zero stats" called out a master of arms for saying no. So a brawl is a settling of scores: a
// commoner calls the player out only over a real grievance, a notable never does it himself, and a great gap in standing or
// at arms keeps a man's fists in his pockets unless he is reckless.

#region

using CalradiaRemembers.Logic.Social;

#endregion

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>A fight with steel between nobles, or with fists between anyone else.</summary>
   public enum DuelKind
   {
      /// <summary>A duel of honour, with steel: the nobles' affair.</summary>
      Honour,

      /// <summary>A brawl, with fists: a settling of scores, no house's honour at stake.</summary>
      Brawl
   }

   /// <summary>What decides whether a commoner calls the player out to a brawl.</summary>
   public sealed class BrawlInitiativeFacts
   {
      /// <summary>Their station (SocialStationPolicy's key).</summary>
      public string Station { get; init; }

      /// <summary>They hold a live grudge against the player.</summary>
      public bool HoldsAGrudge { get; init; }

      /// <summary>Their personal regard for the player.</summary>
      public int Regard { get; init; }

      /// <summary>Where the player stands in the world.</summary>
      public PlayerRung Rung { get; init; }

      /// <summary>How they compare with the player at arms.</summary>
      public ArmsGap Gap { get; init; }

      /// <summary>The game's Valor trait (-2 to 2): at 2 they are reckless enough to try anyway.</summary>
      public int Valor { get; init; }
   }

   /// <summary>Who fights the player how, and who may start a brawl.</summary>
   public static class BrawlPolicy
   {
      /// <summary>The regard at or below which a commoner holds a grievance worth their fists without a grudge: wary of the player.</summary>
      public const int GrievanceRegard = NpcMemoryService.Core.Standing.Regard.WaryTier;

      /// <summary>
      ///   How a character of <paramref name="station" /> fights the player: a duel for a crown, a lord or a noble, a brawl
      ///   for a companion, a wanderer, a gang leader or a notable of trade; null for anyone who does not fight the player at
      ///   all (a preacher, a character no station covers).
      /// </summary>
      public static DuelKind? KindFor(string station)
      {
         switch (station)
         {
            case SocialStationPolicy.Ruler:
            case SocialStationPolicy.Lord:
            case SocialStationPolicy.Noble: return DuelKind.Honour;
            case SocialStationPolicy.Companion:
            case SocialStationPolicy.Wanderer:
            case SocialStationPolicy.GangLeader:
            case "merchant":
            case "artisan":
            case "ruralnotable":
            case "headman": return DuelKind.Brawl;
            default: return null;
         }
      }

      /// <summary>Whether they may call the player out to a brawl themselves (the player may still call them out).</summary>
      public static bool MayCallOut(BrawlInitiativeFacts f)
      {
         if (f == null || KindFor(f.Station) != DuelKind.Brawl) return false;
         // A notable of trade sends others to settle a score; he does not roll up his own sleeves.
         if (IsNotableOfTrade(f.Station)) return false;
         if (!f.HoldsAGrudge && f.Regard > GrievanceRegard) return false;
         if (f.Valor >= 2) return true;
         if (f.Gap == ArmsGap.FarBelow) return false;

         // A companion answers to the captain whatever their rank; anyone else thinks twice before a lord.
         return f.Station == SocialStationPolicy.Companion || f.Rung < PlayerRung.RisingHouse;
      }

      private static bool IsNotableOfTrade(string station) => station == "merchant" || station == "artisan" || station == "ruralnotable" || station == "headman";
   }
}
