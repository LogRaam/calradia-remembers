// Code written by Gabriel Mailhot, 21/09/2026.
// The most gold one action may move, in ONE place.
//
// Found by reading docs/maps/constants.md with a critical eye (21/09/2026): the number 10000 lived in five places
// under four spellings. Two were bare literals in the bridge (give_gold and take_gold), which is where the rule is
// actually enforced; three were named constants in policies that only MIRROR it (BastardBlackmailPolicy,
// GoldPledgePolicy, HushMoneyPolicy). The blackmail policy's own doc explains why that is dangerous: a demand above
// what one take_gold can carry is structurally impayable, so the player's gold is taken and no silence is bought.
// Raising the literal in the bridge alone, or lowering it, was that bug waiting for somebody to need a bigger number.
//
// Each policy keeps its own constant, because each asks its own question (the most a pledge may be, the most a hush
// payment may be) and may one day want a LOWER ceiling. What they may never do is exceed this, and now they cannot
// drift from it without saying so.

namespace CalradiaRemembers.Logic
{
   /// <summary>The ceiling on a single gold transfer, enforced by the bridge and mirrored by every policy that prices one.</summary>
   public static class GoldTransferLimit
   {
      /// <summary>
      ///   The largest amount one give_gold or take_gold may move. A guard against a hallucinated figure, not a
      ///   balance choice: a model that writes "1000000" must neither conjure that coin nor empty a purse with it.
      /// </summary>
      public const int MaxSingleTransfer = 10000;
   }
}
