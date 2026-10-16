// Code written by Gabriel Mailhot, 03/09/2026.
// Player report (Nexus): a companion asked for "a private word" as the player entered a HOSTILE village or an
// enemy castle, and answering inside that hostile entry flow closed the window and dumped the player into the
// location's encounter. A private word is a social pause: it belongs in a settlement the player is peacefully
// visiting, never mid-siege, mid-raid, or in enemy territory at war.
// Gabriel, 16/10/2026: a player who slips into a town in disguise must not be called to the hall, or pulled aside,
// as though the gates had opened for them. The entry popup comes up at the gate, before the player picks the
// disguise, so the gate itself is read: the game would not let the player in openly (a crime rating, a barred owner),
// or the player is already in disguise.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate: is this settlement entry a moment where a companion (or a fief notable, gated through the
    ///   same entry point) may ask the player for a private word? The host gathers the engine facts; this
    ///   decides. Any one of the hostilities blocks the offer: a siege in progress, the settlement being raided,
    ///   its owning faction at war with the player's, or a player not openly admitted (barred at the gate, or
    ///   in disguise).
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

           /// <summary>The game would not let the player walk in openly (barred, or only by disguise), or they are disguised.</summary>
           public bool NotOpenlyAdmitted { get; set; }
        }

        public static bool IsSafeForPrivateWord(SettlementSafetyFacts facts)
        {
           if (facts == null) return false;

           bool underSiege = facts.UnderSiege;
           bool raidInProgress = facts.RaidInProgress;
           bool atWarWithPlayer = facts.AtWarWithPlayer;
           bool notOpenlyAdmitted = facts.NotOpenlyAdmitted;

           return !underSiege && !raidInProgress && !atWarWithPlayer && !notOpenlyAdmitted;
        }
    }
}
