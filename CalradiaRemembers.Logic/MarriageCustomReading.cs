// Code written by Gabriel Mailhot, 08/10/2026.
// How strictly a culture holds marriage, as a mod may read it (tashmetu, Nexus, 08/10/2026: Regard and Standing weighs
// affairs by CR's own jealousy cultures and read them from internal types). The reading is the one CR plays by.

namespace CalradiaRemembers.Logic
{
   /// <summary>A culture's marriage custom: strictness 0 (low) to 2 (high), and whether a house of the tier asked shares partners.</summary>
   public readonly struct MarriageCustomReading
   {
      public MarriageCustomReading(int strictness, bool sharesPartners)
      {
         Strictness = strictness;
         SharesPartners = sharesPartners;
      }

      /// <summary>0 low (Khuzait, Aserai...), 1 medium (Battania), 2 high (Vlandia, the Empire...).</summary>
      public int Strictness { get; }

      /// <summary>The culture accepts a shared partner in a house of the clan tier asked.</summary>
      public bool SharesPartners { get; }

      /// <summary>Reads a jealousy culture for a clan tier, by CR's own rule (ToleratesSharedPartners).</summary>
      public static MarriageCustomReading Of(JealousyCulture culture, int clanTier)
      {
         culture ??= JealousyCulture.Monogamous;
         int strictness = culture.Outrage == JealousyOutrage.Low ? 0 : culture.Outrage == JealousyOutrage.Medium ? 1 : 2;

         return new MarriageCustomReading(strictness, culture.ToleratesSharedPartners(clanTier));
      }
   }
}
