// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: whether a prospective governor ACCEPTS the post the council pledged them.
// This is the FIRST governance executor's own decision seam, kept pure and deterministic (the mod's test harness
// forbids randomness, and a dice roll dressed up as "their judgment" could never be told apart from a bug by a
// test). Author's condition: the NPC MAY refuse, but MECHANICALLY, from their own standing toward the player,
// decided BEFORE the appointment is ever applied at the lift - never narrated as a refusal after the post
// already took effect. That ordering is enforced by the caller (CouncilLift.SettleAppointGovernor calls this
// BEFORE ChangeGovernorAction.Apply); this policy only supplies the verdict.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Whether a governorship pledge is accepted, and a short factual reason when it is not.</summary>
    public readonly struct GovernorAppointmentVerdict
    {
        public GovernorAppointmentVerdict(bool accepted, string refusalReason)
        {
            Accepted = accepted;
            RefusalReason = refusalReason;
        }

        public bool Accepted { get; }

        /// <summary>Null when accepted; a short factual reason when refused.</summary>
        public string RefusalReason { get; }
    }

    /// <summary>Decides whether the prospective governor accepts the post, purely from their standing toward the player.</summary>
    public static class GovernorAppointmentPolicy
    {
        /// <summary>
        ///   Tuning: standing toward the player at/above this accepts the post. Governing one of the player's
        ///   fiefs is the SAME class of decision StanceGatePolicy.DelegatedRiskTrustFloor already gates for
        ///   lending troops or influence: the NPC exposes THEMSELVES on the player's word (an appointment can be
        ///   revoked, and a fief poorly held reflects on them). This mirrors that established delegated-risk
        ///   floor (also 20) rather than inventing a second number for the same kind of trust.
        /// </summary>
        public const int StandingFloor = 20;

        /// <summary>Read when a verdict is refused for standing below the floor.</summary>
        public const string BelowStandingReason = "does not yet trust the player enough to accept the post";

        /// <summary>
        ///   Accept/refuse verdict from the prospective governor's standing toward the player alone. Deterministic
        ///   (no randomness): the SAME pledge always resolves the SAME way at the lift, so a test can pin the
        ///   boundary exactly rather than chase a flaky roll.
        /// </summary>
        public static GovernorAppointmentVerdict Decide(int standingTowardPlayer)
            => standingTowardPlayer >= StandingFloor
                ? new GovernorAppointmentVerdict(true, null)
                : new GovernorAppointmentVerdict(false, BelowStandingReason);
    }
}
