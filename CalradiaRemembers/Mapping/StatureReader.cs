// Code written by Gabriel Mailhot, 28/09/2026.
// The ONE place the mod reads rank from the game (Gabriel, 28/09/2026: rank as a type). Before it, some twenty sites each
// read Clan.Tier, and a few also read rulership, each their own way; the engine-facts map now shows every rule that
// reads rank under this reader's name. The rungs and the rules live in Stature (pure, tested).

#region

using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads a <see cref="Stature" /> from the live game.</summary>
   public static class StatureReader
   {
      /// <summary>The player's stature: their clan's tier, and whether they rule a realm.</summary>
      public static Stature Player => Of(Hero.MainHero);

      /// <summary>A person's stature: their clan's tier, and whether they rule its realm. None for no one.</summary>
      public static Stature Of(Hero hero)
         => new Stature(hero?.Clan?.Tier ?? 0, hero?.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero);

      /// <summary>A house's stature: its tier, and whether it is its realm's ruling clan. None for no house.</summary>
      public static Stature OfClan(Clan clan)
         => new Stature(clan?.Tier ?? 0, clan?.Kingdom != null && clan.Kingdom.RulingClan == clan);
   }
}
