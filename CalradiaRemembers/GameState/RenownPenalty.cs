// Code written by Gabriel Mailhot, 06/09/2026.
// Every renown PENALTY in this mod was silently doing nothing, found 2026-09-06 when cr.testall's
// letter_leak_consequences reported "player renown 30.6 -> 30.6" after a leak that is supposed to cost 2 or 3.
//
// The cause is in the engine, and it is easy to miss because the call looks right:
//
//    GainRenownAction.Apply(hero, -3f)   ->   ApplyInternal:  if (gainedRenown > 0f) { ... }
//
// A negative is discarded without a word, and Clan.AddRenown guards the same way, so neither sanctioned path can
// take renown away. Five call sites were affected: the bastard scandal, the duel outcome, the captive-humiliation
// penalty, the leaked-letter consequence, and the player-violence consequence. All of them read as working code and
// none of them had ever moved a single point.
//
// The only path left is the Clan.Renown setter. Tier is deliberately NOT recalculated: vanilla only ever raises it,
// there is no engine precedent for demoting a clan, and a silent tier drop would quietly shrink party size and
// influence. Renown lost, rank kept.

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>Takes renown away from a hero's clan, which the engine's own GainRenownAction cannot do.</summary>
   public static class RenownPenalty
   {
      /// <summary>
      ///   Removes <paramref name="loss" /> renown from this hero's clan, never below zero, and returns how much was
      ///   actually taken (0 when there was nothing to take, or no clan). Use this for every penalty:
      ///   <c>GainRenownAction.Apply</c> with a negative is a no-op, however right it looks.
      /// </summary>
      public static float Apply(Hero hero, float loss)
      {
         Clan clan = hero?.Clan;
         if (clan == null || loss <= 0f) return 0f;

         float taken = clan.Renown < loss ? clan.Renown : loss;
         if (taken <= 0f) return 0f;

         clan.Renown -= taken;

         return taken;
      }
   }
}
