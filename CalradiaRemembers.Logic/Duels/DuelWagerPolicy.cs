// Code written by Gabriel Mailhot, 18/11/2026.
// The gold wager on a duel or a brawl. Aporo (Discord, 18/11/2026): "i've made a deal with one mercenary for the duel if i win,
// i will take him as a prisoner if i lose i will pay him 30000, he agreed, and he lost, and the he said he will be my
// prisoner, but i don't think he had an option to". The game held no wager at all: DuelOutcomeApplier always staked honour,
// so the model agreed to a bet nothing would ever pay. Gabriel (18/11/2026) ruled: tell the character the truth (gold is
// the only stake the game holds) and wire the gold wager. The character is told what its OWN purse can stake (an NPC cannot
// see the player's coin, see EncounterContext.PlayerPurseGold); the player's side is checked when the fight is armed, and
// the player is told plainly when their purse cannot cover it. Only a fight actually fought pays: a refusal settles nothing.
// A wager is one transfer like any other, so GoldTransferLimit holds it (Gabriel, 01/11/2026: no fortune extracted in
// roleplay); Aporo's 30,000 is held at the ceiling, and the player is told so.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>What a wager asked for at the challenge meets in the two purses.</summary>
   public sealed class DuelWagerFacts
   {
      /// <summary>The gold the challenge named; 0 when none.</summary>
      public int Asked { get; init; }

      /// <summary>The opponent's purse when the fight is armed.</summary>
      public int NpcPurse { get; init; }

      /// <summary>The player's purse when the fight is armed.</summary>
      public int PlayerPurse { get; init; }
   }

   /// <summary>Why a named wager does not ride on the fight.</summary>
   public enum DuelWagerRefusal
   {
      /// <summary>The wager rides, or none was named.</summary>
      None,

      /// <summary>The player cannot cover the sum.</summary>
      PlayerCannotCover,

      /// <summary>The opponent cannot cover the sum.</summary>
      NpcCannotCover,

      /// <summary>The sum was above the ceiling on one transfer; the wager rides at the ceiling instead.</summary>
      HeldAtCeiling
   }

   /// <summary>Reads, holds and caps a duel's gold wager. Pure.</summary>
   public static class DuelWagerPolicy
   {
      /// <summary>The cap the character is told is rounded down to this many denars, so it names a purse, not a ledger.</summary>
      public const int CapStep = 100;

      /// <summary>The largest wager: one transfer's ceiling.</summary>
      public const int MaxWager = GoldTransferLimit.MaxSingleTransfer;

      /// <summary>The gold a challenge's <c>wager</c> field names: digits only ("30,000 denars" reads 30000); 0 for none.</summary>
      public static int Read(string raw) => CoinAmount.Parse(raw);

      /// <summary>
      ///   The most the character may agree to stake: its own purse, rounded down to <see cref="CapStep" />, and never above
      ///   <see cref="MaxWager" />. 0 means no wager can ride on the fight.
      /// </summary>
      public static int PromptCap(int npcPurse)
      {
         int purse = npcPurse < MaxWager ? npcPurse : MaxWager;

         return purse <= 0 ? 0 : purse / CapStep * CapStep;
      }

      /// <summary>
      ///   The wager that rides on the fight: the sum asked (held at <see cref="MaxWager" />) when both purses cover it, else
      ///   nothing, with the reason. The player's purse is checked first, since the player is the one told.
      /// </summary>
      public static (int Held, DuelWagerRefusal Refusal) Hold(DuelWagerFacts facts)
      {
         if (facts == null || facts.Asked <= 0) return (0, DuelWagerRefusal.None);
         bool overCeiling = facts.Asked > MaxWager;
         int sum = overCeiling ? MaxWager : facts.Asked;
         if (!DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Gold, sum, facts.PlayerPurse, false))
            return (0, DuelWagerRefusal.PlayerCannotCover);
         if (!DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Gold, sum, facts.NpcPurse, false))
            return (0, DuelWagerRefusal.NpcCannotCover);

         return (sum, overCeiling ? DuelWagerRefusal.HeldAtCeiling : DuelWagerRefusal.None);
      }

      /// <summary>What the loser actually pays: the wager, or all the loser still holds if their purse shrank since.</summary>
      public static int Payable(int wager, int loserPurse)
      {
         if (wager <= 0 || loserPurse <= 0) return 0;

         return wager < loserPurse ? wager : loserPurse;
      }
   }
}
