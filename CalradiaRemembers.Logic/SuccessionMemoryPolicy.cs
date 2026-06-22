// Code written by Gabriel Mailhot, 22/06/2026.
// Dynastic succession: when the player dies and an heir takes over, an NPC inherits the standing, debts,
// and grudges of their dealings with the predecessor — but NOT the predecessor's private history. Pure so
// the rule for what crosses the generation stays tested: a leak would have a lord recall the late ruler's
// love affairs to their child, which is exactly the immersion break this guards against.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The rule for which recorded events survive a succession (are inherited by the heir).</summary>
    public static class SuccessionMemoryPolicy
    {
        /// <summary>
        ///   True when an event is inherited by the heir: world, political, and relational dealings
        ///   (meetings, conflicts, collaborations, agreements, betrayals) become the heir's reputation,
        ///   debts, and grudges. False for the predecessor's PRIVATE history — flirtations, intimacy,
        ///   captivity, jealousy — which dies with them and is dropped.
        /// </summary>
        public static bool SurvivesSuccession(NotableEventType type) => type switch
        {
            NotableEventType.Flirt
                or NotableEventType.Intimacy
                or NotableEventType.Captivity
                or NotableEventType.Jealousy => false,
            _ => true
        };
    }
}
