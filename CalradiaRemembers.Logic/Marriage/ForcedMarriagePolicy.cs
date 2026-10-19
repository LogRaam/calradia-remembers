// Code written by Gabriel Mailhot, 23/07/2026.
// Ratified 2026-07-23, born of a player report: a player earned the family's blessing through a real quest,
// then found the wedding silently barred by a gate no one had named (the bride's own regard). Gabriel's call:
// history sides with the family. A house that has given its blessing CAN bind its daughter for political ends,
// so the union proceeds; what the bar below MarriageEligibility.MarriageEligibilityThreshold buys instead is
// the bride's RESENTMENT, a ForcedMarriage grudge whose size scales with how far her heart actually stood from
// the vow. A grudge and not a flat malus, deliberately: it decays, it can be resolved, it tarnishes
// kindnesses, and it surfaces in the prompt and the encyclopedia, so a marriage begun in duty is a playable
// redemption arc rather than a mute penalty.

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>How a marriage sealed on the family's blessing, against the spouse's own unwon heart, is resented.</summary>
   public static class ForcedMarriagePolicy
   {
      /// <summary>
      ///   Tuning: resentment felt even at a NEAR miss. Being wed a hair short of genuine regard is still being
      ///   given away, so the floor sits above zero; combined with the smallest shortfall it lands around the
      ///   grudge-formation floor, so a marriage at 49 regard forms only the faintest grievance (or, for a
      ///   merciful nature, none at all: GrudgePolicy's trait-modulated floor keeps the final say).
      /// </summary>
      public const int BaseResentment = 12;

      /// <summary>
      ///   Tuning: the resentment ceiling. Below ExecutedKin's permanent-vendetta territory on purpose: a
      ///   forced marriage is a grave wrong, but it is one the design wants WINNABLE, and a magnitude pinned to
      ///   the top of the scale would outlive any plausible amends.
      /// </summary>
      public const int MaxResentment = 75;

      /// <summary>
      ///   True when sealing this marriage now would be a POLITICAL union rather than a love match: the
      ///   spouse's best regard for the player (their personal ledger OR the live vanilla relation, the same
      ///   two-sided read every marriage gate uses) is still below the marriage bar.
      /// </summary>
      public static bool IsForcedUnion(int bestRegard)
         => IsForcedUnion(bestRegard, MarriageEligibility.MarriageEligibilityThreshold);

      /// <summary>
      ///   Threshold-injected overload: a union is political (forced) when the best regard is below the EFFECTIVE
      ///   marriage bar, which the engine may lower from the shipped default via the MCM "Marriage Regard Needed"
      ///   setting (ModSettings.MarriageRegardThreshold). Kept pure by taking the bar as a parameter so the seal
      ///   gate (BannerlordGameStateBridge.ExecuteMarry) and the love-match gate (EncounterContextBuilder) share
      ///   the SAME effective bar and can never drift.
      /// </summary>
      public static bool IsForcedUnion(int bestRegard, int marriageBar)
         => bestRegard < marriageBar;

      /// <summary>
      ///   The grudge magnitude the wedding plants: the base plus ONE point per point of shortfall below the
      ///   marriage bar, capped. Linear on purpose, so the wound tracks the truth of the moment: a bride at 45
      ///   regard was nearly won and forms a faint, fast-fading grievance, while one dragged to the altar at
      ///   -20 starts near the ceiling. Zero when the union is not forced at all, so a caller may apply this
      ///   unconditionally after any wedding. BOTH LEDGERS, on purpose: <paramref name="bestRegard" /> is the better of
      ///   the mod's own ReputationWithPlayer and the engine's GetRelation, as a bride won on either counts as won.
      /// </summary>
      public static int GrudgeMagnitude(int bestRegard)
         => GrudgeMagnitude(bestRegard, MarriageEligibility.MarriageEligibilityThreshold);

      /// <summary>
      ///   Threshold-injected overload: the shortfall (and thus the resentment) is measured against the EFFECTIVE
      ///   marriage bar the engine used to force the union, so a wedding forced at a LOWERED bar plants a wound
      ///   sized to that same bar rather than the shipped 50.
      /// </summary>
      public static int GrudgeMagnitude(int bestRegard, int marriageBar)
      {
         if (!IsForcedUnion(bestRegard, marriageBar)) return 0;

         int shortfall = marriageBar - bestRegard;
         int magnitude = BaseResentment + shortfall;

         return magnitude > MaxResentment ? MaxResentment : magnitude;
      }
   }
}
