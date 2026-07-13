// Code written by Gabriel Mailhot, 12/07/2026.
// Pure: turns a presence tier into the NPC's "awareness of being" for the prompt, and decides whether the
// elapsed-time note ("it has been a season since we last met") should surface at all. The point of the tier
// is that co-travel and a chance co-location are NOT the same: a companion riding at your side never feels
// ignored (player report: crew acting as if abandoned for decades between two ports), while a lord you meet
// in a shared hall may still remark on the season apart. Kept here, behind tests, so the wording and the two
// thresholds are documented and easy to tune.

namespace CalradiaRemembers.Logic.Social
{
    /// <summary>
    ///   Renders the spatial "you are with the player like THIS" line, and gates the separate elapsed-gap note
    ///   (handled by <see cref="MeetingGapDescriber" />) so it never fires for someone who travels with the
    ///   player daily.
    /// </summary>
    public static class PresenceDescriber
    {
        /// <summary>
        ///   The present-tense fact of how the NPC is at the player's side right now, or null when they are
        ///   <see cref="PresenceTier.Away" /> (nothing spatial to say; any note is the elapsed-gap note's job).
        ///   The host renders this verbatim, without the "do not dwell on it" restraint the gap note carries,
        ///   because sharing a road is a bond to lean into, not a lapse to excuse.
        /// </summary>
        /// <param name="placeName">The shared settlement's name for <see cref="PresenceTier.SamePlace" />; may be null.</param>
        /// <param name="atSea">
        ///   Whether the player is currently sailing. The co-travel tiers phrase the bond in sea terms (deck,
        ///   voyage, fleet) rather than land ones (road, march, camp) so a companion aboard ship is not made to
        ///   speak of marching. A shared settlement is always ashore, so <see cref="PresenceTier.SamePlace" />
        ///   ignores it.
        /// </param>
        public static string Describe(PresenceTier tier, string placeName, bool atSea)
        {
            switch (tier)
            {
                case PresenceTier.InPlayerParty:
                    return atSea
                        ? "You sail at the player's side, sharing deck and voyage day after day."
                        : "You travel at the player's side, sharing camp and road day after day.";

                case PresenceTier.InPlayerArmy:
                    return atSea
                        ? "You sail in the player's fleet, and see them about the deck."
                        : "You march in the player's army, and see them about the camp.";

                case PresenceTier.SamePlace:
                    return string.IsNullOrWhiteSpace(placeName)
                        ? "You and the player are in the same place just now."
                        : $"You and the player are both in {placeName} just now.";

                default:
                    return null;
            }
        }

        /// <summary>
        ///   Whether the elapsed-time note ("since you last saw the player") is allowed for this tier. Suppressed
        ///   for the co-travel tiers (you see the player constantly, so the count is meaningless and made crew
        ///   sound abandoned), allowed for <see cref="PresenceTier.SamePlace" /> (a reunion, though you share a
        ///   roof now) and <see cref="PresenceTier.Away" />.
        /// </summary>
        public static bool ShowsElapsedGap(PresenceTier tier)
            => tier == PresenceTier.SamePlace || tier == PresenceTier.Away;
    }
}
