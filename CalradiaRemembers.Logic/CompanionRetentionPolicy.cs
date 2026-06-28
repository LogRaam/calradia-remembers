// Code written by Gabriel Mailhot, 28/06/2026.
// Companion retention — the pure decision logic for how an unhappy companion leaves your service. The design rule
// is that NO companion ever departs silently: an unhappy one keeps ASKING for an audience; if the player hears
// them out they are mollified (the engine resets the refusals and lifts their spirits); if the player keeps
// REFUSING the audience, after a few refusals they give up and leave by a farewell letter. This stays pure and
// unit-tested; the engine owns the inquiry, the conversation, the letter, and the actual removal.

namespace CalradiaRemembers.Logic
{
   /// <summary>What the engine should do this tick for an unhappy companion.</summary>
   public enum RetentionStep
   {
      /// <summary>Nothing to do (contentment off, or the companion is not unhappy enough to leave).</summary>
      None,

      /// <summary>The companion asks the player for an audience — a chance to be talked round.</summary>
      RequestAudience,

      /// <summary>Out of patience after too many refused audiences — write the farewell letter and leave on its arrival.</summary>
      SendFarewell
   }

   /// <summary>
   ///   Pure rules for the unhappy-companion retention loop: whether to ask for an audience, how a refusal or a
   ///   granted audience moves the count, and when patience finally runs out into a farewell.
   /// </summary>
   public static class CompanionRetentionPolicy
   {
      /// <summary>Refused audiences a companion tolerates before they give up and leave. Tunable.</summary>
      public const int MaxAudienceRefusals = 3;

      /// <summary>
      ///   The step to take for a companion this tick, given whether the system is enabled, whether they are
      ///   unhappy enough to leave, and how many audiences the player has already refused.
      /// </summary>
      public static RetentionStep Decide(bool contentmentEnabled, bool wantsToLeave, int refusals)
      {
         if (!contentmentEnabled || !wantsToLeave) return RetentionStep.None;

         return refusals >= MaxAudienceRefusals
            ? RetentionStep.SendFarewell
            : RetentionStep.RequestAudience;
      }

      /// <summary>One more refused audience. Never grows past the leave threshold (the next decide sends the farewell).</summary>
      public static int RegisterRefusal(int refusals) => refusals < 0 ? 1 : refusals + 1;

      /// <summary>The player heard them out — the grievance is spent; refusals reset to zero.</summary>
      public static int OnAudienceGranted() => 0;

      /// <summary>True once enough audiences have been refused that the companion will leave on the next decide.</summary>
      public static bool IsOutOfPatience(int refusals) => refusals >= MaxAudienceRefusals;
   }
}
