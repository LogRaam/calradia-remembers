// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod's ConversationEventFeeder: what counts as a public affront worth gossiping
// about, and how far the disgrace travels. Pure so the threshold and the magnitude curve — too low
// and every spat becomes gossip, too high and no disgrace ever spreads — stay protected by tests.

using System;

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The rule behind "insults travel": when a conversation's relation drop, delivered before
    ///   witnesses, becomes a circulating rumour, and how widely it carries. The host scans the
    ///   actions for the steepest drop and counts the witnesses; this holds the tuning.
    /// </summary>
    public static class PublicAffrontPolicy
    {
        /// <summary>A relation drop at least this steep counts as a grave, gossip-worthy affront.</summary>
        public const int AffrontThreshold = -5;

        private const int BaseMagnitude = 25;
        private const int MagnitudePerWitness = 5;
        private const int MaxMagnitude = 50;

        /// <summary>
        ///   True when a relation drop is a public affront: at least one witness saw it, and the
        ///   steepest drop this exchange is as harsh as <see cref="AffrontThreshold" />. Routine
        ///   cooling (−1), or any slight with no one watching, never spreads.
        /// </summary>
        public static bool IsAffront(int worstRelationDelta, int witnessCount)
            => witnessCount > 0 && worstRelationDelta <= AffrontThreshold;

        /// <summary>
        ///   How far the disgrace carries: a base that grows with the size of the audience, capped —
        ///   a slight before a crowd is common knowledge, but no single scene becomes legend.
        /// </summary>
        public static int Magnitude(int witnessCount)
            => Math.Min(MaxMagnitude, BaseMagnitude + witnessCount * MagnitudePerWitness);
    }
}
