// Code written by Gabriel Mailhot, 10/07/2026.
// Round-table group conversation: once the floor is open (see RoundTableIntentDetector), the mod hands out a
// capped burst of NPC turns, round-robin, then returns control to the player. This is the pure arithmetic:
// which participant indices speak this burst, and where the next burst should start so that over repeated
// triggers everyone eventually gets a turn. Spawning the turns and gathering participants is engine glue (mod-side).

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
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
        ///   The ordered participant INDICES to give a turn this burst, round-robin from startIndex.
        ///   participantCount = number of present participants (main NPC + witnesses). Returns `cap` indices
        ///   (each in [0, participantCount)) when participantCount > 0 and cap > 0; empty list otherwise.
        ///   Round-robin means index = (startIndex + k) % participantCount for k in [0, cap). When
        ///   participantCount is less than cap this naturally wraps, producing back-and-forth cross-talk.
        /// </summary>
        public static IReadOnlyList<int> SpeakerOrder(int participantCount, int startIndex, int cap)
        {
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
        public static int NextStartIndex(int participantCount, int startIndex, int cap)
        {
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
