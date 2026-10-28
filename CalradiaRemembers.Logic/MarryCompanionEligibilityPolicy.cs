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
      /// <summary>
      ///   What a match between the player and one of their own companions rests on, read off the campaign by
      ///   the caller.
      ///   <para>
      ///     Named rather than positional (2026-09-10): this took SEVEN booleans in a row, and cr.test all called
      ///     it as CanMarry(true, true, true, true, false, false, true), seven bare values with nothing to say
      ///     which was which. Inverting "already wed" and "blood kin" there would have compiled, passed, and
      ///     quietly tested the wrong rule.
      ///   </para>
      /// </summary>
      public sealed class MarryCompanionFacts
      {
         /// <summary>They are one of the player's own companions, which is what this policy is about.</summary>
         public bool IsCompanion { get; set; }

         /// <summary>They are alive.</summary>
         public bool IsAlive { get; set; }

         /// <summary>They are of age.</summary>
         public bool IsAdult { get; set; }

         /// <summary>Their orientation admits the player.</summary>
         public bool OrientationCompatibleWithPlayer { get; set; }

         /// <summary>They are already married to somebody.</summary>
         public bool AlreadyWed { get; set; }

         /// <summary>They share blood with the player, which no match may cross.</summary>
         public bool IsBloodKinOfPlayer { get; set; }

         /// <summary>The player is free to wed: unmarried, or in a culture that admits another spouse.</summary>
         public bool PlayerCanTakeAnotherSpouse { get; set; }
      }

      /// <summary>Whether the player may wed this companion. Every condition must hold; unknown facts refuse.</summary>
      public static bool CanMarry(MarryCompanionFacts facts)
         => facts != null
            && facts.IsCompanion
            && facts.IsAlive
            && facts.IsAdult
            && facts.OrientationCompatibleWithPlayer
            && !facts.AlreadyWed
            && !facts.IsBloodKinOfPlayer
            && facts.PlayerCanTakeAnotherSpouse;
   }
}
