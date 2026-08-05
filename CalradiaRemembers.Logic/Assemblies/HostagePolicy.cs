// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "give_hostage", v1 "invite
// d'honneur"): the Parley's fourth concession. Unlike tribute (a daily payment, negotiable in amount) and
// release_prisoner (a named captive, no duration at all), a hostage's own free variable is TIME: how long they
// reside at the player's own court before returning home.
//
// Author's design call (adapting COUNCIL_ACTIONS.md's own recipe): a FIXED 45-day term, the midpoint of the
// doc's own 20-90 day range, mirroring TributePolicy.TributeDays' and StipendPolicy.TrancheDays' own deliberate
// simplification versus a negotiated range - the LLM proposes WHO (the relative's name), never a second duration
// parameter to get wrong or clamp. A future build could expose a negotiated term (20-90 days) the same way
// tribute's own amount is negotiated; this build keeps it fixed so the term is never a second field to ground
// through TargetHint alongside the hostage's name.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for give_hostage: the fixed residency term.</summary>
    public static class HostagePolicy
    {
        /// <summary>
        ///   A hostage always resides at the player's own court for a FIXED term of 45 days (COUNCIL_ACTIONS.md's
        ///   own 20-90 day range, midpoint chosen): the only variable the LLM proposes is WHICH eligible relative
        ///   is given, never a second duration figure.
        /// </summary>
        public const int TermDays = 45;
    }
}
