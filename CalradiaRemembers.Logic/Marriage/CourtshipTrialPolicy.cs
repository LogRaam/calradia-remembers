// Code written by Gabriel Mailhot, 11/08/2026.
// COURTSHIP TRIAL (author's story): a lady agrees to wed the player BUT sets a condition first: she rides at his
// side for a time, as a party member and not yet a wife, to take his measure. When that time is served she does
// not merely become marriageable in silence; she PROACTIVELY reaches out (a letter in her own voice) to accept.
// This pure gate answers the one temporal question the mechanism hangs on: has the trial been served? It reasons
// over plain facts only (the mod's CampaignBehaviorBase reads the engine, the accompaniment behaviors, and the
// live marriage gate, and hands them in), mirroring the split EscortContinuationPolicy / RetainerLeavePolicy
// already draw for the two accompaniment mechanics this trial rides on (follow_me and ride_with_me).

#region

#endregion

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>Pure completion gate for a courtship trial (a marriage prospect riding with the player to be tested).</summary>
   public static class CourtshipTrialPolicy
   {
      /// <summary>
      ///   How long the prospect must ride at the player's side before the trial is served: two Calradian
      ///   seasons (<see cref="ChronicleCalendar.DaysPerSeason" /> is 21, so 42 days). Long enough that the
      ///   test reads as a real courtship arc rather than a formality, short enough to complete inside one
      ///   campaign of ordinary play. A single named constant so the behavior, the self-test, and these unit
      ///   tests can never drift onto different terms.
      /// </summary>
      public static int TrialDays => 2 * ChronicleCalendar.DaysPerSeason;

      /// <summary>
      ///   True when a tracked courtship trial has been SERVED and should now open the match: the prospect is
      ///   STILL accompanying the player (they never stopped part way, which would have lapsed the trial), they
      ///   are STILL an eligible prospect (no hard blocker such as war or captivity has since fallen between
      ///   them), and they have ridden along for at least <paramref name="trialDays" /> days. All three are
      ///   required: a trial that is abandoned early, or that a war has since barred, is not a trial passed, and
      ///   the day count alone must never carry it.
      /// </summary>
      /// <param name="accompanying">The prospect is currently accompanying the player (escorting via follow_me, or a ride_with_me retainer).</param>
      /// <param name="daysAccompanied">How many days have elapsed since the trial began (today minus the recorded start day).</param>
      /// <param name="trialDays">The term the trial must serve, normally <see cref="TrialDays" />.</param>
      /// <param name="stillEligibleProspect">The pairing is still free of any HARD marriage blocker (see <see cref="MarriageBlockOverridePolicy" />).</param>
      public static bool IsComplete(bool accompanying, int daysAccompanied, int trialDays, bool stillEligibleProspect)
      {
         if (!accompanying) return false;
         if (!stillEligibleProspect) return false;

         return daysAccompanied >= trialDays;
      }
   }
}
