// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (26/10/2026): "le LLM a tendance a aimer tous les gestes sexuels que le joueur peut decrire. Ce serait plus
// interessant si chaque NPC avait des preferences pour certains gestes sexuels, et meme parfois des gestes qu'il n'aime
// pas. Par exemple un NPC pourrait particulierement aimer le sexe oral mais detester le sexe anal." Then (27/10/2026):
// "je dirais au moins quelque chose que le personnage aime beaucoup, et une chose qu'elle aime moins". So each character
// is given, once, one or two gestures they love, always one they are less fond of, and about two in five a gesture they
// dislike outright. Every other gesture is met with ordinary pleasure and is not written down.
// The roll is seeded by the hero's id with a stable hash (string.GetHashCode changes from one run to the next), so the
// same character rolls the same tastes; it is then kept in the save, so retuning this table never changes a character
// the player already knows.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>A character's tastes in bed, as rolled.</summary>
   public sealed class IntimateTastes
   {
      /// <summary>The gestures they love: one or two.</summary>
      public List<IntimateAct> Loved { get; init; } = new();

      /// <summary>The gesture they are less fond of: always one.</summary>
      public List<IntimateAct> LessFond { get; init; } = new();

      /// <summary>The gesture they dislike outright, often none.</summary>
      public List<IntimateAct> Disliked { get; init; } = new();
   }

   /// <summary>The gestures a character loves, is less fond of and dislikes, rolled once from their id. Pure.</summary>
   public static class IntimateActPolicy
   {
      /// <summary>Every gesture a character may love or dislike.</summary>
      public static readonly IReadOnlyList<IntimateAct> All = Enum.GetValues(typeof(IntimateAct)).Cast<IntimateAct>().Where(a => a != IntimateAct.None).ToList();

      /// <summary>
      ///   The tastes of <paramref name="heroId" />. A woman is never given taking her partner anally (the setting has no
      ///   place for it); no gesture falls in two lists.
      /// </summary>
      public static IntimateTastes Roll(string heroId, bool isFemale)
      {
         var rng = new Random(StableSeed(heroId));
         List<IntimateAct> pool = Pool(isFemale, rng);

         int loved = rng.Next(100) < 60 ? 1 : 2;
         int disliked = rng.Next(100) < 40 ? 1 : 0;

         return new IntimateTastes {
            Loved = pool.Take(loved).ToList(), LessFond = pool.Skip(loved).Take(1).ToList(), Disliked = pool.Skip(loved + 1).Take(disliked).ToList()
         };
      }

      /// <summary>
      ///   The gesture a character rolled before 27/10/2026 is less fond of: the first of their own shuffled gestures not
      ///   already loved or disliked, so a character rolled today gets the same one as <see cref="Roll" /> gives.
      /// </summary>
      public static List<IntimateAct> LessFondFor(string heroId, bool isFemale, IEnumerable<IntimateAct> taken)
      {
         var takenSet = new HashSet<IntimateAct>(taken ?? Enumerable.Empty<IntimateAct>());

         return Pool(isFemale, new Random(StableSeed(heroId))).Where(a => !takenSet.Contains(a)).Take(1).ToList();
      }

      /// <summary>FNV-1a over the id: the same number on every run and every machine, unlike string.GetHashCode.</summary>
      public static int StableSeed(string heroId)
      {
         unchecked
         {
            uint h = 2166136261;
            foreach (char c in heroId ?? "")
            {
               h ^= c;
               h *= 16777619;
            }

            return (int) h;
         }
      }

      // The character's gestures in their own shuffled order: the first draws of the seeded generator.
      private static List<IntimateAct> Pool(bool isFemale, Random rng)
      {
         List<IntimateAct> pool = All.Where(a => !(isFemale && a == IntimateAct.AnalGiving)).ToList();
         for (int i = pool.Count - 1; i > 0; i--)
         {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
         }

         return pool;
      }
   }
}
