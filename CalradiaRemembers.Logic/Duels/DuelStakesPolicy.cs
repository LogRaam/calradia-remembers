// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0 (pure): what may be WAGERED on a duel, and what walking away from one costs before a
// crowd. Honour rides on every duel whether or not anything material is staked (relation, renown and regard
// move regardless), so the wager is the OPTIONAL extra the ratified design puts on the table from Inc 1: gold,
// or a prisoner's freedom. The rule that matters is that a wager must be one the challenger can actually
// cover, or a duel becomes a way to promise gold you do not have and a prisoner you do not hold. The forfeit
// cost is the other half: refusing (or failing to show for) a duel before witnesses must sting in renown, or
// nobody ever fights and the whole pillar is decorative.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>What is staked on a duel beyond honour, which always rides.</summary>
   public enum DuelStakeKind
   {
      /// <summary>Nothing material: only honour, renown and regard move. Always a legal stake.</summary>
      Honour,

      /// <summary>A purse. The loser pays the winner.</summary>
      Gold,

      /// <summary>A prisoner the challenger holds goes free if they lose.</summary>
      PrisonerFreedom
   }

   /// <summary>The rules on what may be wagered, and what abandoning a duel costs before witnesses.</summary>
   public static class DuelStakesPolicy
   {
      /// <summary>Tuning: renown lost for refusing or failing to show, before no witnesses at all (the private baseline).</summary>
      public const int BaseForfeitRenown = 10;

      /// <summary>Tuning: additional renown lost per witness to the refusal. A crowd makes cowardice common knowledge.</summary>
      public const int ForfeitRenownPerWitness = 4;

      /// <summary>Tuning: the most renown a single refusal can cost, however large the crowd. No one shaming is legend.</summary>
      public const int MaxForfeitRenown = 40;

      /// <summary>
      ///   Whether a proposed wager is one the challenger can actually honour. Honour is always legal (nothing
      ///   changes hands but pride). Gold is legal only for a positive amount the challenger truly has: staking
      ///   money you cannot pay is an empty promise the bridge must refuse before it is ever offered. A
      ///   prisoner's freedom is legal only when the challenger actually holds a prisoner to free.
      /// </summary>
      public static bool IsWagerLegal(DuelStakeKind kind, int goldAmount, int challengerGold, bool challengerHoldsAPrisoner)
      {
         switch (kind)
         {
            case DuelStakeKind.Honour: return true;
            case DuelStakeKind.Gold: return goldAmount > 0 && challengerGold >= goldAmount;
            case DuelStakeKind.PrisonerFreedom: return challengerHoldsAPrisoner;
            default: return false;
         }
      }

      /// <summary>
      ///   The renown cost of walking away from a duel: a private baseline that grows with the audience,
      ///   capped. Refusing a challenge no one saw is a quiet matter; refusing one before a full hall is a
      ///   lasting stain. A negative witness count floors to none.
      /// </summary>
      public static int ForfeitRenownCost(int witnessCount)
      {
         int witnesses = witnessCount < 0 ? 0 : witnessCount;
         int cost = BaseForfeitRenown + witnesses * ForfeitRenownPerWitness;

         return cost > MaxForfeitRenown ? MaxForfeitRenown : cost;
      }
   }
}
