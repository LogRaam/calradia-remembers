// Code written by Gabriel Mailhot, 30/09/2026.
// The engine's Hero.IsAlive means only "not dead": a hero the game has taken out of the world (HeroState Disabled: not
// yet in play, or set aside by a mod, such as a custom family's late parent) reads IsAlive == true, while
// Hero.AllAliveHeroes leaves it out. Clan.Heroes keeps both the dead and the disabled. Confirmed from the game's own
// code on 30/09/2026, after Maifailun's dead father sat at a council. A check that means "is this person in the world"
// asks here; a check that means "may this person act" asks ActionGate, which asks here.

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Whether a hero is in the world at all: alive, and not taken out of it by the game.</summary>
   public static class HeroPresence
   {
      /// <summary>Alive and not disabled. What Hero.IsAlive is usually taken to mean, and does not.</summary>
      public static bool IsInTheWorld(Hero hero)
         => hero != null && hero.IsAlive && hero.HeroState != Hero.CharacterStates.Disabled;
   }
}
