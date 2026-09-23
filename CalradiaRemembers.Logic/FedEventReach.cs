// Code written by Gabriel Mailhot, 23/09/2026.
// cr.feed_test fed magnitude 30 with no tag and promised it would "reach fewer people". It reached nobody, ever:
// 30 is under the awareness threshold at full freshness. A console tool that feeds news must say how far the
// news will actually carry, from the same arithmetic that decides it.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How far and how long a fed event carries, in the three observer situations the tags create: a
    ///   stranger far away, a member of a tagged realm, and someone at the event's place.
    /// </summary>
    public static class FedEventReach
    {
        /// <summary>
        ///   A short, honest line for a console: for each situation, how many days the news is heard, or that it
        ///   is not heard at all. <paramref name="factionTagged" /> and <paramref name="placed" /> say whether the
        ///   event carries those tags; an untagged situation is not described.
        /// </summary>
        public static string Describe(int magnitude, bool factionTagged, bool placed)
        {
            string text = "Reach: " + For("anyone, anywhere", RumorAwareness.LastAgeKnown(magnitude, false, 0, false));

            if (factionTagged)
                text += "; " + For("people of the tagged realm(s)", RumorAwareness.LastAgeKnown(magnitude, true, 0, false));

            if (placed)
                text += "; " + For("anyone at the place", RumorAwareness.LastAgeKnown(magnitude, false, RumorAwareness.GeoBonusMax, false));

            return text + ". Named heroes gain a further +" + RumorAwareness.ConcernedPartyBonus + ".";
        }

        private static string For(string who, int lastAge)
            => lastAge < 0
                ? $"{who} never hear it"
                : lastAge == 0
                    ? $"{who} only on the day itself"
                    : $"{who} for {lastAge} day(s)";
    }
}
