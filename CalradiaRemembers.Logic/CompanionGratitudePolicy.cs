// Code written by Gabriel Mailhot, 20/08/2026.
// The GRATITUDE found-topic: a content companion seeks the player out, of their own accord, to thank them for a
// specific kindness (an Appreciation the player planted on them, see CalradiaRemembers.Logic.Appreciations) and
// reaffirm their loyalty. Purely warm, never a request or a bargain (design brief). This decides which live
// appreciation (if any) is significant, recent, and content enough to earn a whole private audience over: the
// magnitude floor is deliberately well above AppreciationPolicy.MagnitudeFloor (the mere FORMATION floor), so
// only a genuinely meaningful kindness pulls the player aside, not every gift that barely registered; the
// recency window keeps the audience reacting to something that still feels present in the story, rather than
// resurfacing a kindness from many months ago that has since been overtaken by ordinary play (that staleness is
// Callback's own territory, which explicitly wants OLD memories); and the happiness gate exists because a
// thank-you from an unhappy companion reads as false, CompanionHappiness.Band's own Content threshold is reused
// so this never disagrees with what the encyclopedia/prompt already tell the player about this companion's mood.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for whether/how strongly a live Appreciation qualifies as an unprompted gratitude audience.</summary>
    public static class CompanionGratitudePolicy
    {
        // Well above AppreciationPolicy.MagnitudeFloor (15, the bare formation floor): a kindness that only just
        // cleared formation is routine warmth, not something worth a whole private audience. This is deliberately
        // a floor on TODAY'S effective (post-decay) magnitude, not the stored one, so a once-strong kindness that
        // has already faded mostly away no longer qualifies either.
        public const int MinEffectiveMagnitude = 40;

        // A kindness thanked for many months late reads as delayed, not heartfelt. Keeps Gratitude reacting to
        // something that still feels recent in the unfolding story, distinct from Callback, which deliberately
        // wants OLD, long-settled memories (CompanionCallbackPolicy.MinDaysAgo is a FLOOR; this is a CEILING).
        public const int RecencyWindowDays = 60;

        /// <summary>
        ///   True when a live appreciation is significant enough, recent enough, and the companion is CONTENT
        ///   enough in the player's service to earn a gratitude audience. Spend-once bookkeeping (has this
        ///   appreciation already been thanked for) is the caller's concern, exactly like Callback/RumourReport.
        /// </summary>
        public static bool Holds(int effectiveMagnitude, int daysAgo, int happinessScore)
        {
            if (!IsContentEnough(happinessScore)) return false;
            if (daysAgo < 0 || daysAgo > RecencyWindowDays) return false;

            return effectiveMagnitude >= MinEffectiveMagnitude;
        }

        /// <summary>
        ///   Reuses CompanionHappiness's own Content/Devoted bands (the same wording the encyclopedia and the
        ///   prompt already show the player), so a thank-you can never be voiced by a companion the player would
        ///   currently read as restless or disgruntled.
        /// </summary>
        public static bool IsContentEnough(int happinessScore)
            => CompanionHappiness.Band(happinessScore) is HappinessBand.Content or HappinessBand.Devoted;

        /// <summary>
        ///   The gratitude topic's strength (0..100): the appreciation's own effective magnitude, unchanged, so a
        ///   bigger kindness always outranks a smaller one when several found-topics compete for the audience.
        /// </summary>
        public static int Strength(int effectiveMagnitude)
        {
            if (effectiveMagnitude < 0) return 0;

            return effectiveMagnitude > 100 ? 100 : effectiveMagnitude;
        }
    }
}
