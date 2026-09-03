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
        public static bool IsSafeForPrivateWord(bool underSiege, bool raidInProgress, bool atWarWithPlayer)
            => !underSiege && !raidInProgress && !atWarWithPlayer;
    }
}
