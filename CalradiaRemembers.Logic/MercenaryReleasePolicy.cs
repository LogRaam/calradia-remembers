// Code written by Gabriel Mailhot, 04/08/2026.
// end_mercenary fix (player report, 2026-08-04): "The King agreed to free me from my mercenary contract.
// Nothing happens." join_as_mercenary (the enrollment) had a real action wired to it; ending the very same
// contract did not, so the promise was empty. This tiny pure gate is the one guard the bridge consults
// before dissolving the contract: never a silent no-op (this whole class of bug is what the recent
// silent-refusal work fixed), so a refused attempt is surfaced to the model as a refusal, not swallowed.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate for the <c>end_mercenary</c> action: whether the player's clan is truly under mercenary
    ///   service right now, the one fact both the prompt teaching (<c>EncounterContext.PlayerIsMercenary</c>)
    ///   and the bridge's execution guard must agree on before a "contract dissolved" outcome is real.
    /// </summary>
    public static class MercenaryReleasePolicy
    {
        /// <summary>
        ///   True only when the player's clan is presently under a mercenary contract. The bridge must refuse
        ///   (never silently ignore) when this is false, so a King who "agrees" to release a clan that was
        ///   never a mercenary in the first place gets an honest in-character refusal instead of nothing.
        /// </summary>
        public static bool CanEnd(bool isUnderMercenaryService) => isUnderMercenaryService;
    }
}
