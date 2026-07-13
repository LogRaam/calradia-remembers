// Code written by Gabriel Mailhot, 12/07/2026.
// The one authoritative, always-live answer to "where does this NPC stand relative to the player?" for every
// channel of communication (chat context, letters, jealousy). The spatial truth is read straight from the
// engine each time it is asked, never persisted: the engine already knows, and a stored mirror would drift
// (a missed "left the party" transition would leave an NPC believing they still ride with you). Consolidates
// what used to be three inconsistent checks; HeroProximity and JealousyBehavior now delegate here.

#region

using CalradiaRemembers.Logic.Social;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Social
{
   /// <summary>Resolves an NPC's live <see cref="PresenceTier" /> and the "is beside the player" gate that most callers want.</summary>
   public static class PlayerPresenceResolver
   {
      /// <summary>
      ///   The NPC's presence relative to the player right now, from the engine's live party/army/settlement
      ///   state. Returns <see cref="PresenceTier.Away" /> when there is no way to place them beside the player.
      /// </summary>
      public static PresenceTier Resolve(Hero hero)
      {
         if (hero == null || Hero.MainHero == null) return PresenceTier.Away;

         MobileParty main = MobileParty.MainParty;
         MobileParty heroParty = hero.PartyBelongedTo;

         if (main != null && heroParty != null)
         {
            // Same party, or a party the player leads: they ride at your side.
            if (heroParty == main || heroParty.LeaderHero == Hero.MainHero) return PresenceTier.InPlayerParty;

            // A separate party marching in the player's army.
            if (main.Army != null && heroParty.Army == main.Army) return PresenceTier.InPlayerArmy;
         }

         // Sitting in the same town or castle as the player.
         Settlement here = Hero.MainHero.CurrentSettlement;
         if (here != null && hero.CurrentSettlement == here) return PresenceTier.SamePlace;

         return PresenceTier.Away;
      }

      /// <summary>
      ///   True when the NPC is effectively beside the player (any tier but <see cref="PresenceTier.Away" />).
      ///   The gate the letter and jealousy systems use: someone at your side would speak, not post a letter,
      ///   and is never "neglected".
      /// </summary>
      public static bool IsBeside(Hero hero) => Resolve(hero) != PresenceTier.Away;
   }
}
