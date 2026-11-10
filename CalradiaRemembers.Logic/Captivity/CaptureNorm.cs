// Code written by Gabriel Mailhot, 08/10/2026.
// How a lord taken captive by the player is framed to those who hear of it. Fakade (Nexus, 08/10/2026): lords were
// wary or offended that the player took prisoners, which they do themselves. No rule called it an offence: the news
// carried no stake and no framing, and the model filled the gap. Gabriel: keep every consequence, frame the deed.
// Fakade (Nexus, 10/11/2026): after a plain capture and a garrison dungeon, a lady's realm said she was "kept like
// chattel rather than a lady of consequence", and the lord whose garrison held another wrote that the player had
// "abandoned her". The 08/10 line itself named how a captive is kept as the true offence, without one fact of how
// she was kept, and the model invented the mistreatment. Now the line carries what the game knows (where, whose
// hold, how many days) and says plainly that nothing tells of ill treatment. NPCs never lie: they do not assert it.

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>What the captive is to the one who hears of the capture.</summary>
   public enum CaptureStake
   {
      None,
      Realm,
      Kin
   }

   /// <summary>What the game knows of a captive the player took, as the hearer would know it.</summary>
   public sealed class CaptiveKeepingFacts
   {
      /// <summary>The hearer's closest stake in the captive.</summary>
      public CaptureStake Stake { get; init; }

      /// <summary>The captive is a woman (the line speaks of her as she, not he).</summary>
      public bool CaptiveIsFemale { get; init; }

      /// <summary>The captive is still a prisoner today.</summary>
      public bool StillHeld { get; init; }

      /// <summary>The settlement whose dungeon holds the captive, null when none (held in a party, or unknown).</summary>
      public string HeldAt { get; init; }

      /// <summary>The captive is held in a settlement the hearer's own house owns.</summary>
      public bool HeldInHearersHold { get; init; }

      /// <summary>The captive rides with the player's own party as a prisoner.</summary>
      public bool HeldInPlayersParty { get; init; }

      /// <summary>Whole days since the captivity began, when still held.</summary>
      public int DaysHeld { get; init; }
   }

   /// <summary>The framing of a lord taken captive by the player. Pure.</summary>
   public static class CaptureNorm
   {
      /// <summary>The world event status a player's capture of a lord carries.</summary>
      public const string Status = "lord_captured";

      /// <summary>The closest stake: kin over realm over none.</summary>
      public static CaptureStake StakeOf(bool kin, bool realm) => kin ? CaptureStake.Kin : realm ? CaptureStake.Realm : CaptureStake.None;

      /// <summary>The line told after the news of the capture, with what is known of how the captive is kept.</summary>
      public static string Line(CaptiveKeepingFacts facts)
      {
         CaptiveKeepingFacts f = facts ?? new CaptiveKeepingFacts();
         string she = f.CaptiveIsFemale ? "she" : "he";
         string her = f.CaptiveIsFemale ? "her" : "him";
         string She = f.CaptiveIsFemale ? "She" : "He";

         string norm = "Taking a defeated lord captive is an ordinary act of war: you and your peers do it yourselves, and a captive "
                       + "is ransomed or freed in time. A captive held in a dungeon or under guard is kept as every captive is kept; "
                       + "that is no slight to " + her + ".";

         string kept = Kept(f, She, her);
         string noAbuse = " Nothing you know tells of ill treatment: never claim or hint that " + she + " was mistreated, shamed, "
                          + "neglected or abandoned, for no word of it has reached you. Let your own nature colour how you take the capture itself.";

         string stake = f.Stake switch {
            CaptureStake.Kin => " But " + she + " is your own kin: you feel it, and you want " + her + " back unharmed.",
            CaptureStake.Realm => " " + She + " is of your realm: a loss for your side, taken as war takes.",
            _ => ""
         };

         return norm + kept + noAbuse + stake;
      }

      private static string Kept(CaptiveKeepingFacts f, string She, string her)
      {
         if (!f.StillHeld) return " " + She + " is no longer a captive.";

         string days = f.DaysHeld <= 1 ? "for a day or so" : $"for {f.DaysHeld} days now";
         if (f.HeldInHearersHold)
            return " " + She + " is held in your own house's dungeon" + (string.IsNullOrWhiteSpace(f.HeldAt) ? "" : " at " + f.HeldAt) + ", " + days
                   + ", in your own keeping.";

         if (!string.IsNullOrWhiteSpace(f.HeldAt)) return " " + She + " is held in the dungeon of " + f.HeldAt + ", " + days + ".";
         if (f.HeldInPlayersParty) return " " + She + " rides as a prisoner with the one who took " + her + ", " + days + ".";

         return "";
      }
   }
}
