// Code written by Gabriel Mailhot, 16/10/2026.
// The world's record of a battle, as rumour carries it. fkasad (Nexus, 16/10/2026), reading the log:
// "Warboss Nog Gutgrowla of Spiky 'Eadz defeated a host at the siege of Hemmerle" and "Roland the Marechal of Les
// Hardis de Montmirail defeated a host in battle": who lost? "a host" for a bunch of beaten looters? where? "People
// should remember a real battle vs a police action, where, who." A side no lord led was named "a host" whatever it
// was, a field battle had no place unless the game tied it to a settlement, and the size was never said (the model
// cannot tell a skirmish from a great battle). Gabriel's go the same day.

namespace CalradiaRemembers.Logic
{
    /// <summary>One side of a recorded battle, as the game knows it.</summary>
    public sealed class BattleSideFacts
    {
        /// <summary>The hero leading the side, already labelled (HeroLabel), or empty when no hero led it.</summary>
        public string LeaderName { get; init; }

        /// <summary>The leading party's own name ("Looters", "Sea Raiders", a caravan), or empty.</summary>
        public string PartyName { get; init; }

        /// <summary>The side was a bandit band.</summary>
        public bool IsBandit { get; init; }

        /// <summary>The realm the side fought for ("Vlandia"), or empty.</summary>
        public string FactionName { get; init; }
    }

    /// <summary>Pure: names a battle's sides and its scale for the world's record.</summary>
    public static class BattleRecordText
    {
        /// <summary>Below this many men on the field at the start, a fight is a skirmish, and carries less far.</summary>
        public const int SkirmishBelow = 60;

        /// <summary>
        ///   From this many men on the field at the start, a fight is "a battle of several hundred men" (below, "a clash of
        ///   a hundred or two"). The long-term memory keeps battles from here up (LongTermHistoryPolicy, design 7.7a).
        /// </summary>
        public const int SeveralHundredFrom = 250;

        /// <summary>From this many men on the field at the start, a fight is a great battle, and carries further.</summary>
        public const int GreatBattleFrom = 800;

        /// <summary>
        ///   A field battle the game tied to no settlement is said to be "near" the closest one within this map distance;
        ///   farther out, no place is named rather than a misleading one.
        /// </summary>
        public const float NearPlaceMaxDistance = 40f;

        /// <summary>
        ///   Names a side: its leader when a hero led it; else a band of bandits, men of a realm, or the party's own
        ///   name; "a host" only when nothing at all is known of it.
        /// </summary>
        public static string Side(BattleSideFacts side)
        {
            if (side == null) return "a host";
            if (!string.IsNullOrWhiteSpace(side.LeaderName)) return side.LeaderName.Trim();
            if (side.IsBandit) return string.IsNullOrWhiteSpace(side.PartyName) ? "a band of outlaws" : $"a band of {side.PartyName.Trim()}";
            if (!string.IsNullOrWhiteSpace(side.FactionName)) return $"men of {side.FactionName.Trim()}";

            return string.IsNullOrWhiteSpace(side.PartyName) ? "a host" : side.PartyName.Trim();
        }

        /// <summary>The scale in words, from the men on the field at the start (both sides); empty when unknown.</summary>
        public static string Scale(int menAtStart)
        {
            if (menAtStart <= 0) return string.Empty;
            if (menAtStart < SkirmishBelow) return "a skirmish of a few dozen men";
            if (menAtStart < SeveralHundredFrom) return "a clash of a hundred or two";
            if (menAtStart < GreatBattleFrom) return "a battle of several hundred men";

            return menAtStart < 2000 ? "a great battle of over a thousand men" : "a great battle of thousands";
        }

        /// <summary>
        ///   The record's line: "{winner} defeated {loser}{place}, {scale}." A field battle with no place reads
        ///   "in battle"; the scale is left out when unknown.
        /// </summary>
        public static string Summary(string winner, string loser, string placeClause, int menAtStart)
        {
            string place = string.IsNullOrEmpty(placeClause) ? " in battle" : placeClause;
            string scale = Scale(menAtStart);

            return $"{winner} defeated {loser}{place}{(scale.Length > 0 ? ", " + scale : string.Empty)}.";
        }

        /// <summary>
        ///   How far word of it carries: a fight over a settlement further than a field battle (45, 35), a skirmish
        ///   less far (minus 10), a great battle further (plus 10).
        /// </summary>
        public static int Magnitude(bool overASettlement, int menAtStart)
        {
            int m = overASettlement ? 45 : 35;
            if (menAtStart > 0 && menAtStart < SkirmishBelow) m -= 10;
            if (menAtStart >= GreatBattleFrom) m += 10;

            return m;
        }
    }
}
