// Code written by Gabriel Mailhot, 27/10/2026.
// The engine side of a character's fear, superstition and gift tastes (Gabriel, 27/10/2026: "un NPC pourrait [...]
// preferer certains cadeaux", "nous pourrions aussi ajouter certaines superstitions et phobies"): rolled once from who
// they are (their traits, their people, their station), read for their page, and, for a gift, how it is met.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Character;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Rolls and reads a character's fear, superstition and gift tastes.</summary>
   internal static class NatureReader
   {
      /// <summary>Rolls what was never rolled: the fear and superstition, then the gift tastes. Kept for good once rolled.</summary>
      internal static void Refresh(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null || profile == null || hero == Hero.MainHero) return;
            string culture = hero.Culture?.StringId;
            Logic.RomanticTraits traits = RomanticProfileBuilder.TraitsOf(hero);
            if (!profile.NatureRolled)
            {
               NatureRoll roll = NaturePolicy.Roll(new NatureFacts {HeroId = hero.StringId, Traits = traits, Culture = culture});
               profile.Fear = roll.Fear;
               profile.Superstition = roll.Superstition;
               profile.NatureRolled = true;
            }

            if (profile.LovedGifts == null || profile.ColdGifts == null)
            {
               GiftTastes tastes = GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = hero.StringId, Traits = traits, Culture = culture, IsNoble = hero.IsLord});
               profile.LovedGifts ??= tastes.Loved;
               profile.ColdGifts ??= tastes.Cold;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NATURE] roll failed for {hero?.Name}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   How a gift of <paramref name="item" /> is met by this character, and the player now knows that taste when it is
      ///   one they love or find cold.
      /// </summary>
      internal static GiftWelcome WelcomeOf(ItemObject item, Hero hero, NpcProfile profile)
      {
         try
         {
            if (item == null || profile == null) return GiftWelcome.Ordinary;
            Refresh(hero, profile);
            GiftKind kind = GiftTastePolicy.KindOf(item.ItemCategory?.StringId);
            GiftWelcome welcome = GiftTastePolicy.Welcome(kind, profile.LovedGifts, profile.ColdGifts);
            if (welcome != GiftWelcome.Ordinary)
            {
               List<GiftKind> known = profile.KnownGifts ?? new List<GiftKind>();
               if (!known.Contains(kind)) known.Add(kind);
               profile.KnownGifts = known;
            }

            return welcome;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NATURE] gift welcome failed: {ex.GetType().Name}: {ex.Message}");

            return GiftWelcome.Ordinary;
         }
      }

      /// <summary>
      ///   Whether this item is of a kind the character loves, so they would pay more for it (Thoragoros1, 03/11/2026). Reads
      ///   only: unlike <see cref="WelcomeOf" /> it never marks the taste learned, since a price is read every turn and no gift
      ///   was given.
      /// </summary>
      internal static bool LovesKindOf(ItemObject item, Hero hero, NpcProfile profile)
      {
         try
         {
            if (item == null || profile == null) return false;
            Refresh(hero, profile);

            return GiftTastePolicy.Welcome(GiftTastePolicy.KindOf(item.ItemCategory?.StringId), profile.LovedGifts, profile.ColdGifts) == GiftWelcome.Loved;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[NATURE] loved kind failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>The fear, superstition and gift lines of their page, only what the player has learned.</summary>
      internal static IReadOnlyList<string> PageLines(Hero hero, NpcProfile profile)
      {
         if (hero == null || profile == null) return Array.Empty<string>();
         var lines = new List<string>(NaturePolicy.PageLines(new NaturePageFacts {
            Fear = profile.Fear, FearKnown = profile.FearKnown, Superstition = profile.Superstition, SuperstitionKnown = profile.SuperstitionKnown,
            IsFemale = hero.IsFemale
         }));
         lines.AddRange(GiftTastePolicy.PageLines(new GiftPageFacts {
            Loved = profile.LovedGifts ?? new List<GiftKind>(), Cold = profile.ColdGifts ?? new List<GiftKind>(),
            Known = profile.KnownGifts ?? new List<GiftKind>(), IsFemale = hero.IsFemale
         }));

         return lines;
      }
   }
}
