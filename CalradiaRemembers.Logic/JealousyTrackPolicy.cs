// Code written by Gabriel Mailhot, 22/09/2026.
// Which of the three prices a jealous partner pays, and it is the only place that decides.
//
// Until now the answer was one fact: does her CULTURE tolerate shared partners. yozakura12 (Nexus, 22/09/2026)
// found what that costs. His Corein says, in her own voice, that she accepts the harem as long as she remains
// the senior wife, and the ledger never heard it: her people are exclusive, so every act cost her the full
// 10/25/40 with no floor, and the arithmetic ends in divorce. In a mod whose premise is that what a character
// says is true, her word moved nothing at all.
//
// Gabriel's ruling, 22/09/2026: THE CULTURE SETS THE PRICE, NOT A VETO. She may accept, and accepting makes her
// somewhat more tolerant, but she is not a woman whose people never called it betrayal in the first place.
//
// THE BOUNDARY IS THE PART THAT KEEPS THIS HONEST. What she accepted is a household, not a blank cheque. A
// liaison she was never told of is a betrayal at full price whatever she agreed to, and that is deliberately
// checked BEFORE her acceptance rather than after: the alternative is the defect this whole piece of work
// exists to correct, where one sentence buys permanent immunity (open_relationship suppresses her jealousy
// outright, for good, on no cultural check whatsoever).

namespace CalradiaRemembers.Logic
{
   /// <summary>What is known about a jealous partner when her price is being decided.</summary>
   public sealed class JealousyTrackFacts
   {
      /// <summary>
      ///   Her own people share partners at the player's standing (<c>JealousyCulture.ToleratesSharedPartners</c>,
      ///   gated on the PLAYER's clan tier, since plural marriage is the player's elite privilege).
      /// </summary>
      public bool CultureToleratesSharing { get; set; }

      /// <summary>
      ///   She has said she accepts the player's other partners, and has not withdrawn it. False is the
      ///   ordinary case and the old behaviour: silence is not consent.
      /// </summary>
      public bool PartnerAcceptedTheArrangement { get; set; }

      /// <summary>
      ///   This act falls OUTSIDE what she accepted: someone she was never told of, a bond kept from her. Her
      ///   acceptance says nothing about it, and neither does her culture's tolerance of a household.
      /// </summary>
      public bool ActIsOutsideTheArrangement { get; set; }
   }

   /// <summary>Chooses the jealousy track from what the partner's people hold and what she herself agreed to.</summary>
   public static class JealousyTrackPolicy
   {
      /// <summary>
      ///   The track, boundary first. An act outside the arrangement is a betrayal whoever she is; then a
      ///   culture that already shares needs no promise and gets the gentle track, promise or not; then her own
      ///   word; and failing all three, the price her people put on it.
      /// </summary>
      public static JealousyTrack For(JealousyTrackFacts facts)
      {
         if (facts == null) return JealousyTrack.Betrayal;
         if (facts.ActIsOutsideTheArrangement) return JealousyTrack.Betrayal;
         if (facts.CultureToleratesSharing) return JealousyTrack.Competition;
         if (facts.PartnerAcceptedTheArrangement) return JealousyTrack.AcceptedAgainstCulture;

         return JealousyTrack.Betrayal;
      }
   }
}
