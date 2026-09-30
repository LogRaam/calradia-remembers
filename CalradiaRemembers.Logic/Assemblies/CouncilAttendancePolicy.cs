// Code written by Gabriel Mailhot, 29/08/2026.
// Convergence Inc 1 of 5: the single decider of how one candidate hero shows up at a council, whatever kind of
// council it is. This consolidates three pieces that used to each answer a fragment of the same question:
// CouncilRosterPolicy (prisoner/scope gating), AssemblyRsvpPolicy (relation/war/distance decline), and the
// assembly arrival/ambassador split (AssemblyArrivalPolicy). Pure and engine-agnostic; a later increment
// projects CouncilAttendanceFacts from live hero/party/faction state and wires this in.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>How a candidate hero shows up at a council once <see cref="CouncilAttendancePolicy.Decide" /> has ruled.</summary>
    public enum CouncilAttendance
    {
        /// <summary>Physically present at the table.</summary>
        InPerson,

        /// <summary>A stand-in speaks in the hero's name. NEVER used for the player's own faction, see Decide.</summary>
        Ambassador,

        /// <summary>Not seated at all.</summary>
        Absent
    }

    /// <summary>Why a <see cref="CouncilAttendance" /> resolved the way it did, for diagnostics and testing.</summary>
    public enum CouncilAttendanceReason
    {
        /// <summary>Held prisoner: bars every form of attendance, checked before anything else.</summary>
        Prisoner,

        /// <summary>Already riding in the player's party: present without travel or willingness checks.</summary>
        InParty,

        /// <summary>An other-faction lord sought as the representative for a parley: a stand-in attends.</summary>
        ParleyEnvoy,

        /// <summary>Own faction, willing, and close enough: attends in person.</summary>
        NearbyOwnFaction,

        /// <summary>Own faction and willing, but too far to reach the table by the threshold: stays home.</summary>
        TooFar,

        /// <summary>Own faction but unwilling: at open war with the player, or regard at/below the hostile floor.</summary>
        Declined,

        /// <summary>An other-faction candidate not sought for a parley: not a council candidate at all.</summary>
        OtherFactionNotInvited
    }

    /// <summary>
    ///   The engine-agnostic facts one candidate hero is judged on. The mod projects these from live TaleWorlds
    ///   hero/party/faction/relation state in a later increment; nothing here is engine-bound, which keeps the
    ///   decision unit-tested and every branch documented by a test.
    /// </summary>
    public readonly struct CouncilAttendanceFacts
    {
        /// <summary>Held prisoner right now. Bars every form of attendance, regardless of every other flag.</summary>
        public bool IsPrisoner { get; init; }

        /// <summary>Riding in the player's own party right now: present without travel or RSVP.</summary>
        public bool IsInPlayerParty { get; init; }

        /// <summary>A lord of the player's OWN faction: the player's own clan, or an allied clan of the player's kingdom.</summary>
        public bool IsPlayerFaction { get; init; }

        /// <summary>An OTHER-faction lord specifically sought as the representative for a parley.</summary>
        public bool IsParleyTarget { get; init; }

        /// <summary>Estimated days of travel to reach the council's host. Only consulted for the player's own faction.</summary>
        public double EtaDays { get; init; }

        /// <summary>The candidate's regard for the player (the mod's own personal ledger, not vanilla relation).</summary>
        public int RelationToPlayer { get; init; }

        /// <summary>The candidate's faction is at open war with the player right now.</summary>
        public bool AtWarWithPlayer { get; init; }
    }

    /// <summary>The one outcome of an attendance decision: how the hero shows up, and why.</summary>
    public readonly struct CouncilAttendanceResult
    {
        public CouncilAttendanceResult(CouncilAttendance attendance, CouncilAttendanceReason reason)
        {
            Attendance = attendance;
            Reason = reason;
        }

        public CouncilAttendance Attendance { get; }
        public CouncilAttendanceReason Reason { get; }
    }

    /// <summary>
    ///   Reads a <see cref="CouncilAttendanceFacts" /> for one candidate hero into a single
    ///   <see cref="CouncilAttendanceResult" />: In Person, Ambassador, or Absent. This is the convergence point
    ///   for CouncilRosterPolicy's prisoner/scope gating, AssemblyRsvpPolicy's relation/war/distance decline, and
    ///   AssemblyArrivalPolicy's arrival/ambassador split, so the two systems answer one seating question instead
    ///   of three overlapping ones. Ambassadors are ALWAYS other-faction: the player's own house either shows up
    ///   or stays home, it never sends a stand-in to its own table (the player report this guards against is a
    ///   war council that "felt magic", distant nobility of the player's own realm appearing in person when they
    ///   should have stayed home, or worse, standing in by ambassador as if that were ever how one's own house
    ///   behaves).
    /// </summary>
    public static class CouncilAttendancePolicy
    {
        /// <summary>
        ///   Below or AT this regard, an own-faction candidate no longer heeds the summons. Reused from
        ///   <see cref="AssemblyRsvpPolicy.HeedRegardFloor" /> (-20) rather than re-derived, so the two systems
        ///   agree on what counts as hostile. Note the boundary differs from the RSVP policy's own reading:
        ///   AssemblyRsvpPolicy declines only STRICTLY below the floor (regard AT the floor still accepts); the
        ///   ratified rule for this convergence is inclusive ("regard not AT OR BELOW a hostile floor" declines),
        ///   so the same numeric floor is reused but the comparison here is `&lt;=`.
        /// </summary>
        public const int HostileRegardFloor = AssemblyRsvpPolicy.HeedRegardFloor;

        /// <summary>
        ///   Decides attendance in ratified order. (1) A prisoner is always Absent, before anything else. (2) A
        ///   candidate already in the player's party always attends InPerson, regardless of faction or distance.
        ///   (3) A parley target is always represented by an Ambassador; proximity and faction checks do not
        ///   apply to them. (4) The player's own faction attends InPerson only if willing (not at open war with
        ///   the player, and regard above <see cref="HostileRegardFloor" />) AND close enough
        ///   (<paramref name="etaDays" /> at or under <paramref name="thresholdDays" />); otherwise Absent. An
        ///   own-faction candidate is NEVER an Ambassador. (5) Any other other-faction candidate (not a parley
        ///   target) is Absent: not a council candidate at all. <paramref name="thresholdDays" /> is a parameter
        ///   (the MCM-configured value flows in from a later increment), never hardcoded here.
        /// </summary>
        public static CouncilAttendanceResult Decide(CouncilAttendanceFacts facts, double thresholdDays)
        {
            if (facts.IsPrisoner)
                return new CouncilAttendanceResult(CouncilAttendance.Absent, CouncilAttendanceReason.Prisoner);

            if (facts.IsInPlayerParty)
                return new CouncilAttendanceResult(CouncilAttendance.InPerson, CouncilAttendanceReason.InParty);

            if (facts.IsParleyTarget)
                return new CouncilAttendanceResult(CouncilAttendance.Ambassador, CouncilAttendanceReason.ParleyEnvoy);

            if (facts.IsPlayerFaction)
            {
                if (facts.AtWarWithPlayer || facts.RelationToPlayer <= HostileRegardFloor)
                    return new CouncilAttendanceResult(CouncilAttendance.Absent, CouncilAttendanceReason.Declined);

                return facts.EtaDays <= thresholdDays
                    ? new CouncilAttendanceResult(CouncilAttendance.InPerson, CouncilAttendanceReason.NearbyOwnFaction)
                    : new CouncilAttendanceResult(CouncilAttendance.Absent, CouncilAttendanceReason.TooFar);
            }

            return new CouncilAttendanceResult(CouncilAttendance.Absent, CouncilAttendanceReason.OtherFactionNotInvited);
        }
    }
}
