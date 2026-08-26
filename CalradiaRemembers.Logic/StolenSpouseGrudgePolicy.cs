// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 3: how deep a wound the JILTED spouse carries when the player's affair ends
// their marriage. Pure so the sizing (a base sting, deepened by how much they loved the partner who left,
// tempered by a forgiving nature, and capped so it stays winnable) is a tested, one-line decision. The behavior
// plants it as a GrudgeSource.StolenSpouse grudge against the player. Get it wrong and either being cuckolded
// barely registers, or it plants a life-sentence grudge no amends could ever outlive.

namespace CalradiaRemembers.Logic
{
   /// <summary>Sizes the jilted spouse's personal grudge against the player when a stolen-love affair ends their marriage.</summary>
   public static class StolenSpouseGrudgePolicy
   {
      /// <summary>Tuning: the baseline sting of being left for the player, before love or temperament adjust it.</summary>
      public const int BaseMagnitude = 35;

      /// <summary>Tuning: divisor on the jilted spouse's (positive) relation to the partner who left; a loved partner lost cuts deeper.</summary>
      public const int LoveWoundDivisor = 3;

      /// <summary>Tuning: magnitude removed per positive point of the jilted spouse's Mercy; a forgiving nature nurses a lighter grudge.</summary>
      public const int MercyReliefPerLevel = 8;

      /// <summary>Tuning: floor, so even a mild, merciful spouse feels SOMETHING at being left for another.</summary>
      public const int MinMagnitude = 20;

      /// <summary>Tuning: cap, kept below vendetta territory so the grudge system can still let amends win them round in time.</summary>
      public const int MaxMagnitude = 80;

      /// <summary>
      ///   The grudge magnitude for a spouse left because of the player's affair. <paramref name="jiltedRelationToDeparted" />
      ///   is the jilted spouse's relation to the partner who left them (a loved partner lost hurts more);
      ///   <paramref name="jiltedMercy" /> is their vanilla Mercy trait level (a forgiving nature softens it).
      ///   Clamped to [<see cref="MinMagnitude" />, <see cref="MaxMagnitude" />].
      /// </summary>
      public static int Magnitude(int jiltedRelationToDeparted, int jiltedMercy)
      {
         int loveWound = Positive(jiltedRelationToDeparted) / LoveWoundDivisor;
         int mercyRelief = Positive(jiltedMercy) * MercyReliefPerLevel;
         int raw = BaseMagnitude + loveWound - mercyRelief;

         if (raw < MinMagnitude) return MinMagnitude;
         return raw > MaxMagnitude ? MaxMagnitude : raw;
      }

      private static int Positive(int value) => value > 0 ? value : 0;
   }
}
