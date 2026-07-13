// Code written by Gabriel Mailhot, 12/07/2026.
// The pure inputs the RSVP decision needs about ONE invited hero, relative to the assembly's host settlement
// and its initiator (the summoner). The mod reads these from the campaign; tests build them directly, so the
// decision stays engine-free and every case is documented by a test.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One invited hero's situation, as the RSVP policy needs to see it.</summary>
    public readonly struct AssemblyInvitationFacts
    {
        // ── Availability: can they physically attend at all? ──

        public bool IsAlive { get; init; }

        /// <summary>Held prisoner: cannot answer a summons.</summary>
        public bool IsPrisoner { get; init; }

        /// <summary>Besieged inside a settlement, or pinned in an active battle: cannot leave to travel.</summary>
        public bool IsOccupied { get; init; }

        // ── Ground: will they enter the host settlement? ──

        /// <summary>The host settlement is owned by a faction the invitee is at war with.</summary>
        public bool HostOwnedByInviteesEnemy { get; init; }

        // ── Willingness: do they care to heed this summoner? ──

        /// <summary>The invitee's personal regard toward the summoner (the mod's ReputationWithPlayer or its equivalent), roughly -100..+100.</summary>
        public int RegardToInitiator { get; init; }

        /// <summary>The summons is a parley: the initiator's faction and the invitee's faction are at war.</summary>
        public bool InitiatorAtWarWithInvitee { get; init; }

        // ── Relevance / priority: is the journey worth it? ──

        /// <summary>How far the host is from the invitee, bucketed (0 = same or nearby settlement, larger = farther).</summary>
        public int DistanceTier { get; init; }

        // ── Shortcut ──

        /// <summary>The invitee already travels in the initiator's own party (a companion): present without any journey or RSVP.</summary>
        public bool IsCompanionOfInitiator { get; init; }
    }
}
