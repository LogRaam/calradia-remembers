// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (SuperGraham339, 2026-07-10): a lady in the player's own ARMY kept sending "I miss you" letters.
// The old check only covered the player's own party, so an NPC marching in the same army, or sitting in the same
// town or castle, still "wrote from afar". This is the single authoritative notion of "near the player" for the
// letter system: such an NPC does not write, they would simply speak.

#region

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>One authoritative answer to "is this NPC effectively beside the player right now?"</summary>
   public static class HeroProximity
   {
      /// <summary>
      ///   True when the NPC is travelling with the player (same party, a party the player leads, or a party
      ///   marching in the player's army) or is in the same town or castle as the player. Near people talk,
      ///   they do not post letters.
      /// </summary>
      public static bool IsNearPlayer(Hero hero)
      {
         if (hero == null || Hero.MainHero == null) return false;

         MobileParty main = MobileParty.MainParty;
         MobileParty heroParty = hero.PartyBelongedTo;

         // Same party, or a party the player leads.
         if (main != null && heroParty != null)
         {
            if (heroParty == main || heroParty.LeaderHero == Hero.MainHero) return true;

            // Marching in the same army (the reported case: a lady invited into the player's army).
            if (main.Army != null && heroParty.Army == main.Army) return true;
         }

         // Sitting in the same town or castle as the player.
         Settlement here = Hero.MainHero.CurrentSettlement;

         return here != null && hero.CurrentSettlement == here;
      }
   }
}
