// Code written by Gabriel Mailhot, 18/07/2026.
// Courier audit (island report): one courier in flight per sender — a slow or stuck courier must never
// snowball into a whole pack of couriers riding from the same NPC, one per queued letter.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>
    ///   Pure gate for the reply-courier dispatch: whether a sender already has a courier on the road.
    ///   Kept engine-free so the rule is pinned by unit tests; the host resolves the letters from the store.
    /// </summary>
    public static class CourierDispatchPolicy
    {
        /// <summary>
        ///   True when the sender already has an undelivered, unlost letter bearing a courier party id —
        ///   that is, a courier physically on the road for them. While one is in flight, no second courier
        ///   may be dispatched for that sender (player report: couriers piling up, one per queued letter).
        /// </summary>
        public static bool HasCourierInFlight(IEnumerable<PendingLetter> sentLetters)
        {
            return sentLetters != null
                && sentLetters.Any(l => l is {IsDelivered: false, IsLost: false} && !string.IsNullOrEmpty(l.CourierPartyId));
        }
    }
}
