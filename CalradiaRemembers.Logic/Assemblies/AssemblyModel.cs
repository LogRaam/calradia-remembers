// Code written by Gabriel Mailhot, 12/07/2026.
// ASSEMBLY pillar, Inc 0 (state layer): the pure vocabulary shared by the RSVP and arrival policies and by the
// mod's AssemblyBehavior. No engine dependency, so it stays unit-tested and save-shape stable.

namespace CalradiaRemembers.Logic.Assemblies
{
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
            Decision = decision;
            Reason = reason;
        }

        public RsvpDecision Decision { get; }
        public RsvpReason Reason { get; }

        public bool Accepted => Decision == RsvpDecision.Accept;
    }
}
