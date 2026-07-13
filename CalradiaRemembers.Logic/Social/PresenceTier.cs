// Code written by Gabriel Mailhot, 12/07/2026.
// The single vocabulary for "how is this NPC present to the player right now?", shared by every channel of
// communication (chat, letters, jealousy). One authority, one enum: a scenario reads the tier before it runs
// its own logic, rather than each re-deciding what "with the player" means (which had drifted into three
// different, inconsistent checks).

namespace CalradiaRemembers.Logic.Social
{
    /// <summary>
    ///   Where an NPC stands relative to the player at this moment, from closest to farthest. The spatial
    ///   truth is always read live from the engine (never persisted, it changes every hour as parties move);
    ///   this enum is only the shared vocabulary the pure policies reason about.
    /// </summary>
    public enum PresenceTier
    {
        /// <summary>Rides in the player's own party (or a party the player leads): side by side, day after day.</summary>
        InPlayerParty,

        /// <summary>A separate party marching in the player's army: campaigning together, seen about the camp.</summary>
        InPlayerArmy,

        /// <summary>In the same town or castle as the player just now: a shared roof, not necessarily a shared road.</summary>
        SamePlace,

        /// <summary>Elsewhere. The only tier for which "how long since we last met" is worth remarking on.</summary>
        Away
    }
}
