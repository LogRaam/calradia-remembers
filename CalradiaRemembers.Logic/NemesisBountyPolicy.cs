// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 3 (bounty missions, the world hook): pure eligibility gate for a lord
// offering a bounty on a tracked nemesis. Primary rule (ratified): the ISSUING lord's own region
// (home settlement, else where they currently stand, else their culture as a last resort) must
// match the nemesis's RegionId (see NemesisIdentity.RegionId, itself a settlement-or-culture key).
// The optional "past victim" rule (a lord this nemesis has personally wronged may also offer,
// regardless of region) is NOT built here: CR has no persisted record of WHICH lords a nemesis has
// preyed upon, only the region he is anchored to, so that extension is flagged for a later
// increment rather than guessed at. Every input is a plain primitive (bools/strings) so the engine
// glue (resolving a Hero's settlement/culture ids) stays out of this pure class entirely.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a given lord may offer the player a bounty on a given tracked nemesis. Pure:
    ///   every fact (region keys, aliveness, whether a bounty is already active) is supplied by the
    ///   caller, resolved from the live game world.
    /// </summary>
    public static class NemesisBountyPolicy
    {
        /// <summary>
        ///   The three keys a lord can be placed by, and the one key the nemesis is anchored to. FOUR faction
        ///   and settlement ids in a row, and the first three are tried in PRIORITY order, which the naming
        ///   now carries: home first (the strongest signal his lands were troubled by this band), then where
        ///   the lord currently stands, then culture.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class RegionMatchFacts
        {
           /// <summary>The lord's home settlement. The strongest signal.</summary>
           public string LordHomeSettlementId { get; set; }

           /// <summary>
           ///   Where the lord stands right now. Covers a landless wanderer-lord, or one away from their fief.
           /// </summary>
           public string LordCurrentSettlementId { get; set; }

           /// <summary>
           ///   The lord's culture. The fallback the nemesis's own RegionId itself uses when his band had no home
           ///   settlement to anchor to.
           /// </summary>
           public string LordCultureId { get; set; }

           /// <summary>The nemesis's anchor. Blank means unanchored, and nothing matches an unanchored foe.</summary>
           public string NemesisRegionId { get; set; }
        }

        /// <summary>
        ///   True when the lord's own region key matches the nemesis's <see cref="NemesisIdentity.RegionId" />.
        ///   Checked in priority order: home settlement first (the strongest signal a lord's lands were
        ///   troubled by this band), then wherever the lord currently stands (covers a landless wanderer-lord
        ///   or one away from their fief), then culture (the fallback the nemesis's own RegionId itself uses
        ///   when his band had no home settlement to anchor to). Any missing key is simply skipped, never
        ///   treated as a match.
        /// </summary>
        public static bool RegionMatches(RegionMatchFacts facts)
        {
           if (facts == null) return false;

           string lordHomeSettlementId = facts.LordHomeSettlementId;
           string lordCurrentSettlementId = facts.LordCurrentSettlementId;
           string lordCultureId = facts.LordCultureId;
           string nemesisRegionId = facts.NemesisRegionId;

            if (string.IsNullOrEmpty(nemesisRegionId)) return false;

            if (!string.IsNullOrEmpty(lordHomeSettlementId) && lordHomeSettlementId == nemesisRegionId) return true;
            if (!string.IsNullOrEmpty(lordCurrentSettlementId) && lordCurrentSettlementId == nemesisRegionId) return true;
            if (!string.IsNullOrEmpty(lordCultureId) && lordCultureId == nemesisRegionId) return true;

            return false;
        }

        /// <summary>
        ///   What decides whether THIS lord would put a price on THIS nemesis's head. Four booleans in a row,
        ///   two of them requirements and two of them disqualifiers, and one of the disqualifiers is
        ///   per-giver rather than world-wide, which is exactly the distinction an order-based call cannot
        ///   make visible.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class BountyIssuerFacts
        {
           /// <summary>The nemesis is alive.</summary>
           public bool NemesisAlive { get; set; }

           /// <summary>
           ///   He was spared into the player's service. A turned nemesis can never be bountied: he is no longer
           ///   a foe.
           /// </summary>
           public bool NemesisTurned { get; set; }

           /// <summary>
           ///   THIS lord already holds a bounty on him. Per-giver, not store-wide, so several lords may each
           ///   put a price on the same head and the player collects from each.
           /// </summary>
           public bool GiverAlreadyBountiedHim { get; set; }

           /// <summary>The issuing lord's region is the nemesis's own.</summary>
           public bool RegionMatches { get; set; }
        }

        /// <summary>
        ///   The full offer gate: the nemesis must be a LIVING, NOT-TURNED, already-manifested foe (the engine
        ///   only calls this once a Hero is bound to the identity), THIS lord must not already hold a bounty on
        ///   him, and the issuing lord's region must match his. All are required.
        ///   Ratified 2026-07-24: <c>GiverAlreadyBountiedHim</c> is now PER-GIVER, not store-wide,
        ///   so several lords may each put a price on the same head (the player collects from each). A TURNED
        ///   nemesis (spared into the player's service) can never be bountied: he is no longer a foe (audit M3).
        /// </summary>
        public static bool IsEligibleIssuer(BountyIssuerFacts facts)
        {
           if (facts == null) return false;

           bool nemesisAlive = facts.NemesisAlive;
           bool nemesisTurned = facts.NemesisTurned;
           bool giverAlreadyBountiedHim = facts.GiverAlreadyBountiedHim;
           bool regionMatches = facts.RegionMatches;

           return nemesisAlive && !nemesisTurned && !giverAlreadyBountiedHim && regionMatches;
        }
    }
}
