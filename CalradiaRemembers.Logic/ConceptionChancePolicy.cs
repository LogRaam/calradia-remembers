// Code written by Gabriel Mailhot, 18/11/2026.
// The chance that an act able to cause conception leads to a child. Kámen (Discord, 18/11/2026): "I'm still having
// problems with pregnancy not being triggered... almost every model I tested had problems with it, and I have to really
// spam it". Traced the same day: even a correct impregnation_risk tag led to a child only one time in seven, a flat 15%
// rolled after the act with no setting and nothing said to the player, so a model doing its work looked like a model
// failing. Gabriel (18/11/2026) approved an MCM slider, 15% by default as before.

namespace CalradiaRemembers.Logic
{
   /// <summary>Turns the MCM's conception chance into the roll's probability. Pure.</summary>
   public static class ConceptionChancePolicy
   {
      /// <summary>The shipped chance, in percent: the 15% the roll always used.</summary>
      public const int DefaultPercent = 15;

      /// <summary>
      ///   The probability (0 to 1) that one act conceives, from the slider's percent; the default when the setting cannot be
      ///   read, and a value out of 0..100 held to its bounds.
      /// </summary>
      public static float Chance(int? percent)
      {
         int p = percent ?? DefaultPercent;
         if (p < 0) p = 0;
         if (p > 100) p = 100;

         return p / 100f;
      }
   }
}
