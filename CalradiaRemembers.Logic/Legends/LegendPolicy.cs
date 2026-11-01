// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): "When something extraordinary happens, such as a heroic last stand or a siege won against the odds,
// it could become a legend that characters retell, differently depending on their connection to the event. This could apply to
// the player too." Gabriel, 01/11/2026, "go" on three points: the player and the lords alike; a teller slants the tone, the stress
// and the reasons he believes, never the facts (NPCs never lie); no gameplay effect but the renown the player's own legend feeds.
// The game decides what is a legend, with no model call. The figures are mine, to tune in play.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Legends
{
   /// <summary>What kind of deed passed into legend.</summary>
   public enum LegendKind
   {
      /// <summary>A field won by the side badly outnumbered.</summary>
      VictoryAgainstTheOdds,

      /// <summary>A siege assault thrown back by a garrison badly outnumbered.</summary>
      SiegeHeld,

      /// <summary>A side overwhelmed, that made its enemy pay far beyond its own numbers.</summary>
      LastStand
   }

   /// <summary>What separates one who hears of a legend from the deed.</summary>
   public enum LegendBond
   {
      /// <summary>The deed is the character's own.</summary>
      OwnDeed,

      /// <summary>They fought on the side the legend honours.</summary>
      FoughtBeside,

      /// <summary>They fought on the other side.</summary>
      FoughtAgainst,

      /// <summary>Of the house of the one the legend honours.</summary>
      SameHouse,

      /// <summary>Of the realm the legend honours.</summary>
      SameRealm,

      /// <summary>Of the realm it was done against.</summary>
      OpposingRealm,

      /// <summary>No tie to it at all.</summary>
      Stranger
   }

   /// <summary>A battle as it ended, read at the start of it for the numbers.</summary>
   public sealed class LegendBattleFacts
   {
      /// <summary>The men the winning side brought.</summary>
      public int WinnerMen { get; init; }

      /// <summary>The men the losing side brought.</summary>
      public int LoserMen { get; init; }

      /// <summary>The men the winning side lost, dead or wounded.</summary>
      public int WinnerLosses { get; init; }

      /// <summary>True when the winners were the garrison of a siege assault.</summary>
      public bool GarrisonWon { get; init; }

      /// <summary>True when a lord of no bandit band led each side: a rout of looters is no one's legend.</summary>
      public bool LordsOnBothSides { get; init; }
   }

   /// <summary>What ties one character to a legend.</summary>
   public sealed class LegendTellerFacts
   {
      /// <summary>The character is the one the legend honours.</summary>
      public bool IsTheHero { get; init; }

      /// <summary>The character fought on the side the legend honours.</summary>
      public bool FoughtBeside { get; init; }

      /// <summary>The character fought on the other side.</summary>
      public bool FoughtAgainst { get; init; }

      /// <summary>The character is of the honoured one's house.</summary>
      public bool SameHouse { get; init; }

      /// <summary>The character is of the realm the legend honours.</summary>
      public bool SameRealm { get; init; }

      /// <summary>The character is of the realm it was done against.</summary>
      public bool OpposingRealm { get; init; }
   }

   /// <summary>Which battles pass into legend, who has heard of them, and how each tells it.</summary>
   public static class LegendPolicy
   {
      /// <summary>The smallest side that can make a legend: thirty men against ninety is a skirmish however it ends.</summary>
      public const int FewestMen = 30;

      /// <summary>How many men the stronger side must bring for each one of the weaker.</summary>
      public const int OddsAgainst = 3;

      /// <summary>In tenths, what a last stand must cost the enemy against the men who made it: half as many again.</summary>
      public const int LastStandTollInTenths = 15;

      /// <summary>The days between two legends, a season: what is told everywhere must stay extraordinary.</summary>
      public const int DaysBetweenLegends = 21;

      /// <summary>The days before a legend is known across the realms it touched.</summary>
      public const int DaysToReachTheRealms = 7;

      /// <summary>The days before all Calradia tells it.</summary>
      public const int DaysToReachCalradia = 30;

      /// <summary>The kind of legend this battle makes, or null for an ordinary battle.</summary>
      public static LegendKind? Classify(LegendBattleFacts f)
      {
         if (f == null || !f.LordsOnBothSides || Math.Min(f.WinnerMen, f.LoserMen) < FewestMen) return null;

         if (f.LoserMen >= f.WinnerMen * OddsAgainst) return f.GarrisonWon ? LegendKind.SiegeHeld : LegendKind.VictoryAgainstTheOdds;

         bool overwhelmed = f.WinnerMen >= f.LoserMen * OddsAgainst;
         bool paidFor = f.WinnerLosses * 10 >= f.LoserMen * LastStandTollInTenths;

         return overwhelmed && paidFor ? LegendKind.LastStand : (LegendKind?) null;
      }

      /// <summary>
      ///   True when a season has passed since the last legend. The player's own deed never waits: a lord's battle across the
      ///   map must not take the name of what the player just did (player experience over simulation).
      /// </summary>
      public static bool MayRaise(int? lastLegendDay, int today, bool theDeedIsThePlayers)
         => theDeedIsThePlayers || !lastLegendDay.HasValue || today - lastLegendDay.Value >= DaysBetweenLegends;

      /// <summary>The name the legend keeps for good.</summary>
      public static string Name(LegendKind kind, string place, string hero)
      {
         switch (kind)
         {
            case LegendKind.SiegeHeld: return $"The Holding of {place ?? hero}";
            case LegendKind.LastStand: return $"The Last Stand of {hero}";
            default: return $"The Day of {place ?? hero}";
         }
      }

      /// <summary>The closest tie this character has to a legend.</summary>
      public static LegendBond BondOf(LegendTellerFacts f)
      {
         if (f == null) return LegendBond.Stranger;
         if (f.IsTheHero) return LegendBond.OwnDeed;
         if (f.FoughtBeside) return LegendBond.FoughtBeside;
         if (f.FoughtAgainst) return LegendBond.FoughtAgainst;
         if (f.SameHouse) return LegendBond.SameHouse;
         if (f.SameRealm) return LegendBond.SameRealm;

         return f.OpposingRealm ? LegendBond.OpposingRealm : LegendBond.Stranger;
      }

      /// <summary>True once word of it has come this far: those who were there at once, the realms in a week, the rest in a month.</summary>
      public static bool HasHeard(LegendBond bond, int daysSince)
      {
         switch (bond)
         {
            case LegendBond.SameRealm:
            case LegendBond.OpposingRealm:
               return daysSince >= DaysToReachTheRealms;
            case LegendBond.Stranger:
               return daysSince >= DaysToReachCalradia;
            default:
               return true;
         }
      }

      /// <summary>
      ///   The one legend a character carries into a conversation: the one that touches them closest, then the newest. One, so
      ///   that a legend stays a story and does not become a recital.
      /// </summary>
      public static Legend Closest(IEnumerable<Legend> legends, Func<Legend, LegendBond> bondOf, int today)
         => (legends ?? Enumerable.Empty<Legend>())
            .Where(l => HasHeard(bondOf(l), today - l.Day))
            .OrderBy(bondOf)
            .ThenByDescending(l => l.Day)
            .FirstOrDefault();

      /// <summary>What happened, plainly: the part a teller may never change.</summary>
      public static string Deed(Legend legend)
      {
         string at = string.IsNullOrEmpty(legend.PlaceName) ? "in open country" : $"at {legend.PlaceName}";
         string enemy = string.IsNullOrEmpty(legend.OpposingRealmName) ? "the enemy" : legend.OpposingRealmName;

         switch (legend.Kind)
         {
            case LegendKind.SiegeHeld:
               return $"{legend.HeroName} held {legend.PlaceName ?? "the walls"} with {legend.FewMen} men against {legend.ManyMen} of {enemy}";
            case LegendKind.LastStand:
               return $"{legend.HeroName} stood {at} with {legend.FewMen} men against {legend.ManyMen} of {enemy}, lost the field, and cost them {legend.Toll} men";
            default:
               return $"{legend.HeroName} won {at} with {legend.FewMen} men against {legend.ManyMen} of {enemy}";
         }
      }

      /// <summary>The block a character reads, in the prompt: the deed, what it is to them, and what they may not change.</summary>
      public static string PromptLine(Legend legend, LegendBond bond, string when)
         => $"A LEGEND YOU KNOW, \"{legend.Name}\": in {when}, {Deed(legend)}. {Stance(bond)} "
            + "Bring it up only when the talk turns that way. Slant it as you truly believe, but never change who won, where, or the numbers.";

      private static string Stance(LegendBond bond)
      {
         switch (bond)
         {
            case LegendBond.OwnDeed: return "This is your own deed, and you know what it truly cost.";
            case LegendBond.FoughtBeside: return "You stood on that side that day, and you tell it with pride.";
            case LegendBond.FoughtAgainst: return "You fought on the other side that day, and the memory rankles.";
            case LegendBond.SameHouse: return "It is the glory of your own house.";
            case LegendBond.SameRealm: return "It is your realm's pride.";
            case LegendBond.OpposingRealm: return "It was done against your realm, and you hear it told with rancour.";
            default: return "It is nothing to you but a story people tell.";
         }
      }
   }
}
