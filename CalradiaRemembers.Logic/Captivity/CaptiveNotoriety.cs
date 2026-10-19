// Code written by Gabriel Mailhot, 03/10/2026.
// Lordfadooboo (Nexus, 03/10/2026): Rhagaea, empress, in an Aserai cell, said "few men of the hills trouble
// themselves with the affairs of empresses held in foreign cells". Her prompt said no news reaches her (right) and
// never that her capture is the news; and CR recorded a capture only when the player took part. A ruler's capture,
// or a landed house head's, is talked of: the captive is told so, and the world hears it. A lesser lord's is not,
// since the AI takes them all the time.

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>Whose capture is talked of, and how far the news carries. Pure.</summary>
   public static class CaptiveNotoriety
   {
      /// <summary>What the world knows the captive as, said to her; null when her capture is no news.</summary>
      public static string Describe(Stature stature, bool isClanLeader)
      {
         if (stature.IsRuler) return "a ruler";

         return isClanLeader && stature.IsLandedNoble ? "the head of a noble house" : null;
      }

      /// <summary>How far the news of the capture carries (0 = not recorded as news).</summary>
      public static int NewsMagnitude(Stature stature, bool isClanLeader)
      {
         if (stature.IsRuler) return 65;
         if (!isClanLeader || !stature.IsLandedNoble) return 0;

         return stature.IsGreatHouse ? 45 : 35;
      }
   }
}
