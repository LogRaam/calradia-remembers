// Code written by Gabriel Mailhot, 03/09/2026.
// Player report (Nexus): a companion asked for "a private word" as the player entered a HOSTILE village or an
// enemy castle, and answering inside that hostile entry flow closed the window and dumped the player into the
// location's encounter. A private word is a social pause: it belongs in a settlement the player is peacefully
// visiting, never mid-siege, mid-raid, or in enemy territory at war.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate: is this settlement entry a moment where a companion (or a fief notable, gated through the
    ///   same entry point) may ask the player for a private word? The host gathers the engine facts; this
    ///   decides. Any one of the three hostilities blocks the offer: a siege in progress, the settlement being
    ///   raided, or its owning faction at war with the player's.
    /// </summary>
    public static class SettlementAudienceSafetyPolicy
    {
        /// <summary>
        ///   What is happening to the settlement. Three booleans in a row, all disqualifiers, and all read as
        ///   plain true when things are at their worst.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class SettlementSafetyFacts
        {
           /// <summary>An army is at the walls.</summary>
           public bool UnderSiege { get; set; }

           /// <summary>A raid is under way.</summary>
           public bool RaidInProgress { get; set; }

           /// <summary>Its owners are at war with the player.</summary>
           public bool AtWarWithPlayer { get; set; }
        }

        public static bool IsSafeForPrivateWord(SettlementSafetyFacts facts)
        {
           if (facts == null) return false;

           bool underSiege = facts.UnderSiege;
           bool raidInProgress = facts.RaidInProgress;
           bool atWarWithPlayer = facts.AtWarWithPlayer;

           return !underSiege && !raidInProgress && !atWarWithPlayer;
        }
    }
}
