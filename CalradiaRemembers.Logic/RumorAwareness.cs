// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The pure awareness model: how far news of an event carries, and how surely it has reached a
    ///   given observer. Recency is a 1→0 MULTIPLIER on reach (magnitude + faction + proximity), so a
    ///   faint rumour is NOT known everywhere merely for being fresh — it still needs real reach. The
    ///   engine-coupled store feeds plain numbers (faction match, a precomputed geo bonus, age in days)
    ///   so this stays testable.
    /// </summary>
    public static class RumorAwareness
    {
        /// <summary>Score at/above which the observer "knows" the event.</summary>
        public const int Threshold = 50;

        /// <summary>Bonus to reach when the event touches the observer's own faction.</summary>
        public const int FactionBonus = 25;

        /// <summary>Days over which freshness decays from full to nothing.</summary>
        public const int RecencyWindowDays = 40;

        /// <summary>
        ///   Proximity bonus when the event happened on the observer's doorstep. The host scales it down with
        ///   map distance; it lives here so a reach estimate can be made without the map.
        /// </summary>
        public const int GeoBonusMax = 25;

        /// <summary>
        ///   M-G6 (regard audit): the SUBJECT of an event (named in its actors) is far likelier to catch wind of it
        ///   than a distant bystander. Without it, the target of a sway scored no higher than a stranger, so a
        ///   cross-faction target never crossed the threshold. A flat lift, added AFTER recency, so it still
        ///   cannot carry news that has faded to nothing.
        /// </summary>
        public const int ConcernedPartyBonus = 25;

        // Score bands for confidence (distortion with distance).
        private const int WellKnownAt  = 90;
        private const int SecondhandAt = 62;

        /// <summary>
        ///   reach × recency. <paramref name="geoBonus"/> is the already-computed proximity bonus
        ///   (0..max) the host derives from map distance; <paramref name="factionMatches"/> is whether
        ///   the event touches the observer's faction; <paramref name="ageDays"/> is today − the event day.
        /// </summary>
        public static int Score(int magnitude, bool factionMatches, int geoBonus, int ageDays)
        {
            float recency = Math.Max(0f, 1f - Math.Max(0, ageDays) / (float) RecencyWindowDays);
            int reach = magnitude + (factionMatches ? FactionBonus : 0) + Math.Max(0, geoBonus);
            return (int) (reach * recency);
        }

        /// <summary>
        ///   The full awareness verdict for one observer and one event, with every term that went into it. The
        ///   host's awareness queries score through this, so the explanation a diagnostic prints is the very
        ///   arithmetic that gates the prompt, never a re-derivation of it.
        /// </summary>
        public static RumorAwarenessBreakdown Explain(int magnitude, bool factionMatches, int geoBonus, bool concernedParty, int ageDays)
        {
            int score = Score(magnitude, factionMatches, geoBonus, ageDays) + (concernedParty ? ConcernedPartyBonus : 0);

            return new RumorAwarenessBreakdown {
                Magnitude = magnitude,
                FactionMatches = factionMatches,
                GeoBonus = Math.Max(0, geoBonus),
                ConcernedParty = concernedParty,
                AgeDays = Math.Max(0, ageDays),
                Score = score,
                Knows = Knows(score),
                Confidence = Rate(score),
                LastAgeKnown = LastAgeKnown(magnitude, factionMatches, geoBonus, concernedParty)
            };
        }

        /// <summary>
        ///   The oldest age, in days, at which an observer with these terms still knows the event, or -1 when
        ///   they could not know it even on the day it happened. Recency only ever falls, so this is the day
        ///   the news fades for them.
        /// </summary>
        public static int LastAgeKnown(int magnitude, bool factionMatches, int geoBonus, bool concernedParty)
        {
            int last = -1;

            for (int age = 0; age <= RecencyWindowDays; age++)
                if (Knows(Score(magnitude, factionMatches, geoBonus, age) + (concernedParty ? ConcernedPartyBonus : 0)))
                    last = age;

            return last;
        }

        /// <summary>True when a score means the observer would, by now, have heard of the event.</summary>
        public static bool Knows(int score) => score >= Threshold;

        /// <summary>Maps an awareness score to how surely the news is held (for hedged phrasing).</summary>
        public static RumorConfidence Rate(int score)
        {
            if (score >= WellKnownAt) return RumorConfidence.WellKnown;
            if (score >= SecondhandAt) return RumorConfidence.Secondhand;
            return RumorConfidence.Distant;
        }
    }
}
