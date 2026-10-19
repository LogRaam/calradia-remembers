// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 companion hardening. Two guards on a companion's lifecycle transitions, each closing a MAJEUR audit
// bug where the world moved between the promise and its execution:
//   - MayDepartNow: the happiness/war-weariness loops fired an ORGANIC departure (quit / retire) with no check
//     that the companion was AWAY on an errand. A companion mid-errand cannot walk out of your service; the
//     errand owns them until it resolves, or they would depart while still abroad on your business.
//   - MayReintegrate: a returning errand-runner was re-added to the player's party with no check that they are
//     still one of the player's own clan companions. A companion EXPELLED (or who left the clan) while away
//     would silently reappear in the party on return; they must be dropped instead.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>
    ///   Pure gates for companion lifecycle transitions the engine drives: whether one may organically depart
    ///   right now, and whether a returning errand-runner may be re-added to the player's party.
    /// </summary>
    public static class CompanionLifecyclePolicy
    {
        /// <summary>
        ///   True when the companion may quit or retire organically now. A companion AWAY on an errand may not:
        ///   they are abroad on the player's business and cannot resolve to leave until the errand ends.
        /// </summary>
        public static bool MayDepartNow(bool isAwayOnErrand) => !isAwayOnErrand;

        /// <summary>
        ///   True when a returning companion may be re-added to the player's party: only if they are STILL one of
        ///   the player's own clan companions. Expelled or defected while away, they must not reappear in the
        ///   party; the errand is torn down and the tracking dropped instead.
        /// </summary>
        public static bool MayReintegrate(bool isPlayerCompanion, bool isInPlayerClan)
            => isPlayerCompanion && isInPlayerClan;
    }
}
