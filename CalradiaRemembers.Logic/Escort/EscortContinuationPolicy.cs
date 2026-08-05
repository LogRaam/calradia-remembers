// Code written by Gabriel Mailhot, 04/08/2026.
// follow_me's own daily-tick end gate (mod FollowMeBehavior.Advance): an escort promise must end CLEANLY,
// never lingering past what the player was told (a bounded term, see EscortEligibilityPolicy.TermDays) and
// never past what the fiction still supports (the escorting lord dies, is captured, or the two sides go to
// war). Kept pure and separate from EscortEligibilityPolicy (the START gate) because the two run at different
// moments against different facts: eligibility gates whether a NEW escort may begin, this one gates whether an
// ALREADY-RUNNING escort must stop.
namespace CalradiaRemembers.Logic.Escort
{
    /// <summary>Pure end-condition gate for an ALREADY-ACTIVE follow_me escort.</summary>
    public static class EscortContinuationPolicy
    {
        /// <summary>
        ///   True when the escort's term has run out, or the lord has died, been captured, or gone to war with
        ///   the player since the escort began - any one of these ends it immediately, so a promise the
        ///   fiction no longer supports can never silently keep going.
        /// </summary>
        public static bool ShouldEnd(int daysRemaining, bool isDead, bool isCaptured, bool isAtWar)
            => daysRemaining <= 0 || isDead || isCaptured || isAtWar;
    }
}
