// Code written by Gabriel Mailhot, 27/10/2026.
// Gabriel (26/10/2026): "le LLM a tendance a aimer tous les gestes sexuels que le joueur peut decrire. Ce serait plus
// interessant si chaque NPC avait des preferences pour certains gestes sexuels, et meme parfois des gestes qu'il n'aime
// pas. Par exemple un NPC pourrait particulierement aimer le sexe oral mais detester le sexe anal." Each character is
// given, once, one to three gestures they particularly love and, about one in two, a gesture they dislike (rarely two).
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
   /// <summary>The gestures a character loves and dislikes, rolled once from their id. Pure.</summary>
   public static class IntimateActPolicy
   {
      /// <summary>Every gesture a character may love or dislike.</summary>
      public static readonly IReadOnlyList<IntimateAct> All = Enum.GetValues(typeof(IntimateAct)).Cast<IntimateAct>().Where(a => a != IntimateAct.None).ToList();

      /// <summary>
      ///   The gestures <paramref name="heroId" /> loves and dislikes. A woman is never given taking her partner anally
      ///   (the setting has no place for it); the loved and the disliked never overlap.
      /// </summary>
      public static (List<IntimateAct> Loved, List<IntimateAct> Disliked) Roll(string heroId, bool isFemale)
      {
         var rng = new Random(StableSeed(heroId));
         List<IntimateAct> pool = All.Where(a => !(isFemale && a == IntimateAct.AnalGiving)).ToList();
         Shuffle(pool, rng);

         int roll = rng.Next(100);
         int loved = roll < 55 ? 1 : roll < 90 ? 2 : 3;
         roll = rng.Next(100);
         int disliked = roll < 50 ? 0 : roll < 92 ? 1 : 2;

         return (pool.Take(loved).ToList(), pool.Skip(loved).Take(disliked).ToList());
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

      private static void Shuffle(List<IntimateAct> list, Random rng)
      {
         for (int i = list.Count - 1; i > 0; i--)
         {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
         }
      }
   }
}
