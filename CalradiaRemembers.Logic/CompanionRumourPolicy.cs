// Code written by Gabriel Mailhot, 22/07/2026.
// The RUMOUR REPORT found-topic: the companion brings word of the world. The WorldEventStore already scores
// which events a given observer would plausibly have heard (RumorAwareness) and rates each with a
// RumorConfidence; this turns that existing rating into the same 0..100 strength scale the other found-topics
// compare against, so a rumour competes fairly rather than always winning or always losing by construction.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for scoring a rumour candidate's strength once the engine has already resolved awareness.</summary>
    public static class CompanionRumourPolicy
    {
        // A well-known rumour is worth voicing more than a barely-caught whisper of the same magnitude, the
        // companion is more confident bringing it up at all.
        private const int WellKnownBonus = 10;
        private const int DistantPenalty = 10;

        /// <summary>
        ///   The rumour's strength (0..100): the event's own reach (<paramref name="magnitude" />, already
        ///   0..100 on the WorldEvent itself) nudged by how surely it reached this companion.
        /// </summary>
        public static int Strength(int magnitude, RumorConfidence confidence)
        {
            int adjusted = confidence switch {
                RumorConfidence.WellKnown => magnitude + WellKnownBonus,
                RumorConfidence.Distant => magnitude - DistantPenalty,
                _ => magnitude
            };

            if (adjusted < 0) return 0;

            return adjusted > 100
                ? 100
                : adjusted;
        }
    }
}
