// Code written by Gabriel Mailhot, 18/06/2026.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Commoners
{
   /// <summary>
   ///   Creates synthetic <see cref="NpcProfile" /> objects for transient commoner
   ///   conversations. These profiles are never persisted to the store — they live
   ///   only for the duration of a single chat window.
   /// </summary>
   public static class CommonsProfileFactory
   {
      private static readonly string[] _archetypes = {
         "a farmer selling grain at the market",
         "a tanner's apprentice",
         "a baker's helper",
         "a tired stable hand",
         "a town watchman taking a rest",
         "a wool merchant passing through",
         "an innkeeper's daughter running errands",
         "a retired soldier turned day laborer",
         "a fisherman visiting from the harbor",
         "a seamstress taking a break between orders",
         "a young shepherd who came to sell a ewe",
         "a blacksmith's apprentice covered in soot"
      };

      private static readonly string[] _fallbackNames =
         {"Aren", "Mira", "Jorin", "Tessa", "Davan", "Lyra", "Cort", "Nessa"};

      // Short culture-keyed name pools. Keyed on the culture name string from the game.
      private static readonly Dictionary<string, string[]> _namesByCulture =
         new(StringComparer.OrdinalIgnoreCase) {
            ["empire"] = new[] {"Maro", "Cassia", "Lucius", "Valeria", "Tertius", "Flavia", "Cato", "Rufina"},
            ["vlandia"] = new[] {"Wulfric", "Ingrid", "Aldric", "Sigrid", "Bertram", "Edith", "Hagen", "Marta"},
            ["sturgia"] = new[] {"Borys", "Lyudmila", "Radovan", "Oksana", "Mirko", "Daria", "Svyat", "Halyna"},
            ["aserai"] = new[] {"Farouk", "Aisha", "Tariq", "Sultana", "Rashid", "Fatima", "Yusuf", "Zahra"},
            ["khuzait"] = new[] {"Temur", "Altai", "Khulan", "Batu", "Oyun", "Togrul", "Sarnai", "Jebe"},
            ["battania"] = new[] {"Caoilfhinn", "Ailbhe", "Fergal", "Sorcha", "Bran", "Grainne", "Cael", "Niamh"}
         };

      private static readonly Random _rng = new();

      /// <summary>
      ///   Creates a synthetic commoner profile for a conversation in the given
      ///   settlement. The profile Id is unique per call; caller must NOT store it.
      ///   <paramref name="knownIsFemale" /> is the ACTUAL gender of the villager the player is speaking to
      ///   (from the conversation character); pass it so a female villager is voiced as a woman. Only when it is
      ///   null (no partner resolvable) does the gender fall back to a coin flip, as it always did.
      /// </summary>
      public static NpcProfile Create(Settlement settlement, bool? knownIsFemale = null)
      {
         string culture = settlement?.Culture?.Name?.ToString() ?? "";
         bool isFemale = knownIsFemale ?? _rng.Next(2) == 0;
         string name = DrawName(culture, isFemale);
         string archetype = _archetypes[_rng.Next(_archetypes.Length)];

         return new NpcProfile {
            Id = $"cr_commoner_{_rng.Next(0, 999999):D6}",
            Name = name,
            Clan = "common folk",
            Faction = settlement?.MapFaction?.Name?.ToString() ?? "none",
            IsFemale = isFemale,
            Personality = archetype,
            ReputationWithPlayer = 0
         };
      }

      #region private

      private static string DrawName(string culture, bool isFemale)
      {
         if (!_namesByCulture.TryGetValue(culture, out string[] pool))
            pool = _fallbackNames;

         // Even indices = male-leaning, odd = female-leaning (a rough split that keeps the
         // pool simple without encoding gender separately for each culture).
         var filtered = new List<string>();
         for (var i = 0; i < pool.Length; i++)
         {
            bool femaleSlot = i % 2 != 0;
            if (femaleSlot == isFemale) filtered.Add(pool[i]);
         }

         string[] draw = filtered.Count > 0
            ? filtered.ToArray()
            : pool;

         return draw[_rng.Next(draw.Length)];
      }

      #endregion
   }
}