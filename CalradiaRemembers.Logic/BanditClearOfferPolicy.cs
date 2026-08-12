// Code written by Gabriel Mailhot, 12/08/2026.
// Decides whether a giver's grounded quest menu may offer an open-field bandit-clearing deed near their anchor
// town. Before this, the viable-quest scanner offered bandit_clear UNCONDITIONALLY whenever an anchor town
// existed, even when no bandit party actually roamed there, so a lord could send the player after bandits that
// were not there; the player found nothing, reported so, and the giver turned hostile (player report). This pure
// rule pins the invariant "offered => completable": the menu offers the deed only when at least one bandit party
// is currently within QuestDeedTracker's own bandit-clear completion radius of the anchor. The engine-facing map
// scan (which produces the count) stays in the mod; only the count-to-decision step lives here so it stays tested.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether an open-field bandit-clearing deed is viable to offer near a giver's anchor town.</summary>
    public static class BanditClearOfferPolicy
    {
        /// <summary>
        ///   True only when at least one bandit party is currently roaming within the anchor's completion radius.
        ///   A non-positive count (none nearby, or a defensive miscount) yields false, so the giver never offers
        ///   a bandit-clearing task the world cannot satisfy.
        /// </summary>
        public static bool ShouldOffer(int nearbyBanditPartyCount) => nearbyBanditPartyCount > 0;
    }
}
