// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (27/10/2026): "un NPC pourrait [...] preferer certains cadeaux. Par exemple [...] Une Dame pourrait aimer les
// fourrures", in the What You Know pillar ("On va le faire maintenant"). Each character is given, once, one or two kinds
// of gift they love and, about one in two, a kind that leaves them cold. Their people weigh it most (furs and ale in
// Sturgia, horses on the steppe, wine and velvet in the Empire), then their traits (the valorous love arms) and their
// station (a noble house prizes velvet and jewelry, a townsman silver and good food); never their sex, so the lady who
// loves furs is a Sturgian or a proud one, not every woman. The kind is read from the item's own category, so the game,
// not the model, knows what was given. A loved gift earns twice the regard its value would; a cold one half, and never
// costs any (my default, awaiting Gabriel's ruling). The player learns the taste by giving such a gift.
// Silver retired (EvilPoptart1987, Nexus, 09/11/2026): "Many characters are rolling a preference for "silver" in gifts.
// The problem is, there is no such item. There is silver ore, which they don't want and jewelry which is what silver is
// processed into." The game's only item of category "silver" is Silver Ore; its silverware is of category "jewelry".
// No new roll draws Silver; one already rolled reads as Jewelry (GiftTastePolicy.WithoutSilver). Gabriel: "vas-y".

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>Who is rolled for gift tastes.</summary>
   public sealed class GiftTasteFacts
   {
      /// <summary>The hero's id, the seed of the roll.</summary>
      public string HeroId { get; init; }

      /// <summary>Their traits (each -2..2).</summary>
      public RomanticTraits Traits { get; init; } = new();

      /// <summary>Their culture's id ("sturgia", "empire"...), or null.</summary>
      public string Culture { get; init; }

      /// <summary>They belong to a noble house.</summary>
      public bool IsNoble { get; init; }
   }

   /// <summary>A character's gift tastes, as rolled.</summary>
   public sealed class GiftTastes
   {
      /// <summary>The kinds they love: one or two.</summary>
      public List<GiftKind> Loved { get; init; } = new();

      /// <summary>The kind that leaves them cold, often none.</summary>
      public List<GiftKind> Cold { get; init; } = new();
   }

   /// <summary>What the encyclopedia says of a character's gift tastes.</summary>
   public sealed class GiftPageFacts
   {
      /// <summary>The kinds they love.</summary>
      public IReadOnlyList<GiftKind> Loved { get; init; } = new List<GiftKind>();

      /// <summary>The kinds that leave them cold.</summary>
      public IReadOnlyList<GiftKind> Cold { get; init; } = new List<GiftKind>();

      /// <summary>The kinds whose welcome the player has learned by giving one.</summary>
      public IReadOnlyList<GiftKind> Known { get; init; } = new List<GiftKind>();

      /// <summary>The character is a woman.</summary>
      public bool IsFemale { get; init; }
   }

   /// <summary>How a gift is met.</summary>
   public enum GiftWelcome
   {
      /// <summary>Neither loved nor cold: worth what its value says.</summary>
      Ordinary,

      /// <summary>A kind they love.</summary>
      Loved,

      /// <summary>A kind that leaves them cold.</summary>
      Cold
   }

   /// <summary>Rolls gift tastes, reads an item's kind, sizes a gift's regard and words what was learned. Pure.</summary>
   public static class GiftTastePolicy
   {
      /// <summary>The kind of gift an item category is ("fur" is Furs), or None for anything not given as a gift.</summary>
      public static GiftKind KindOf(string categoryId)
      {
         string c = (categoryId ?? "").Trim().ToLowerInvariant();
         if (c.StartsWith("melee_weapons") || c.StartsWith("ranged_weapons") || c.StartsWith("shield")) return GiftKind.Weapons;

         return c switch {
            "fur" => GiftKind.Furs,
            "wine" => GiftKind.Wine,
            "beer" => GiftKind.Ale,
            "jewelry" => GiftKind.Jewelry,
            "velvet" => GiftKind.Velvet,
            "linen" or "cloth" or "cotton" => GiftKind.FineCloth,
            "horse" or "war_horse" or "noble_horse" => GiftKind.Horses,
            "garment" or "light_armor" or "medium_armor" or "heavy_armor" or "ultra_armor" => GiftKind.Armor,
            "cheese" or "date_fruit" or "olives" or "grape" or "butter" or "meat" => GiftKind.Delicacies,
            _ => GiftKind.None
         };
      }

      /// <summary>The gift tastes of a character, the same on every roll.</summary>
      public static GiftTastes Roll(GiftTasteFacts f)
      {
         var rng = new Random(IntimateActPolicy.StableSeed((f?.HeroId ?? "") + "|gifts"));
         int loved = rng.Next(100) < 70 ? 1 : 2;
         int cold = rng.Next(100) < 50 ? 1 : 0;
         Dictionary<GiftKind, int> boost = Boosts(f);
         List<GiftKind> open = ((GiftKind[]) Enum.GetValues(typeof(GiftKind))).Where(k => k != GiftKind.None && k != GiftKind.Silver).ToList();

         List<GiftKind> lovedKinds = Draw(open, loved, rng, k => 3 + boost[k]);
         List<GiftKind> coldKinds = Draw(open, cold, rng, k => boost[k] > 0 ? 1 : 3);

         return new GiftTastes {Loved = lovedKinds, Cold = coldKinds};
      }

      /// <summary>
      ///   Tastes rolled before Silver was retired, mended: a loved Silver becomes Jewelry (the game's silverware), or
      ///   goes when Jewelry is already loved; a cold Silver becomes Jewelry unless Jewelry is loved or already cold; a
      ///   learned Silver is learned Jewelry. Tastes without Silver come back unchanged.
      /// </summary>
      public static GiftTastes WithoutSilver(GiftTastes rolled, IReadOnlyList<GiftKind> known, out List<GiftKind> knownAfter)
      {
         List<GiftKind> loved = rolled?.Loved ?? new List<GiftKind>();
         List<GiftKind> cold = rolled?.Cold ?? new List<GiftKind>();
         var lovedAfter = loved.Select(k => k == GiftKind.Silver ? GiftKind.Jewelry : k).Distinct().ToList();
         var coldAfter = cold.Select(k => k == GiftKind.Silver ? GiftKind.Jewelry : k).Where(k => !lovedAfter.Contains(k)).Distinct().ToList();
         knownAfter = known?.Select(k => k == GiftKind.Silver ? GiftKind.Jewelry : k).Distinct().ToList();

         return new GiftTastes {Loved = lovedAfter, Cold = coldAfter};
      }

      /// <summary>How a gift of <paramref name="kind" /> is met by one with these tastes.</summary>
      public static GiftWelcome Welcome(GiftKind kind, IEnumerable<GiftKind> loved, IEnumerable<GiftKind> cold)
      {
         if (kind == GiftKind.None) return GiftWelcome.Ordinary;
         if (loved?.Contains(kind) == true) return GiftWelcome.Loved;

         return cold?.Contains(kind) == true ? GiftWelcome.Cold : GiftWelcome.Ordinary;
      }

      /// <summary>The regard a gift earns: twice its value's warmth when loved, half (rounded down) when cold, never below 0.</summary>
      public static int Warmth(int valueWarmth, GiftWelcome welcome)
      {
         int w = Math.Max(0, valueWarmth);

         return welcome switch {
            GiftWelcome.Loved => w * 2,
            GiftWelcome.Cold => w / 2,
            _ => w
         };
      }

      /// <summary>The lines on their page: only the tastes the player has learned.</summary>
      public static IReadOnlyList<string> PageLines(GiftPageFacts f)
      {
         var knownSet = new HashSet<GiftKind>(f?.Known ?? new List<GiftKind>());
         List<string> l = (f?.Loved ?? new List<GiftKind>()).Where(knownSet.Contains).Select(Noun).Where(n => n != null).ToList();
         List<string> c = (f?.Cold ?? new List<GiftKind>()).Where(knownSet.Contains).Select(Noun).Where(n => n != null).ToList();
         string her = f?.IsFemale == true ? "her" : "him";
         var lines = new List<string>();
         if (l.Count > 0) lines.Add($"Gifts that delight {her}: {Joined(l)}.");
         if (c.Count > 0) lines.Add($"Gifts that leave {her} cold: {Joined(c)}.");

         return lines;
      }

      private static Dictionary<GiftKind, int> Boosts(GiftTasteFacts f)
      {
         Dictionary<GiftKind, int> b = ((GiftKind[]) Enum.GetValues(typeof(GiftKind))).ToDictionary(k => k, _ => 0);
         void Add(GiftKind k, int n) => b[k] += n;
         switch ((f?.Culture ?? "").ToLowerInvariant())
         {
            case "sturgia":
               Add(GiftKind.Furs, 6);
               Add(GiftKind.Ale, 4);
               break;
            case "nord":
               Add(GiftKind.Ale, 5);
               Add(GiftKind.Furs, 4);
               Add(GiftKind.Weapons, 3);
               break;
            case "battania":
               Add(GiftKind.Ale, 4);
               Add(GiftKind.Jewelry, 3);
               Add(GiftKind.Horses, 2);
               break;
            case "khuzait":
               Add(GiftKind.Horses, 7);
               Add(GiftKind.Furs, 4);
               break;
            case "aserai":
               Add(GiftKind.Horses, 5);
               Add(GiftKind.Jewelry, 4);
               Add(GiftKind.Delicacies, 4);
               break;
            case "empire":
               Add(GiftKind.Wine, 6);
               Add(GiftKind.Velvet, 4);
               Add(GiftKind.Jewelry, 3);
               break;
            case "vlandia":
               Add(GiftKind.Wine, 5);
               Add(GiftKind.Horses, 3);
               Add(GiftKind.Weapons, 3);
               break;
         }

         RomanticTraits t = f?.Traits ?? new RomanticTraits();
         if (t.Valor >= 1)
         {
            Add(GiftKind.Weapons, 4);
            Add(GiftKind.Armor, 3);
            Add(GiftKind.Horses, 2);
         }

         if (t.Honor >= 1) Add(GiftKind.Weapons, 2);
         if (t.Calculating >= 1)
         {
            Add(GiftKind.Jewelry, 2);
         }

         if (f?.IsNoble == true)
         {
            Add(GiftKind.Velvet, 2);
            Add(GiftKind.Jewelry, 2);
            Add(GiftKind.Wine, 2);
         }
         else
         {
            Add(GiftKind.Ale, 3);
            Add(GiftKind.Delicacies, 3);
         }

         return b;
      }

      // Draws n kinds from open by weight, without putting one back.
      private static List<GiftKind> Draw(List<GiftKind> open, int n, Random rng, Func<GiftKind, int> weight)
      {
         var drawn = new List<GiftKind>();
         for (int i = 0; i < n && open.Count > 0; i++)
         {
            int total = open.Sum(weight);
            int pick = rng.Next(total);
            foreach (GiftKind k in open)
            {
               if (pick < weight(k))
               {
                  drawn.Add(k);
                  open.Remove(k);
                  break;
               }

               pick -= weight(k);
            }
         }

         return drawn;
      }

      private static string Noun(GiftKind k) => k switch {
         GiftKind.Furs => "furs",
         GiftKind.Wine => "wine",
         GiftKind.Ale => "ale",
         GiftKind.Jewelry => "jewelry",
         GiftKind.Velvet => "velvet",
         GiftKind.FineCloth => "fine cloth",
         GiftKind.Horses => "horses",
         GiftKind.Weapons => "weapons",
         GiftKind.Armor => "armour",
         GiftKind.Delicacies => "delicacies",
         _ => null
      };

      private static string Joined(IReadOnlyList<string> parts)
         => parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
   }
}
