// Code written by Gabriel Mailhot, 12/07/2026.
// Pure RSVP decision: does an invited hero accept a summons to an assembly, and if not, why. Deterministic so
// the reasons are documented by tests. Order matters: availability gates first (a prisoner cannot even
// consider it), then the ground (no one enters an enemy's town), then willingness, then relevance. The four
// constants are the honest tuning knobs, flagged for play-testing.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Reads an <see cref="AssemblyInvitationFacts" /> into an accept/decline plus a reason.</summary>
    public static class AssemblyRsvpPolicy
    {
        // ── Tuning (TUNABLE: adjust once play-tested) ──────────────────────────

        /// <summary>Below this regard, an ordinary (non-enemy) invitee will not heed the summoner.</summary>
        public const int HeedRegardFloor = -20;

        /// <summary>Below this regard, even a parley is refused: the invitee hates the summoner too much to talk.</summary>
        public const int ParleyRegardFloor = -60;

        /// <summary>At or beyond this distance tier, a lukewarm invitee judges the journey not worth it.</summary>
        public const int FarDistanceTier = 3;

        /// <summary>The regard a far invitee needs before a long journey is worth making.</summary>
        public const int WorthTravelRegard = 10;

        public static RsvpOutcome Decide(AssemblyInvitationFacts f)
        {
            // A companion already in the summoner's own party is present without RSVP or travel.
            if (f.IsCompanionOfInitiator)
                return new RsvpOutcome(RsvpDecision.Accept, RsvpReason.Accepted);

            // Availability: dead, imprisoned, or pinned by a siege or battle cannot attend at all.
            if (!f.IsAlive || f.IsPrisoner || f.IsOccupied)
                return new RsvpOutcome(RsvpDecision.Decline, RsvpReason.DeclinedUnavailable);

            // Ground: no one walks into the territory of a faction they are at war with. A parley with an enemy
            // therefore requires NEUTRAL ground, a constraint that emerges here rather than being coded apart.
            if (f.HostOwnedByInviteesEnemy)
                return new RsvpOutcome(RsvpDecision.Decline, RsvpReason.DeclinedEnemyGround);

            // Willingness: too little regard to heed the call. An enemy invited to parley has a LOWER bar (they
            // will come to talk unless they utterly hate the summoner) than an ally, who merely must not resent them.
            int floor = f.InitiatorAtWarWithInvitee ? ParleyRegardFloor : HeedRegardFloor;
            if (f.RegardToInitiator < floor)
                return new RsvpOutcome(RsvpDecision.Decline, RsvpReason.DeclinedHostile);

            // Relevance / priority: the player cannot summon the whole map. A far host is only worth the journey
            // when the relationship justifies it.
            if (f.DistanceTier >= FarDistanceTier && f.RegardToInitiator < WorthTravelRegard)
                return new RsvpOutcome(RsvpDecision.Decline, RsvpReason.DeclinedNotWorthTravel);

            return new RsvpOutcome(RsvpDecision.Accept, RsvpReason.Accepted);
        }
    }
}
