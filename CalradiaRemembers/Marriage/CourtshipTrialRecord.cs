// Code written by Gabriel Mailhot, 11/08/2026.
// The persisted shape of one courtship trial (a marriage prospect riding at the player's side to test him before
// consenting to wed). Mirrored 1:1 by CourtshipTrialBehavior's SyncData (byte[] JSON via Newtonsoft.Json, key
// "CalradiaRemembers_CourtshipTrials_v1bytes"). A plain POCO: additive, defaulted, no migration.

namespace CalradiaRemembers.Marriage
{
   /// <summary>One courtship trial: the day it began and whether it has since been served (passed).</summary>
   public sealed class CourtshipTrialRecord
   {
      /// <summary>The campaign day (CampaignTime.Now.ToDays) the prospect began accompanying the player.</summary>
      public int StartDay { get; set; }

      /// <summary>True once the trial has been served: the prospect has reached out to accept the match. Latches on.</summary>
      public bool Passed { get; set; }
   }
}
