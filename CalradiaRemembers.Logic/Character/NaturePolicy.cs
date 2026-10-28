// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (27/10/2026): "nous pourrions aussi ajouter certaines superstitions et phobies", in the What You Know pillar
// ("On va le faire maintenant"). Each character is given, once, perhaps a fear and perhaps a superstition, seeded by
// their id. Most fear nothing worth naming. Their traits weigh it (the timid fear more, the calculating believe less) and
// so does their people: a Khuzait of the open steppe fears closed places and the sea more than most, a Battanian keeps
// the old ways of omens and ancestors, a sea-going Nord never fears the sea. Beliefs, never magic: Calradia has none.
// The player learns each when the character lets it show; the encyclopedia then names it.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>Who is rolled for a fear and a superstition.</summary>
   public sealed class NatureFacts
   {
      /// <summary>The hero's id, the seed of the roll.</summary>
      public string HeroId { get; init; }

      /// <summary>Their traits (each -2..2).</summary>
      public RomanticTraits Traits { get; init; } = new();

      /// <summary>Their culture's id ("battania", "khuzait"...), or null.</summary>
      public string Culture { get; init; }
   }

   /// <summary>A character's fear and superstition, as rolled.</summary>
   public sealed class NatureRoll
   {
      /// <summary>What they fear, often nothing.</summary>
      public Fear Fear { get; init; }

      /// <summary>A superstition they keep, often none.</summary>
      public Superstition Superstition { get; init; }
   }

   /// <summary>What the encyclopedia says of a character's fear and superstition.</summary>
   public sealed class NaturePageFacts
   {
      /// <summary>What they fear.</summary>
      public Fear Fear { get; init; }

      /// <summary>The player learned it.</summary>
      public bool FearKnown { get; init; }

      /// <summary>A superstition they keep.</summary>
      public Superstition Superstition { get; init; }

      /// <summary>The player learned it.</summary>
      public bool SuperstitionKnown { get; init; }

      /// <summary>The character is a woman.</summary>
      public bool IsFemale { get; init; }
   }

   /// <summary>Rolls and words a character's fear and superstition. Pure.</summary>
   public static class NaturePolicy
   {
      /// <summary>The percent chance of a fear: lower for the valorous, higher for the timid.</summary>
      public static int FearChance(RomanticTraits t) => Math.Max(10, Math.Min(70, 35 - 12 * (t?.Valor ?? 0)));

      /// <summary>The percent chance of a superstition: lower for the calculating, higher among peoples of old ways.</summary>
      public static int SuperstitionChance(RomanticTraits t, string culture)
         => Math.Max(10, Math.Min(80, 45 - 12 * (t?.Calculating ?? 0) + (OldWays(culture) ? 10 : 0)));

      /// <summary>The fear and superstition of a character, the same on every roll.</summary>
      public static NatureRoll Roll(NatureFacts f)
      {
         var rng = new Random(IntimateActPolicy.StableSeed((f?.HeroId ?? "") + "|nature"));
         RomanticTraits t = f?.Traits ?? new RomanticTraits();
         string c = (f?.Culture ?? "").ToLowerInvariant();
         Fear fear = rng.Next(100) < FearChance(t) ? Draw(rng, FearWeights(c)) : Fear.None;
         Superstition belief = rng.Next(100) < SuperstitionChance(t, c) ? Draw(rng, SuperstitionWeights(c, t)) : Superstition.None;

         return new NatureRoll {Fear = fear, Superstition = belief};
      }

      /// <summary>The lines on their page, only what the player has learned.</summary>
      public static IReadOnlyList<string> PageLines(NaturePageFacts f)
      {
         var lines = new List<string>();
         if (f == null) return lines;

         string she = f.IsFemale ? "She" : "He";
         string fear = f.FearKnown ? FearNoun(f.Fear) : null;
         if (fear != null) lines.Add($"{she} fears {fear}.");
         string belief = f.SuperstitionKnown ? BeliefLine(f.Superstition) : null;
         if (belief != null) lines.Add($"{she} {belief}.");

         return lines;
      }

      private static bool OldWays(string culture) => culture is "battania" or "sturgia" or "khuzait" or "nord";

      private static Dictionary<Fear, int> FearWeights(string c)
      {
         Dictionary<Fear, int> w = ((Fear[]) Enum.GetValues(typeof(Fear))).Where(x => x != Fear.None).ToDictionary(x => x, _ => 4);
         if (c == "khuzait")
         {
            w[Fear.ClosedSpaces] += 6;
            w[Fear.TheSea] += 6;
         }

         if (c == "aserai") w[Fear.TheSea] += 4;
         if (c is "sturgia" or "battania") w[Fear.Fire] += 3;
         if (c == "empire") w[Fear.Sickness] += 4;
         if (c == "nord") w[Fear.TheSea] = 0;

         return w;
      }

      private static Dictionary<Superstition, int> SuperstitionWeights(string c, RomanticTraits t)
      {
         Dictionary<Superstition, int> w = ((Superstition[]) Enum.GetValues(typeof(Superstition))).Where(x => x != Superstition.None).ToDictionary(x => x, _ => 4);
         void Add(Superstition s, int n) => w[s] += n;
         switch (c)
         {
            case "battania":
               Add(Superstition.Ancestors, 4);
               Add(Superstition.Omens, 4);
               Add(Superstition.Thresholds, 2);
               break;
            case "sturgia":
               Add(Superstition.Omens, 3);
               Add(Superstition.EvilEye, 3);
               Add(Superstition.Thresholds, 3);
               break;
            case "khuzait":
               Add(Superstition.Omens, 4);
               Add(Superstition.Ancestors, 4);
               break;
            case "aserai":
               Add(Superstition.EvilEye, 5);
               Add(Superstition.Charms, 3);
               break;
            case "empire":
               Add(Superstition.Dreams, 3);
               Add(Superstition.Moon, 3);
               Add(Superstition.Charms, 2);
               break;
            case "vlandia":
               Add(Superstition.Charms, 3);
               Add(Superstition.Rituals, 3);
               break;
            case "nord":
               Add(Superstition.Omens, 4);
               Add(Superstition.Rituals, 3);
               break;
         }

         if (t.Valor >= 1) Add(Superstition.Rituals, 3);

         return w;
      }

      private static T Draw<T>(Random rng, Dictionary<T, int> weights)
      {
         List<KeyValuePair<T, int>> live = weights.Where(x => x.Value > 0).OrderBy(x => Convert.ToInt32(x.Key)).ToList();
         int pick = rng.Next(live.Sum(x => x.Value));
         foreach (KeyValuePair<T, int> x in live)
         {
            if (pick < x.Value) return x.Key;
            pick -= x.Value;
         }

         return live[live.Count - 1].Key;
      }

      private static string FearNoun(Fear f) => f switch {
         Fear.TheSea => "the sea",
         Fear.Fire => "fire",
         Fear.Wolves => "wolves",
         Fear.TheDark => "the dark",
         Fear.Sickness => "sickness",
         Fear.Heights => "heights",
         Fear.Snakes => "snakes",
         Fear.ClosedSpaces => "closed, narrow places",
         _ => null
      };

      private static string BeliefLine(Superstition s) => s switch {
         Superstition.Omens => "reads omens in birds and signs before a battle or a journey",
         Superstition.Charms => "never goes without a lucky charm",
         Superstition.Ancestors => "believes the dead watch and judge the living",
         Superstition.EvilEye => "fears the evil eye and ill-wishing",
         Superstition.Dreams => "takes dreams for portents",
         Superstition.Rituals => "keeps a small ritual before every fight or journey",
         Superstition.Moon => "holds that the moon decides good days and bad",
         Superstition.Thresholds => "keeps iron or salt at the door against ill luck",
         _ => null
      };
   }
}
