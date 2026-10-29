// Code written by Gabriel Mailhot, 28/10/2026.
// Where the player stands in the eyes of the one they speak to, in rank and at arms. Gabriel (28/10/2026): "Il faut aussi
// que le comportement s'ajuste selon le statut du joueur. Un paysan qui parle au joueur niveau 1 sera plus rude que si il
// parle au joueur niveau 20 Clan tier 3", five rungs. avalanche24 (Nexus, 28/10/2026): a companion "with his best stat
// less than 100" boasted of beating a kingdom before a player who "kills 100+ enemies in almost every battle"; nothing
// told the character who was the better fighter. Rank is read as Stature (the one rank type), prowess as
// CombatProwessPolicy.Prowess (the one notion of who fights better); the gap is bucketed so a skill point never moves it.

#region

using CalradiaRemembers.Logic.Battles;

#endregion

namespace CalradiaRemembers.Logic.Social
{
   /// <summary>The player's place in the world, as anyone can see it.</summary>
   public enum PlayerRung
   {
      /// <summary>No house of note, no lands, no name yet.</summary>
      Nobody,

      /// <summary>A captain with a band, a sword for hire, without lands or title.</summary>
      Sellsword,

      /// <summary>The head of a house of some weight, not yet a great lord.</summary>
      RisingHouse,

      /// <summary>A great lord: lands held of a crown, or a great house.</summary>
      LordOfTheRealm,

      /// <summary>Rules a realm.</summary>
      Sovereign
   }

   /// <summary>How the one speaking compares with the player at arms.</summary>
   public enum ArmsGap
   {
      /// <summary>The player is far the better fighter.</summary>
      FarBelow,

      /// <summary>The player is the better fighter.</summary>
      Below,

      /// <summary>About even.</summary>
      Even,

      /// <summary>The one speaking is the better fighter.</summary>
      Above,

      /// <summary>The one speaking is far the better fighter.</summary>
      FarAbove
   }

   /// <summary>What the rung is read from.</summary>
   public sealed class PlayerRungFacts
   {
      /// <summary>The player's stature: their clan's tier and whether they rule.</summary>
      public Stature Stature { get; init; }

      /// <summary>The player's clan is sworn to a realm by oath (not under a mercenary contract).</summary>
      public bool SwornVassal { get; init; }

      /// <summary>The player's clan serves a realm under a mercenary contract.</summary>
      public bool Mercenary { get; init; }

      /// <summary>The player's clan holds a town or a castle.</summary>
      public bool HoldsFief { get; init; }

      /// <summary>The player's character level.</summary>
      public int Level { get; init; }
   }

   /// <summary>Reads the player's rung and the gap at arms, and says them to the one speaking.</summary>
   public static class PlayerRungPolicy
   {
      /// <summary>TUNE: a level from which a lone fighter is plainly a seasoned sword, whatever their house.</summary>
      public const int SeasonedLevel = 15;

      /// <summary>TUNE: how many times the other's prowess makes one "far" the better fighter.</summary>
      public const double FarRatio = 2.0;

      /// <summary>The player's rung.</summary>
      public static PlayerRung Rung(PlayerRungFacts f)
      {
         if (f == null) return PlayerRung.Nobody;
         if (f.Stature.IsRuler) return PlayerRung.Sovereign;
         if (f.SwornVassal && f.HoldsFief || f.Stature.IsGreatHouse) return PlayerRung.LordOfTheRealm;
         if (f.Stature.ClanTier >= 2 || f.HoldsFief || f.SwornVassal) return PlayerRung.RisingHouse;
         if (f.Stature.IsEstablished || f.Mercenary || f.Level >= SeasonedLevel) return PlayerRung.Sellsword;

         return PlayerRung.Nobody;
      }

      /// <summary>How <paramref name="speaker" /> compares with <paramref name="player" /> at arms.</summary>
      public static ArmsGap Gap(CombatantFacts speaker, CombatantFacts player)
      {
         double mine = CombatProwessPolicy.Prowess(speaker);
         double theirs = CombatProwessPolicy.Prowess(player);

         if (theirs >= mine * FarRatio) return ArmsGap.FarBelow;
         if (theirs >= mine * CombatProwessPolicy.ExploitRatio) return ArmsGap.Below;
         if (mine >= theirs * FarRatio) return ArmsGap.FarAbove;
         if (mine >= theirs * CombatProwessPolicy.ExploitRatio) return ArmsGap.Above;

         return ArmsGap.Even;
      }

      /// <summary>What the one speaking sees of the player's place, in one sentence.</summary>
      public static string RungLine(PlayerRung rung)
      {
         switch (rung)
         {
            case PlayerRung.Sovereign: return "In the eyes of the world the player is a sovereign who rules a realm.";
            case PlayerRung.LordOfTheRealm: return "In the eyes of the world the player is a great lord, with lands and men of their own.";
            case PlayerRung.RisingHouse: return "In the eyes of the world the player heads a rising house: a name of some weight, not yet a great lord.";
            case PlayerRung.Sellsword: return "In the eyes of the world the player is a sword for hire: a captain with a band, but no lands and no title.";
            default: return "In the eyes of the world the player is a nobody: no house of note, no lands, no name yet.";
         }
      }

      /// <summary>What the one speaking knows of the gap at arms, or null when they are evenly matched.</summary>
      public static string GapLine(ArmsGap gap)
      {
         switch (gap)
         {
            case ArmsGap.FarBelow: return "The player is a far better fighter than you, and you know it: you would not last against them.";
            case ArmsGap.Below: return "The player is the better fighter of you two, and you know it.";
            case ArmsGap.Above: return "You are the better fighter of you two.";
            case ArmsGap.FarAbove: return "You are a far better fighter than the player, and you know it.";
            default: return null;
         }
      }

      /// <summary>Both, as the line the one speaking is given.</summary>
      public static string Line(PlayerRung rung, ArmsGap gap)
      {
         string arms = GapLine(gap);

         return arms == null ? RungLine(rung) : RungLine(rung) + " " + arms;
      }
   }
}
