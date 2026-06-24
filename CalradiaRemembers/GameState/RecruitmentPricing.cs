// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Decides whether an NPC is a genuinely recruitable wanderer and what their vanilla
   ///   hiring price is. The single authority used by the prompt feed
   ///   (EncounterContextBuilder), the cash-hire action (ExecuteJoinParty), and the
   ///   item-barter bargain (QuestFactory / grant settlement) — so all three agree on who
   ///   can be hired and the price the rest of the economy is measured against.
   /// </summary>
   public static class RecruitmentPricing
   {
      /// <summary>
      ///   The premium for paying a hire in goods rather than coin: goods are less liquid
      ///   and less convenient to the NPC, so they demand twice the cash value in kind.
      /// </summary>
      public const float InKindPremiumMultiplier = 2.0f;

      /// <summary>
      ///   The vanilla hiring price when <paramref name="hero" /> is a free, living wanderer
      ///   the player's clan can still take on; null when the hire could not succeed (so the
      ///   NPC is never taught to agree to a deal the game would refuse).
      /// </summary>
      public static int? GetHiringPrice(Hero hero)
      {
         try
         {
            if (!IsRecruitable(hero)) return null;
            int price = Campaign.Current.Models.CompanionHiringPriceCalculationModel
                                .GetCompanionHiringPrice(hero);

            return price > 0
               ? price
               : null;
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   The denar value of goods required to hire <paramref name="hero" /> in kind:
      ///   the hiring price times <see cref="InKindPremiumMultiplier" />. Null when the
      ///   hire could not succeed.
      /// </summary>
      public static int? GetInKindRequiredValue(Hero hero)
      {
         int? price = GetHiringPrice(hero);

         if (price == null) return null;

         return Math.Max(1, (int) (price.Value * InKindPremiumMultiplier));
      }

      /// <summary>
      ///   True when <paramref name="hero" /> is a living, free wanderer in nobody's service
      ///   and the player's clan still has a companion slot — the same gate the cash hire
      ///   re-checks before executing.
      /// </summary>
      public static bool IsRecruitable(Hero hero)
      {
         if (hero is not {IsAlive: true} || hero.IsPrisoner) return false;
         if (hero.Occupation != Occupation.Wanderer) return false;
         if (hero.CompanionOf != null || hero.PartyBelongedTo != null) return false;

         var clan = Clan.PlayerClan;

         if (clan == null) return false;

         return clan.Companions.Count < clan.CompanionLimit;
      }
   }
}