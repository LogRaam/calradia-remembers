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
        /// <param name="observerAggrieved">The deed harmed the observer's own faction or kin.</param>
        /// <param name="observerSharesPlayerSide">The observer stands with the player's faction.</param>
        public static DeedValence Classify(bool observerAggrieved, bool observerSharesPlayerSide)
        {
            if (observerAggrieved) return DeedValence.Wound;       // a blow to me or mine is felt first
            if (observerSharesPlayerSide) return DeedValence.Boon; // our own side's champion did this
            return DeedValence.Neutral;
        }
    }
}
