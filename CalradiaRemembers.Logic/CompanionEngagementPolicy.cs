// Code written by Gabriel Mailhot, 13/07/2026.
// What counts as being EMPLOYED in the player's service. The old rule recognised only one thing, being away on
// an errand, so a companion the player had made a GOVERNOR, or given a party to lead, still read as "idle" and
// took the ennui penalty week after week (player report, SuperGraham339: "I assigned one dude a governor spot
// ... three days later he says I'm tired of this boss"). A post given IS employment, and the mark of trust that
// comes with it.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a companion is kicking their heels. Pure: the engine reads the facts (errand, post,
    ///   command) and asks here.
    /// </summary>
    public static class CompanionEngagementPolicy
    {
        /// <summary>
        ///   Every charge a companion might hold in the player's service. Three booleans in a row, ALL of them
        ///   disqualifiers, so a swap changes nothing today and everything the first time one of them stops
        ///   meaning simply "busy".
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class CompanionChargeFacts
        {
           /// <summary>Away on a companion errand.</summary>
           public bool OnErrand { get; set; }

           /// <summary>Governing a settlement for the player.</summary>
           public bool IsGovernor { get; set; }

           /// <summary>Leading a party of the player's clan.</summary>
           public bool LeadsAParty { get; set; }
        }

        /// <summary>
        ///   True only when the companion has NO occupation at all in your service: not away on an errand, not
        ///   governing a settlement for you, and not leading a party of your clan. Any one of those is a charge
        ///   they hold, and a charge is not idleness.
        /// </summary>
        public static bool IsIdle(CompanionChargeFacts facts)
        {
           if (facts == null) return false;

           bool onErrand = facts.OnErrand;
           bool isGovernor = facts.IsGovernor;
           bool leadsAParty = facts.LeadsAParty;

           return !onErrand && !isGovernor && !leadsAParty;
        }
    }
}
