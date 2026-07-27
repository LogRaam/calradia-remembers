// Code written by Gabriel Mailhot, 12/07/2026.
// ASSEMBLY pillar, Inc 0 (state layer): the pure vocabulary shared by the RSVP and arrival policies and by the
// mod's AssemblyBehavior. No engine dependency, so it stays unit-tested and save-shape stable.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   The kind of gathering the player convenes. Each drives a different roster, session tone, and set of
    ///   resolutions. Only <see cref="Council" /> is built first (its three roster variants live in
    ///   <see cref="CouncilScope" />); the other four are the pillar's planned modes.
    /// </summary>
    public enum AssemblyType
    {
        /// <summary>Invited external lords deliberate policy (tribute, war support, patrols). Sovereign-gated.</summary>
        Political,

        /// <summary>Lords of the player's own army plan operations (missions, sieges, raids).</summary>
        WarCouncil,

        /// <summary>The player gathers his own house (companions, family, or clan lords). See <see cref="CouncilScope" />.</summary>
        Council,

        /// <summary>The player's held prisoners are judged before a jury (prison, torture, execution).</summary>
        Tribunal,

        /// <summary>Merchants and commoners petition the player, whose rulings shift the world.</summary>
        Audience
    }

    /// <summary>Which of the player's own people a <see cref="AssemblyType.Council" /> gathers.</summary>
    public enum CouncilScope
    {
        /// <summary>The player's hired companions (non-noble wanderers of his clan).</summary>
        Companions,

        /// <summary>The dynastic inner circle: spouse, siblings, and adult children.</summary>
        Family,

        /// <summary>Every hero-lord who belongs to the player's clan.</summary>
        ClanLords,

        /// <summary>A convened ASSEMBLY: a mixed roster of whoever answered the summons and reached the hall (or sent an envoy), not one house.</summary>
        Assembly
    }

    /// <summary>
    ///   The lifecycle of one motion taken in an assembly. Decisions are PROVISIONAL during the session: a motion
    ///   is only <see cref="Agreed" /> until the assembly is LIFTED, at which point agreed motions commit
    ///   (<see cref="Applied" /> or <see cref="Failed" />). Because nothing touches the world before the lift, a
    ///   <see cref="Reversed" /> motion costs nothing to undo. See <see cref="ResolutionLifecycle" />.
    /// </summary>
    public enum ResolutionStatus
    {
        /// <summary>Put on the table, not yet consented to.</summary>
        Proposed,

        /// <summary>Consented to; this is the status that commits when the assembly is lifted.</summary>
        Agreed,

        /// <summary>Withdrawn during the session before the lift; it will never commit. Terminal.</summary>
        Reversed,

        /// <summary>Committed at the lift and the bridge applied the real effect. Terminal.</summary>
        Applied,

        /// <summary>Committed at the lift but the bridge could not apply it (reason recorded). Terminal.</summary>
        Failed
    }

    /// <summary>Whether an invited hero agrees to attend an assembly.</summary>
    public enum RsvpDecision
    {
        Accept,
        Decline
    }

    /// <summary>Why an RSVP resolved the way it did, so the caller can pick a fitting reply letter and log line.</summary>
    public enum RsvpReason
    {
        /// <summary>Agreed to attend.</summary>
        Accepted,

        /// <summary>Cannot attend at all: dead, held prisoner, besieged, or locked in a battle.</summary>
        DeclinedUnavailable,

        /// <summary>The host settlement is held by a faction the invitee is at war with; a parley needs neutral ground.</summary>
        DeclinedEnemyGround,

        /// <summary>Regard for the summoner is too low to heed the call.</summary>
        DeclinedHostile,

        /// <summary>The host is too far to be worth the journey given only a lukewarm relationship (relevance/priority).</summary>
        DeclinedNotWorthTravel
    }

    /// <summary>Once an invitee has accepted, whether they can present themselves in person or send a stand-in.</summary>
    public enum ArrivalMode
    {
        /// <summary>The hero can reach the host by the deadline and attends in person.</summary>
        InPerson,

        /// <summary>The hero cannot reach the host in time; an ambassador sits in their name.</summary>
        Ambassador
    }

    /// <summary>The lifecycle status of one invited seat, persisted by the mod's AssemblyBehavior.</summary>
    public enum AssemblyInviteeStatus
    {
        /// <summary>Summoned, no reply yet.</summary>
        Invited,

        /// <summary>Agreed to attend, not yet travelling.</summary>
        Accepted,

        /// <summary>Refused the summons.</summary>
        Declined,

        /// <summary>Accepted and travelling toward the host.</summary>
        EnRoute,

        /// <summary>Physically arrived at the host settlement.</summary>
        Present,

        /// <summary>Could not arrive in time; a stand-in attends in their name.</summary>
        Ambassador
    }

    /// <summary>The one outcome of an RSVP decision: the yes/no and the reason behind it.</summary>
    public readonly struct RsvpOutcome
    {
        public RsvpOutcome(RsvpDecision decision, RsvpReason reason)
        {
            // Council audit m6: guard the pair. An Accept must carry the Accepted reason, and a Decline one of the
            // Declined* reasons; a mismatch (e.g. Accept + DeclinedHostile) would make Accepted disagree with
            // Reason, so a reply letter or log line reads one thing while the decision says another. Refuse it here
            // rather than let a wiring bug flow downstream. default(RsvpOutcome) is Accept+Accepted, a valid pair.
            if ((decision == RsvpDecision.Accept) != (reason == RsvpReason.Accepted))
                throw new System.ArgumentException($"RSVP decision {decision} does not match reason {reason}.", nameof(reason));

            Decision = decision;
            Reason = reason;
        }

        public RsvpDecision Decision { get; }
        public RsvpReason Reason { get; }

        public bool Accepted => Decision == RsvpDecision.Accept;
    }
}
