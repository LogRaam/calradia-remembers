// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (27/10/2026): "Et aussi, 'qu'est-ce qui compte le plus' pour un NPC. Ses enfants? Sa richesse? Son amoureux?
// etc.", then "On va le faire maintenant" and "il faudra penser a les indiquer dans des sections de l'encyclopedia".
// What matters most is chosen among what is TRUE of the character: one who cherishes their children has children, a
// lover needs a lover, the honour of a house needs a house. Their traits weigh the choice, as they weigh their kinks and
// tastes ("les traits influencent les kinks qui eux influencent les gestes"). It is kept, and moves only when the world
// takes its ground away (the child dies, the lover is gone): then a score to settle takes its place if they hold one,
// otherwise another anchor is drawn. My defaults, awaiting Gabriel's ruling: ONE anchor (no second), and it changes only
// on such an event, never with time alone.
// The player learns it when the character says it in their own words; the encyclopedia then names it, without leaking a
// secret: a lover the player does not know of reads "someone she loves".

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>What is true of a character, as what matters most to them is chosen from it.</summary>
   public sealed class MattersMostFacts
   {
      /// <summary>The hero's id, the seed of the choice.</summary>
      public string HeroId { get; init; }

      /// <summary>Their traits (each -2..2).</summary>
      public RomanticTraits Traits { get; init; } = new();

      /// <summary>They have a living child.</summary>
      public bool HasChildren { get; init; }

      /// <summary>They have a living spouse who is not the player.</summary>
      public bool HasSpouse { get; init; }

      /// <summary>They have a lover who is not the player.</summary>
      public bool HasLover { get; init; }

      /// <summary>The player is their spouse or lover.</summary>
      public bool LovesThePlayer { get; init; }

      /// <summary>They have a living parent, brother or sister.</summary>
      public bool HasKin { get; init; }

      /// <summary>They belong to a noble house.</summary>
      public bool IsNoble { get; init; }

      /// <summary>They head their house, or rule a realm.</summary>
      public bool LeadsTheirHouse { get; init; }

      /// <summary>They hold a fief or govern one.</summary>
      public bool HoldsLands { get; init; }

      /// <summary>They belong to no noble house: a wanderer, a hired sword.</summary>
      public bool IsWanderer { get; init; }

      /// <summary>They hold a live grudge the player may know of (against someone else, or one the player knows).</summary>
      public bool HasScoreToSettle { get; init; }
   }

   /// <summary>What the encyclopedia says of what matters most to a character.</summary>
   public sealed class MattersMostPageFacts
   {
      /// <summary>What matters most to them.</summary>
      public MattersMost MattersMost { get; init; }

      /// <summary>The player learned it from them.</summary>
      public bool Known { get; init; }

      /// <summary>The character is a woman.</summary>
      public bool IsFemale { get; init; }

      /// <summary>The player knows of their lover (RomanticProfile.LoverKnownToPlayer).</summary>
      public bool LoverKnownToPlayer { get; init; }
   }

   /// <summary>Chooses, keeps and words what matters most to a character. Pure.</summary>
   public static class MattersMostPolicy
   {
      private static readonly MattersMost[] LovedOnes = {MattersMost.Children, MattersMost.Spouse, MattersMost.Lover, MattersMost.ThePlayer, MattersMost.Kin};

      /// <summary>Whether the world still gives this anchor its ground.</summary>
      public static bool Grounded(MattersMost m, MattersMostFacts f) => Weight(m, f) > 0;

      /// <summary>
      ///   What matters most to them now: the one they have while its ground stands; when it falls, a score to settle if
      ///   they hold one and lost someone they loved, otherwise another drawn by their traits; the first one drawn when
      ///   they had none.
      /// </summary>
      public static MattersMost Resolve(MattersMost current, MattersMostFacts f)
      {
         if (f == null) return current;
         if (current != MattersMost.None && Grounded(current, f)) return current;
         if (current != MattersMost.None && LovedOnes.Contains(current) && f.HasScoreToSettle) return MattersMost.Vengeance;

         return Draw(f, current == MattersMost.None ? f.HeroId : f.HeroId + "|" + current, current);
      }

      /// <summary>
      ///   Whether the player still knows what matters most to them after it moved from <paramref name="before" /> to
      ///   <paramref name="now" />: what they learned was the old anchor, so a new one is unknown until said again.
      /// </summary>
      public static bool StillKnown(MattersMost before, MattersMost now, bool known) => known && before == now;

      /// <summary>The line on their page, or null while the player has not learned it.</summary>
      public static string PageLine(MattersMostPageFacts f)
      {
         if (f == null || !f.Known || f.MattersMost == MattersMost.None) return null;

         string her = f.IsFemale ? "her" : "his";
         string she = f.IsFemale ? "she" : "he";
         string what = f.MattersMost switch {
            MattersMost.Children => $"{her} children",
            MattersMost.Spouse => f.IsFemale ? "her husband" : "his wife",
            MattersMost.Lover => f.LoverKnownToPlayer ? $"{her} lover" : $"someone {she} loves",
            MattersMost.ThePlayer => "you",
            MattersMost.Kin => $"{her} family",
            MattersMost.HouseHonor => $"the honour of {her} house",
            MattersMost.Wealth => "wealth",
            MattersMost.Power => "power",
            MattersMost.Glory => "glory in battle",
            MattersMost.Lands => $"{her} lands and the people on them",
            MattersMost.Freedom => $"{her} freedom",
            MattersMost.Vengeance => $"a score {she} means to settle",
            _ => null
         };

         return what == null ? null : $"What matters most to {(f.IsFemale ? "her" : "him")}: {what}.";
      }

      /// <summary>How likely an anchor is for them; 0 when the world gives it no ground.</summary>
      public static int Weight(MattersMost m, MattersMostFacts f)
      {
         RomanticTraits t = f?.Traits ?? new RomanticTraits();
         int If(bool ground, int w) => ground ? Math.Max(1, w) : 0;

         return m switch {
            MattersMost.Children => If(f.HasChildren, 10 + 3 * (t.Generosity + t.Mercy)),
            MattersMost.Spouse => If(f.HasSpouse, 7 + 3 * t.Honor),
            MattersMost.Lover => If(f.HasLover, 5),
            MattersMost.ThePlayer => If(f.LovesThePlayer, 8 + 3 * t.Honor),
            MattersMost.Kin => If(f.HasKin, 5 + 2 * t.Generosity),
            MattersMost.HouseHonor => If(f.IsNoble, 6 + 4 * t.Honor + (f.LeadsTheirHouse ? 6 : 0)),
            MattersMost.Wealth => If(true, 4 + 3 * t.Calculating - 3 * t.Generosity),
            MattersMost.Power => If(f.IsNoble, 3 + 3 * t.Calculating + (f.LeadsTheirHouse ? 4 : 0)),
            MattersMost.Glory => If(true, 4 + 4 * t.Valor),
            MattersMost.Lands => If(f.HoldsLands, 6 + 3 * t.Mercy),
            MattersMost.Freedom => If(f.IsWanderer, 12),
            MattersMost.Vengeance => If(f.HasScoreToSettle, 4 - 4 * t.Mercy),
            _ => 0
         };
      }

      // A weighted draw among the grounded anchors, seeded so the same character draws the same; never the lost one.
      private static MattersMost Draw(MattersMostFacts f, string seed, MattersMost lost)
      {
         List<(MattersMost m, int w)> weighed = ((MattersMost[]) Enum.GetValues(typeof(MattersMost)))
                                                .Where(m => m != MattersMost.None && m != lost).Select(m => (m, Weight(m, f))).Where(x => x.Item2 > 0).ToList();
         int total = weighed.Sum(x => x.w);
         if (total == 0) return MattersMost.None;

         int pick = new Random(IntimateActPolicy.StableSeed(seed)).Next(total);
         foreach ((MattersMost m, int w) in weighed)
         {
            if (pick < w) return m;
            pick -= w;
         }

         return weighed[weighed.Count - 1].m;
      }
   }
}
