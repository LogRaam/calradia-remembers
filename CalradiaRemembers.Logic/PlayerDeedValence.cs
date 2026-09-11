// Code written by Gabriel Mailhot, 22/06/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How a deed of the PLAYER's lands on an NPC who has only HEARD of it — the emotional colour the
    ///   influence loop gives a lord who was not there. Persisted stance is moved only by real, witnessed
    ///   acts (a battle fought, a promise kept); this is reputation — prompt colouring for a deed that
    ///   merely reached the NPC by word.
    /// </summary>
    public enum DeedValence
    {
        /// <summary>A distant deed, neither for the observer nor against them.</summary>
        Neutral,

        /// <summary>It struck the observer's own faction or kin — wariness, resentment, fear.</summary>
        Wound,

        /// <summary>It served the observer's side — respect, gratitude, a measure of pride.</summary>
        Boon
    }

    /// <summary>
    ///   Classifies a player's deed from the observer's point of view, from two facts the host has
    ///   already resolved against the game (faction membership, kinship). Kept pure and unit-tested.
    ///   A blow to one's own side is felt before any shared allegiance is enjoyed, so a wound outweighs
    ///   a boon when a deed is somehow both.
    /// </summary>
    public static class PlayerDeedValence
    {
        /// <summary>
        ///   How close a deed came to the observer. Three booleans in a row, of which the first two WOUND and
        ///   the third is a BOON: crossing them turns an ally's approval into an injury and back.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ObserverStakeFacts
        {
           /// <summary>The deed harmed the observer's own faction.</summary>
           public bool FactionAggrieved { get; set; }

           /// <summary>
           ///   The deed struck their own kin directly. It cuts even when the broad allegiance does not match, as
           ///   for a kinsman married into another banner.
           /// </summary>
           public bool KinHarmed { get; set; }

           /// <summary>They stand with the player's faction.</summary>
           public bool SharesPlayerSide { get; set; }
        }

        /// <param name="observerFactionAggrieved">The deed harmed the observer's own faction.</param>
        /// <param name="observerKinHarmed">The deed struck the observer's own kin (a clansman/relative) directly.</param>
        /// <param name="observerSharesPlayerSide">The observer stands with the player's faction.</param>
        public static DeedValence Classify(ObserverStakeFacts facts)
        {
           if (facts == null) return DeedValence.Neutral;

           bool observerFactionAggrieved = facts.FactionAggrieved;
           bool observerKinHarmed = facts.KinHarmed;
           bool observerSharesPlayerSide = facts.SharesPlayerSide;

            // A blow to me or mine is felt first — and a strike at one's own kin cuts even when the broad
            // faction allegiance does not match (a kinsman married into another banner, say).
            if (observerFactionAggrieved || observerKinHarmed) return DeedValence.Wound;
            if (observerSharesPlayerSide) return DeedValence.Boon; // our own side's champion did this
            return DeedValence.Neutral;
        }
    }
}
