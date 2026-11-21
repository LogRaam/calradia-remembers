// Code written by Gabriel Mailhot, 20/11/2026.
// Bhelogan (Discord, 20/11/2026), for pictures painted by his mod in the Memories panel: "A stable memory id. CrMemoryRecord
// has day/kind/summary but nothing to point at", and "a 'new memory added' event carrying the same fields would let us
// prepare the picture while the scene is still fresh", and "The session id of the conversation the memory came from".
// Gabriel (20/11/2026): "oui on y va".
//
// CR writes a memory from more than a hundred places, so none of them is asked to announce it. This watch looks at the
// characters' memories at a few moments (each hour, as a conversation opens and as it closes, after a mod's AddMemory)
// and reports what it has not seen yet: a new memory, or a memory whose text was rewritten (the summary that enriches a
// conversation's "Spoke with" anchor). It gives each one an id, and stamps the conversation it was written in when it
// looks as a conversation closes. A memory is told apart by its OBJECT, not its value: two "Spoke with X." on one day are
// two memories, and a memory rewritten is a new object (records are copied with `with`). If this is wrong, a mod paints
// the same memory twice, misses one, or links a memory to a conversation it was not born in.

#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>One memory the watch reports: written since it last looked.</summary>
    public sealed class WatchedMemory
    {
        /// <summary>The character who holds it (hero StringId).</summary>
        public string NpcId { get; init; }

        /// <summary>The memory, already carrying its id (and its conversation, when stamped).</summary>
        public NotableEvent Memory { get; init; }

        /// <summary>True for a memory never seen before; false for one whose text was rewritten.</summary>
        public bool IsNew { get; init; }
    }

    /// <summary>One character's memories as the watch reads them.</summary>
    public sealed class WatchedProfile
    {
        /// <summary>The character (hero StringId).</summary>
        public string NpcId { get; init; }

        /// <summary>A copy of the character's memory list taken now (the list itself may be swapped by another thread).</summary>
        public IReadOnlyList<NotableEvent> Memories { get; init; }
    }

    /// <summary>Notices memories written since the last look. One per loaded campaign; the game thread only.</summary>
    public sealed class MemoryWriteWatch
    {
        private HashSet<NotableEvent> _seen = new(Same.Instance);
        private readonly Dictionary<string, string> _textById = new(StringComparer.Ordinal);

        /// <summary>True once the watch has taken stock of what was already there (that first look reports nothing).</summary>
        public bool Seeded { get; private set; }

        /// <summary>Forgets everything: another campaign was loaded.</summary>
        public void Reset()
        {
            _seen = new HashSet<NotableEvent>(Same.Instance);
            _textById.Clear();
            Seeded = false;
        }

        /// <summary>
        ///   The memory's id, given now when it has none. A memory a mod can point at needs one; an id given here is known to
        ///   the watch, so its later rewrite reads as a rewrite, not as a new memory.
        /// </summary>
        public string EnsureId(NotableEvent memory)
        {
            if (memory == null) return null;
            if (string.IsNullOrEmpty(memory.Id)) memory.Id = NewId();
            if (!_textById.ContainsKey(memory.Id)) _textById[memory.Id] = memory.summary ?? "";

            return memory.Id;
        }

        /// <summary>
        ///   Looks at these characters' memories and returns what was written since the last look. The first look only takes
        ///   stock. <paramref name="sessionId" />, when given, is the conversation closing now: a memory first seen at that
        ///   look was written in it. <paramref name="everyone" /> says these are all the characters, so what is no longer
        ///   anywhere (compressed away, taken back) can be forgotten.
        /// </summary>
        public IReadOnlyList<WatchedMemory> Look(IEnumerable<WatchedProfile> profiles, string sessionId, bool everyone)
        {
            var written = new List<WatchedMemory>();
            var stillThere = everyone ? new HashSet<NotableEvent>(Same.Instance) : null;
            bool firstLook = !Seeded;

            foreach (WatchedProfile profile in profiles ?? Array.Empty<WatchedProfile>())
            {
                if (profile?.Memories == null) continue;

                foreach (NotableEvent memory in profile.Memories)
                {
                    if (memory == null) continue;
                    stillThere?.Add(memory);
                    if (!_seen.Add(memory)) continue;

                    if (firstLook)
                    {
                        if (!string.IsNullOrEmpty(memory.Id)) _textById[memory.Id] = memory.summary ?? "";

                        continue;
                    }

                    bool known = !string.IsNullOrEmpty(memory.Id) && _textById.ContainsKey(memory.Id);
                    if (string.IsNullOrEmpty(memory.Id)) memory.Id = NewId();
                    if (!string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(memory.SessionId) && !known) memory.SessionId = sessionId;

                    string text = memory.summary ?? "";
                    bool rewritten = known && !string.Equals(_textById[memory.Id], text, StringComparison.Ordinal);
                    _textById[memory.Id] = text;
                    if (!known || rewritten) written.Add(new WatchedMemory {NpcId = profile.NpcId, Memory = memory, IsNew = !known});
                }
            }

            if (stillThere != null) _seen = stillThere;
            Seeded = true;

            return written;
        }

        private static string NewId() => Guid.NewGuid().ToString("N");

        private sealed class Same : IEqualityComparer<NotableEvent>
        {
            public static readonly Same Instance = new();
            public bool Equals(NotableEvent a, NotableEvent b) => ReferenceEquals(a, b);
            public int GetHashCode(NotableEvent item) => RuntimeHelpers.GetHashCode(item);
        }
    }
}
