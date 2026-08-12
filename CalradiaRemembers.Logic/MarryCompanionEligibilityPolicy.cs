// Code written by Gabriel Mailhot, 11/08/2026.
// SPIKE (marry a companion): the CARVE-OUT rule, kept pure and tested no matter what the engine verdict is.
// Players want to wed one of their OWN wanderer companions. Today the mod's marriage resolver returns the HARD
// MarriageBlockReason.SameClan for anyone in the player's clan (EncounterContextBuilder.ResolveMarriageBlockReason),
// and a companion IS in the player's clan (CompanionOf == PlayerClan), so the match is barred exactly as vanilla
// bars marrying your own kin. But a wanderer companion is NOT blood kin of the player: the SameClan custom exists
// to forbid incest, and a hired stranger who happens to share the clan banner is not the case that custom is about.
// This table states, in one tested place, WHO the carve-out would admit: a companion of compatible sex/orientation,
// adult, unwed, NOT blood kin, when the player can still take a spouse. It is deliberately blind to the engine
// question of whether MarriageAction can currently seal such a union (that is the spike's separate finding); it
// only pins the mod-side eligibility a future SameClan carve-out would consult, so the decision can never silently
// widen to admit a blood relative or a second spouse.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure eligibility rule for the "marry your own companion" carve-out: which same-clan wanderer companion
   ///   the mod would let the player wed despite the ordinary <c>SameClan</c> bar. A companion is not blood kin,
   ///   so the incest custom the bar enforces does not reach them; every other marriage invariant still holds.
   /// </summary>
   public static class MarryCompanionEligibilityPolicy
   {
      /// <summary>
      ///   True only when EVERY condition for wedding the player to one of their own companions holds. The
      ///   carve-out admits a hero who is a <paramref name="isCompanion" /> (a hired wanderer of the player's
      ///   clan, the one same-clan case that is not kin), <paramref name="isAlive" />,
      ///   <paramref name="isAdult" /> (of marriageable age), of a sex/orientation compatible with the player
      ///   (<paramref name="orientationCompatibleWithPlayer" />), NOT <paramref name="alreadyWed" />, NOT
      ///   <paramref name="isBloodKinOfPlayer" /> (the one line the SameClan bar truly exists to hold, and the
      ///   only same-clan hero it must keep barring), and only while the player can still take a spouse
      ///   (<paramref name="playerCanTakeAnotherSpouse" />). Any single failure closes the match: this is a
      ///   conjunction so a new admission can never slip in without being stated here.
      /// </summary>
      public static bool CanMarry(bool isCompanion,
                                  bool isAlive,
                                  bool isAdult,
                                  bool orientationCompatibleWithPlayer,
                                  bool alreadyWed,
                                  bool isBloodKinOfPlayer,
                                  bool playerCanTakeAnotherSpouse)
      {
         return isCompanion
             && isAlive
             && isAdult
             && orientationCompatibleWithPlayer
             && !alreadyWed
             && !isBloodKinOfPlayer
             && playerCanTakeAnotherSpouse;
      }
   }
}
