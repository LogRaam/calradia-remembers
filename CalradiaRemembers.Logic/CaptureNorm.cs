// Code written by Gabriel Mailhot, 08/10/2026.
// How a lord taken captive by the player is framed to those who hear of it. Fakade (Nexus, 08/10/2026): lords were
// wary or offended that the player took prisoners, which they do themselves. No rule called it an offence: the news
// carried no stake and no framing, and the model filled the gap. Gabriel: keep every consequence, frame the deed.

namespace CalradiaRemembers.Logic
{
   /// <summary>What the captive is to the one who hears of the capture.</summary>
   public enum CaptureStake
   {
      None,
      Realm,
      Kin
   }

   /// <summary>The framing of a lord taken captive by the player. Pure.</summary>
   public static class CaptureNorm
   {
      /// <summary>The world event status a player's capture of a lord carries.</summary>
      public const string Status = "lord_captured";

      /// <summary>The closest stake: kin over realm over none.</summary>
      public static CaptureStake StakeOf(bool kin, bool realm) => kin ? CaptureStake.Kin : realm ? CaptureStake.Realm : CaptureStake.None;

      /// <summary>The line told after the news of the capture.</summary>
      public static string Line(CaptureStake stake)
      {
         const string norm = "Taking a defeated lord captive is an ordinary act of war: you and your peers do it yourselves, and a captive "
                             + "is ransomed or freed in time. What would truly offend is how a captive is kept: held on with no ransom, "
                             + "mistreated, or worse. Let your own nature colour it.";

         return stake switch {
            CaptureStake.Kin => norm + " But he is your own kin: you feel it, and you want him back unharmed.",
            CaptureStake.Realm => norm + " He is of your realm: a loss for your side, taken as war takes.",
            _ => norm
         };
      }
   }
}
