// Code written by Gabriel Mailhot, 22/07/2026.
// The CALLBACK found-topic: an old and strong shared memory resurfaces unprompted. Flagged as the highest-value
// topic of the five (ROADMAP request) because the mod already stores every NotableEvent but nothing ever pays
// one off, this is that payoff. "Significant" mirrors LlmMemoryCompressor's own protected-from-compression
// event types (its own header: never drop a Betrayal/Intimacy/Confrontation/Agreement/Captivity/Jealousy), the
// one existing place in this codebase that already judges an event durable enough to keep. Collaboration is
// added here (not protected there): a shared feat the player and companion pulled off together is exactly the
// kind of thing worth calling back to, even though it is common enough that the compressor may still thin it
// out. Flirt and FirstMeeting are deliberately excluded: the former the compressor itself does not protect,
// and a first meeting is an introduction, not a confidence to resurface.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>Pure rules for whether/how strongly a stored NotableEvent qualifies as an unprompted callback.</summary>
    public static class CompanionCallbackPolicy
    {
        // A callback that resurfaces something from LAST WEEK reads as the mod nagging about yesterday, not as
        // an old memory surfacing unbidden. Below this, an event simply is not "old" yet.
        public const int MinDaysAgo = 20;

        // Age keeps adding weight up to this point, then plateaus: an event from 3 years ago is not
        // meaningfully "more callback-worthy" than one from 200 days ago, and letting age grow unbounded would
        // let a single ancient event dominate strength comparisons forever.
        private const int MaxAgeBonusDays = 200;
        private const int BaseWeight = 30;
        private const int SignificantTypeBonus = 40;
        private const int AgeBonusMax = 30;

        /// <summary>
        ///   True for the event types durable enough to headline an unprompted callback, the same set
        ///   <see cref="NpcMemoryService.Core.Compression.LlmMemoryCompressor" /> refuses to ever drop, plus
        ///   Collaboration (a shared feat, not merely surviving compression).
        /// </summary>
        public static bool IsSignificant(NotableEventType type)
            => type == NotableEventType.Betrayal
               || type == NotableEventType.Intimacy
               || type == NotableEventType.Confrontation
               || type == NotableEventType.Agreement
               || type == NotableEventType.Captivity
               || type == NotableEventType.Jealousy
               || type == NotableEventType.Collaboration;

        /// <summary>
        ///   The callback's strength (0..100; 0 means it does not qualify at all): a significant event old
        ///   enough to resurface earns a floor of <see cref="BaseWeight" /> + <see cref="SignificantTypeBonus" />,
        ///   climbing toward the cap as it ages past <see cref="MinDaysAgo" /> up to <see cref="MaxAgeBonusDays" />:
        ///   "prefer old and significant over recent" (the design rule), never a fresh but insignificant event.
        /// </summary>
        public static int Strength(NotableEventType type, int daysAgo)
        {
            if (daysAgo < MinDaysAgo || !IsSignificant(type)) return 0;

            int cappedAge = daysAgo > MaxAgeBonusDays
                ? MaxAgeBonusDays
                : daysAgo;
            int ageBonus = cappedAge * AgeBonusMax / MaxAgeBonusDays;

            return BaseWeight + SignificantTypeBonus + ageBonus;
        }

        /// <summary>True when this event is old and significant enough to ever be voiced as a callback.</summary>
        public static bool Qualifies(NotableEventType type, int daysAgo) => Strength(type, daysAgo) > 0;
    }
}
