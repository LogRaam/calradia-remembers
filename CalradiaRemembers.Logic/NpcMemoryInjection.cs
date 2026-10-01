// Code written by Gabriel Mailhot, 24/07/2026.
// Extension Surface: the normalisation rules for a memory a third-party mod injects into an NPC through the
// public CrNpcMemory facade. Pure so the contract stays protected by tests; the hero resolution, get-or-create
// and store write are engine-side in the facade. A caller hands raw strings (a summary, an optional kind, how
// many days ago); this bounds and normalises them into a NotableEvent the store can hold and AppendHistory
// can later surface as the NPC's own recollection.

#region

using System;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The door rules an externally-injected NPC memory passes through before it enters the store.</summary>
    public static class NpcMemoryInjection
    {
        /// <summary>
        ///   Maps a caller's kind string to a <see cref="NotableEventType" />, defaulting to
        ///   <see cref="NotableEventType.Other" />. Three types are ENGINE-OWNED and can never be spoofed by a
        ///   caller: FirstMeeting (the mod dedups it and drives the "have we met" logic), Captivity (protected
        ///   from memory compression and tied to real scenes), and Farewell (locks the chat input). A caller
        ///   asking for any of those, or for an unknown value, gets Other instead, so an injected memory can
        ///   never impersonate one of the mod's own load-bearing event kinds.
        /// </summary>
        public static NotableEventType NormalizeKind(string kind)
            => Enum.TryParse(kind, true, out NotableEventType parsed) && IsCallerSafe(parsed)
                ? parsed
                : NotableEventType.Other;

        /// <summary>
        ///   Dates the memory: <paramref name="daysAgo" /> before the current day, floored at day 0 (a memory
        ///   cannot predate the world) and never in the future (a negative value is treated as "today").
        /// </summary>
        public static int ResolveDay(int daysAgo, int currentDay)
        {
            int day = currentDay - (daysAgo < 0 ? 0 : daysAgo);

            return day < 0 ? 0 : day;
        }

        /// <summary>
        ///   Builds the event to store, or returns null when the summary is empty (a memory with no line says
        ///   nothing, so it is rejected rather than stored blank). The summary is trimmed and bounded to the
        ///   same blob-safe length as a fed world event.
        /// </summary>
        public static NotableEvent BuildInjectedEvent(string summary, string kind, int daysAgo, int currentDay, bool isPrivate = false)
        {
            string bound = ExternalEventRules.BoundSummary(summary);

            if (bound == null) return null;

            // A memory the injecting mod marks private keeps CR's audience rule: spoken of only in private (tashmetu,
            // 01/10/2026). Unmarked stays public, as every injected memory was before.
            return new NotableEvent(ResolveDay(daysAgo, currentDay), NormalizeKind(kind), bound) {IsPrivate = isPrivate};
        }

        private static bool IsCallerSafe(NotableEventType type)
            => type != NotableEventType.FirstMeeting
            && type != NotableEventType.Captivity
            && type != NotableEventType.Farewell;
    }
}
