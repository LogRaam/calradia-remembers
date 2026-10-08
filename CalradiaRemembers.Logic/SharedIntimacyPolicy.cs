// Code written by Gabriel Mailhot, 08/10/2026.
// Whether a night another mod reports counts as shared intimacy (tashmetu, Nexus, 08/10/2026; ratified by Gabriel: never
// through a plain memory, only an explicit call held to the bar CR's own chat holds).

namespace CalradiaRemembers.Logic
{
   /// <summary>What becomes of a night another mod reports.</summary>
   public enum SharedIntimacyResult
   {
      /// <summary>Recorded as if it had happened in the chat: the memory, and the romance arc where the chat would move it.</summary>
      Recorded,

      /// <summary>CR's chat would not have allowed it (regard below the bar, a refusal of their nature): nothing is written.</summary>
      NotWelcome,

      /// <summary>Adult content is off, so CR keeps no romance at all.</summary>
      RomanceOff,

      /// <summary>No hero has that id.</summary>
      RejectedUnknownHero,

      /// <summary>No line was given, or CR's memory is not loaded.</summary>
      RejectedInvalid
   }

   /// <summary>What the decision is made on.</summary>
   public sealed class SharedIntimacyFacts
   {
      public bool RomanceOn { get; init; }
      public bool HeroKnown { get; init; }
      public bool SummaryGiven { get; init; }

      /// <summary>CR's own consent bar (IntimacyThresholdPolicy) would allow intimacy with the player now.</summary>
      public bool ChatWouldAllow { get; init; }
   }

   /// <summary>Decides a reported night. Pure.</summary>
   public static class SharedIntimacyPolicy
   {
      public static SharedIntimacyResult Decide(SharedIntimacyFacts f)
      {
         if (f == null || !f.SummaryGiven) return SharedIntimacyResult.RejectedInvalid;
         if (!f.HeroKnown) return SharedIntimacyResult.RejectedUnknownHero;
         if (!f.RomanceOn) return SharedIntimacyResult.RomanceOff;

         return f.ChatWouldAllow ? SharedIntimacyResult.Recorded : SharedIntimacyResult.NotWelcome;
      }
   }
}
