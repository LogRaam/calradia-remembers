// Code written by Gabriel Mailhot, 28/10/2026.
// Reads a character's station from the live game and composes its definition (ModuleData/stations/<key>.txt) for the
// player's rung. Gabriel (28/10/2026): one definition per social status, injected by composition, adjusting to the player's
// standing. avalanche24 (Nexus, 28/10/2026): companions and a landowner spoke "way above their station".

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Social;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>A character's station, and what its definition says.</summary>
   public static class SocialStationReader
   {
      /// <summary>The station key of <paramref name="hero" />, or null when no definition covers them.</summary>
      public static string StationOf(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero) return null;

         return SocialStationPolicy.Resolve(new SocialStationFacts {
            IsRuler = StatureReader.Of(hero).IsRuler,
            IsNoble = hero.IsLord,
            HeadsHouse = hero.Clan?.Leader == hero,
            IsPlayerCompanion = hero.CompanionOf == Clan.PlayerClan && Clan.PlayerClan != null,
            Occupation = hero.Occupation.ToString()
         });
      }

      /// <summary>Who <paramref name="hero" /> is, from their station's definition; null when none says.</summary>
      public static string Who(Hero hero) => SocialStationPolicy.Who(Definition(hero));

      /// <summary>How <paramref name="hero" /> treats the player on <paramref name="rung" />; null when none says.</summary>
      public static string Toward(Hero hero, PlayerRung rung) => SocialStationPolicy.Toward(Definition(hero), rung);

      private static IReadOnlyDictionary<string, string> Definition(Hero hero) => NarrativeLoader.StationDefinition(StationOf(hero));
   }
}
