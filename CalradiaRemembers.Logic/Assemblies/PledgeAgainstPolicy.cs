// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module: pledge_against's ONE council-specific caveat beyond the reused 1:1 gates
// (COUNCIL_ACTIONS.md's Kimi review, "trahison a table"): a seated member must not be allowed to pledge against
// a RIVAL who sits at this very council. Pure and cheap: the OTHER seats present when the pledge was recorded
// are already carried on AssemblyResolutionEntry.WitnessIds (audit M6, the public-promise witness list), so no
// new save field or roster plumbing is needed to answer this.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for pledge_against's council-only caveat: refusing a pledge against a fellow seat.</summary>
    public static class PledgeAgainstPolicy
    {
        /// <summary>
        ///   True when <paramref name="targetHeroId" /> was one of the OTHER members seated at the council when
        ///   this pledge was recorded (<paramref name="witnessIds" />, AssemblyResolutionEntry.WitnessIds): the
        ///   lift's own signal to refuse the pledge outright rather than let a member scheme against a fellow
        ///   seat at the same table. A null/empty witness list (an older save written before audit M6, or a
        ///   solitary "council" of one) never matches, so a pledge is only ever refused for a rival CONFIRMED to
        ///   have sat at the very table where it was struck, never guessed.
        /// </summary>
        public static bool IsRivalSeatedAtCouncil(string targetHeroId, IEnumerable<string> witnessIds)
        {
            if (string.IsNullOrEmpty(targetHeroId) || witnessIds == null) return false;

            return witnessIds.Any(id => string.Equals(id, targetHeroId, StringComparison.Ordinal));
        }
    }
}
