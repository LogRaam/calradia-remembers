// Code written by Gabriel Mailhot, 10/07/2026.
// Round-table group conversation: once the floor is open (see RoundTableIntentDetector), the mod hands out a
// capped burst of NPC turns, round-robin, then returns control to the player. This is the pure arithmetic:
// which participant indices speak this burst, and where the next burst should start so that over repeated
// triggers everyone eventually gets a turn. Spawning the turns and gathering participants is engine glue (mod-side).

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure turn-order and cap arithmetic for a round-table burst.</summary>
    public static class RoundTablePolicy
    {
        /// <summary>TUNE: max NPC turns per round-table trigger before control returns to the player.</summary>
        public const int DefaultCap = 3;

        /// <summary>
        ///   TUNE: the most seated members who speak per player turn at a per-member council. Council audit M12:
        ///   the council used to give the FULL roster a turn every message (one complete LLM call each), so a
        ///   rich clan's council of a dozen lords cost a dozen prompt calls per line. This bounds it; the burst's
        ///   round-robin start advances between turns (<see cref="NextStartIndex" />), so over successive turns
        ///   everyone still gets heard. Set high enough that typical councils speak in full and only large ones
        ///   rotate.
        /// </summary>
        public const int MaxCouncilLapSpeakers = 6;

        /// <summary>The number of members to give a turn at a per-member council of <paramref name="rosterCount" /> seats: the whole table when small, capped at <see cref="MaxCouncilLapSpeakers" /> when large.</summary>
        public static int CouncilLapCap(int rosterCount)
            => rosterCount < MaxCouncilLapSpeakers ? rosterCount : MaxCouncilLapSpeakers;

        /// <summary>
        ///   The room, where the last burst stopped, and how many may speak. THREE INTS in a row that are a
        ///   count, an index and a cap: every arrangement of them produces a valid-looking modulo, so a swap
        ///   gives the wrong people the floor and no exception is ever thrown. Shared with
        ///   <see cref="NextStartIndex" />, which advances the same three.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class RoundTableFacts
        {
           /// <summary>Everyone present: the main NPC plus the witnesses.</summary>
           public int ParticipantCount { get; set; }

           /// <summary>Where the last burst left off, so over repeated triggers everyone gets a turn.</summary>
           public int StartIndex { get; set; }

           /// <summary>
           ///   How many may speak this burst. Fewer participants than the cap wraps naturally, which is what
           ///   produces back-and-forth cross-talk.
           /// </summary>
           public int Cap { get; set; }
        }

        /// <summary>
        ///   The ordered participant INDICES to give a turn this burst, round-robin from startIndex.
        ///   participantCount = number of present participants (main NPC + witnesses). Returns `cap` indices
        ///   (each in [0, participantCount)) when participantCount > 0 and cap > 0; empty list otherwise.
        ///   Round-robin means index = (startIndex + k) % participantCount for k in [0, cap). When
        ///   participantCount is less than cap this naturally wraps, producing back-and-forth cross-talk.
        /// </summary>
        public static IReadOnlyList<int> SpeakerOrder(RoundTableFacts facts)
        {
           if (facts == null) return System.Array.Empty<int>();

           int participantCount = facts.ParticipantCount;
           int startIndex = facts.StartIndex;
           int cap = facts.Cap;

            var order = new List<int>();
            if (participantCount <= 0 || cap <= 0)
                return order;

            var start = Normalize(startIndex, participantCount);
            for (var k = 0; k < cap; k++)
                order.Add((start + k) % participantCount);

            return order;
        }

        /// <summary>
        ///   Where the NEXT trigger should start, so over repeated triggers everyone gets a turn.
        ///   = (startIndex + cap) % participantCount, or 0 when participantCount is not positive.
        /// </summary>
        public static int NextStartIndex(RoundTableFacts facts)
        {
           if (facts == null) return 0;

           int participantCount = facts.ParticipantCount;
           int startIndex = facts.StartIndex;
           int cap = facts.Cap;

            if (participantCount <= 0)
                return 0;

            var start = Normalize(startIndex, participantCount);
            var advance = cap > 0 ? cap : 0;
            return (start + advance) % participantCount;
        }

        private static int Normalize(int index, int count)
            => ((index % count) + count) % count;
    }
}
