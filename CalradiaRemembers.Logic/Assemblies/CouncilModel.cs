// Code written by Gabriel Mailhot, 20/07/2026.
// ASSEMBLY pillar, Council module (first slice): the pure vocabulary for deciding who sits at a clan council.
// The player convenes his OWN house, so there is no travel or RSVP here; a member is seated purely on being
// available and belonging to the chosen roster. No engine dependency, so the rules stay unit-tested.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Why a hero is (or is not) seated at a council, so the roster panel can explain an empty seat.</summary>
    public enum CouncilExclusionReason
    {
        /// <summary>Seated: the hero attends.</summary>
        None,

        /// <summary>Dead; cannot attend.</summary>
        Dead,

        /// <summary>Held prisoner; cannot attend.</summary>
        Imprisoned,

        /// <summary>Pinned in a battle right now; cannot attend.</summary>
        InBattle,

        /// <summary>Alive and free, but not a member of the roster this council gathers.</summary>
        NotOfThisCouncil,

        /// <summary>Marching with an army; cannot break away to attend, even an abstract summons.</summary>
        InArmy,

        /// <summary>Away on a companion errand; cannot attend until they return.</summary>
        OnMission
    }

    /// <summary>
    ///   The flattened facts about one hero needed to seat them at a council. The three membership flags are read
    ///   from the engine (companion of the player's clan, immediate family, clan lord) and are NOT mutually
    ///   exclusive: a sibling who leads his own party is both <see cref="IsImmediateFamily" /> and
    ///   <see cref="IsClanLord" />. The <see cref="CouncilScope" /> chosen decides which flag is consulted.
    /// </summary>
    public readonly struct CouncilMembershipFacts
    {
        public CouncilMembershipFacts(bool isAlive,
                                      bool isPrisoner,
                                      bool isInBattle,
                                      bool isCompanion,
                                      bool isImmediateFamily,
                                      bool isClanLord,
                                      bool isInArmy,
                                      bool isOnMission,
                                      bool isAlliedLeader = false)
        {
            IsAlive = isAlive;
            IsPrisoner = isPrisoner;
            IsInBattle = isInBattle;
            IsCompanion = isCompanion;
            IsImmediateFamily = isImmediateFamily;
            IsClanLord = isClanLord;
            IsInArmy = isInArmy;
            IsOnMission = isOnMission;
            IsAlliedLeader = isAlliedLeader;
        }

        public bool IsAlive { get; }
        public bool IsPrisoner { get; }

        /// <summary>The hero's party is in a MapEvent (a battle) right now.</summary>
        public bool IsInBattle { get; }

        /// <summary>A hired companion (non-noble wanderer) of the player's clan.</summary>
        public bool IsCompanion { get; }

        /// <summary>The player's spouse, a sibling, or an adult child.</summary>
        public bool IsImmediateFamily { get; }

        /// <summary>A hero-lord who belongs to the player's clan.</summary>
        public bool IsClanLord { get; }

        /// <summary>The hero's party is attached to an army right now; cannot break away to attend.</summary>
        public bool IsInArmy { get; }

        /// <summary>The hero is away on a companion errand (CompanionMissionBehavior.IsAway); cannot attend until they return.</summary>
        public bool IsOnMission { get; }

        /// <summary>
        ///   The WarCouncil's own roster fact: a clan LEADER allied to the player (a fellow clan's leader within
        ///   the player's kingdom), or, for an independent player clan (no kingdom), one of the player's own clan
        ///   lords (the roster's no-kingdom fallback, see CouncilRosterResolver.CandidatePool). Not mutually
        ///   exclusive with the others: a sibling who leads his own clan could in principle also be an allied
        ///   leader, though the common case is disjoint from IsClanLord (a DIFFERENT clan's leader).
        /// </summary>
        public bool IsAlliedLeader { get; }
    }

    /// <summary>Whether a hero is seated at a council, and if not, the reason to show on the roster.</summary>
    public readonly struct CouncilEligibility
    {
        public CouncilEligibility(bool eligible, CouncilExclusionReason reason)
        {
            Eligible = eligible;
            Reason = reason;
        }

        public bool Eligible { get; }
        public CouncilExclusionReason Reason { get; }
    }
}
