// Code written by Gabriel Mailhot, 28/10/2026.
// Reads the player's rung and the gap at arms from the live game for PlayerRungPolicy (Gabriel, 28/10/2026: a villager is
// ruder to a nobody than to the head of a tier 3 house; avalanche24, 28/10/2026: a weakling spoke to a master of arms as
// his better). Rank comes from StatureReader, prowess from BattleFeatFacts: no second notion of either.

#region

using System;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Social;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Where the player stands beside a character, read from the live game.</summary>
   public static class PlayerRungReader
   {
      /// <summary>The player's rung today.</summary>
      public static PlayerRung Rung()
      {
         Clan clan = Clan.PlayerClan;

         return PlayerRungPolicy.Rung(new PlayerRungFacts {
            Stature = StatureReader.Player,
            SwornVassal = clan?.Kingdom != null && !clan.IsUnderMercenaryService,
            Mercenary = clan?.Kingdom != null && clan.IsUnderMercenaryService,
            HoldsFief = clan?.Settlements?.Any(s => s != null && (s.IsTown || s.IsCastle)) == true,
            Level = Hero.MainHero?.Level ?? 0
         });
      }

      /// <summary>
      ///   The line <paramref name="speaker" /> is given about the player; the rung alone when the speaker is no hero (a
      ///   commoner) or is the player. Null when the game cannot be read.
      /// </summary>
      public static string LineFor(Hero speaker)
      {
         try
         {
            if (Hero.MainHero == null) return null;
            PlayerRung rung = Rung();
            if (speaker == null || speaker == Hero.MainHero) return PlayerRungPolicy.RungLine(rung);

            return PlayerRungPolicy.Line(rung, PlayerRungPolicy.Gap(BattleFeatFacts.For(speaker), BattleFeatFacts.For(Hero.MainHero)));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RUNG] could not read where the player stands beside {speaker?.Name}: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
