// Code written by Gabriel Mailhot, 09/08/2026.
// Open relationship (Inc 2): a partner who holds RECIPROCAL open terms with the player (an open couple) is
// not jealous of the player's other loves. This one pure decision is shared by BOTH jealousy paths so they
// cannot drift: the daily competition loop (which excludes an open partner from the aggrieved selection) and
// the event-triggered reaction (which never fires for an open partner). Kept trivial and pure on purpose: the
// WIRING (reading the flag off the profile, honoring it in each path) lives in the mod, but the RULE itself
// lives here, named and tested, so "is her jealousy suppressed?" has exactly one answer everywhere.

namespace CalradiaRemembers.Logic.Romance
{
    /// <summary>Pure gate: does a reciprocal open-relationship arrangement suppress this partner's jealousy?</summary>
    public static class OpenRelationshipPolicy
    {
        /// <summary>
        ///   True when the partner's jealousy is suppressed because she and the player hold reciprocal open
        ///   terms. <paramref name="isOpenArrangement" /> is <c>RomanticProfile.IsOpenArrangement</c> for the
        ///   partner in question. Both jealousy paths (competition selection and event-triggered reaction)
        ///   consult this single rule, so an open partner is treated identically wherever jealousy could fire.
        /// </summary>
        public static bool SuppressesJealousy(bool isOpenArrangement) => isOpenArrangement;
    }
}
