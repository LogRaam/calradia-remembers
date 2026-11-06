// Code written by Gabriel Mailhot, 05/11/2026.
// What each side lost in a battle the player fought, in the memory of those who stood on the field. 3chmidt (Nexus,
// 05/11/2026): "It would be also nice, if NPC's also notice your actions in a battle, how many you killed, how many your
// army killed and how many of your army was wounded and killed. Feels like most NPC's shrug it off as me lying when I
// tell them I killed 30 or 50 enemies, even when they fought on the same battlefield." A battle memory said who won and
// where, never what it cost. This adds the toll each side paid, as a captain would tell it (round figures, never a
// tally), seen from the side of the one who remembers. Pure; the host counts the engine's rosters, auto-resolved battles
// included.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>What a battle cost each side, seen from the one who remembers it.</summary>
    public sealed class BattleLossFacts
    {
        /// <summary>Men of the rememberer's own side killed.</summary>
        public int OurFallen { get; init; }

        /// <summary>Men of the rememberer's own side wounded.</summary>
        public int OurWounded { get; init; }

        /// <summary>Men of the other side killed.</summary>
        public int TheirFallen { get; init; }

        /// <summary>Men of the other side wounded.</summary>
        public int TheirWounded { get; init; }
    }

    /// <summary>The sentence a battle's toll adds to a memory of it.</summary>
    public static class BattleLossesText
    {
        /// <summary>
        ///   "On our side some 40 fell and some 90 were wounded; on theirs some 250 fell and some 300 were wounded." Null when
        ///   neither side lost a man, so a bloodless rout says nothing it did not see.
        /// </summary>
        public static string Line(BattleLossFacts f)
        {
            if (f == null) return null;
            string ours = SideToll(f.OurFallen, f.OurWounded);
            string theirs = SideToll(f.TheirFallen, f.TheirWounded);
            if (ours == null && theirs == null) return null;

            var parts = new List<string>();
            parts.Add($"on our side {ours ?? "no man was lost"}");
            parts.Add($"on theirs {theirs ?? "no man was lost"}");
            string line = string.Join("; ", parts);

            return char.ToUpperInvariant(line[0]) + line.Substring(1) + ".";
        }

        /// <summary>A count as it is told after a battle: one, a handful, then round figures.</summary>
        public static string Count(int men)
        {
            if (men <= 1) return men == 1 ? "one" : "none";
            if (men < 10) return "a handful";
            int step = men < 100 ? 10 : men < 1000 ? 50 : 100;
            int rounded = (men + step / 2) / step * step;

            return $"some {rounded:N0}";
        }

        private static string SideToll(int fallen, int wounded)
        {
            if (fallen <= 0 && wounded <= 0) return null;
            if (wounded <= 0) return $"{Count(fallen)} fell";
            if (fallen <= 0) return $"none fell but {Count(wounded)} were wounded";

            return $"{Count(fallen)} fell and {Count(wounded)} were wounded";
        }
    }
}
