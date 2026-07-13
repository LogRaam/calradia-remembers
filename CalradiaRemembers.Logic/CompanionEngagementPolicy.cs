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
        ///   True only when the companion has NO occupation at all in your service: not away on an errand, not
        ///   governing a settlement for you, and not leading a party of your clan. Any one of those is a charge
        ///   they hold, and a charge is not idleness.
        /// </summary>
        public static bool IsIdle(bool onErrand, bool isGovernor, bool leadsAParty)
            => !onErrand && !isGovernor && !leadsAParty;
    }
}
