// Code written by Gabriel Mailhot, 17/10/2026.
// The houses at odds with a character's house, and those of affinity (docs/design/NPC_REGARD.md sections 5.4 and 8.2).
// Gabriel, 17/10/2026, on question 1 ("Hostile clan: the leader's regard, or being at war?"): "les deux": a house is
// listed by its leader's regard, and a house of a realm at war is marked as such. A house's regard is its lord's toward
// the page's house lord, as vanilla means a clan relation (leader to leader); what really happened between the two
// lords wins, with its reason, and the vanilla number stands when nothing did (the rule the friends lists follow).
// tashmetu (18/10/2026, with screenshots of Halthdar's and Svorni's pages): on a lord with many ties the houses came out
// as one dense block, "resentment between their lords" five or six times in a row, and a shared past shown twice, under
// Why and again under the house. So the houses are grouped by reason, one line per reason, and a house whose lord is
// already explained under Why is named through him instead of repeating the reason.
// Gabriel (23/10/2026), on "deep trust between their lords": "c'est pas tres clair" (it read as every lord of both houses).
// The vanilla number is between the two heads of house only, so a house that stands on it gets a row of its own, the two
// heads named with the number ("Hertogi and Ulfhild trust each other deeply (+42)"), the banner first, the war on the
// same line. A house with a story keeps sharing its row with the houses of the same story.
// tashmetu (2/11/2026): on the page of a character who is not the head of the house, "Hertogi and Ulfhild hold each other
// in good regard" read as if it were about the page's character, and every member of the house showed the same lines. The
// label of the section now says whose standing it is.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One other house, as the page's house sees it.</summary>
    public sealed class ClanRegardCandidate
    {
        /// <summary>The house's name.</summary>
        public string Name { get; init; }

        /// <summary>The house's id, so its row can show its banner.</summary>
        public string Id { get; init; }

        /// <summary>What really happened between the two lords gives this verdict (Neither when nothing does).</summary>
        public PairRegardVerdict Derived { get; init; }

        /// <summary>The reason of the derived verdict that the player may know; null when none may be shown.</summary>
        public string DerivedReason { get; init; }

        /// <summary>The vanilla relation between the two lords, used only when nothing derived applies.</summary>
        public int VanillaRelation { get; init; }

        /// <summary>The two houses' realms are at war.</summary>
        public bool AtWar { get; init; }

        /// <summary>
        ///   The name of the house's lord when the page already gives the derived reason under Why; the house is then
        ///   listed "through" him rather than repeating it. Null otherwise.
        /// </summary>
        public string Through { get; init; }

        /// <summary>The name of the page's head of house, one of the two the vanilla number is between.</summary>
        public string Lord { get; init; }

        /// <summary>The name of this house's head, the other of the two.</summary>
        public string OtherLord { get; init; }
    }

    /// <summary>One house in a row of the houses list.</summary>
    public sealed class ClanRegardHouse
    {
        /// <summary>The house's name.</summary>
        public string Name { get; init; }

        /// <summary>The house's id, for its banner.</summary>
        public string Id { get; init; }

        /// <summary>The two houses' realms are at war: marked under this house's banner.</summary>
        public bool AtWar { get; init; }

        /// <summary>The lord through whom the house is explained above, or null.</summary>
        public string Through { get; init; }
    }

    /// <summary>One row of the houses list: a side, a reason, and the houses sharing it.</summary>
    public sealed class ClanRegardGroup
    {
        /// <summary>Of affinity (pale green) rather than at odds (red).</summary>
        public bool Affinity { get; init; }

        /// <summary>The reason the houses share; null for the houses named through their lord above.</summary>
        public string Reason { get; init; }

        /// <summary>
        ///   A house standing on the vanilla number alone: the row is that one house, its reason told after its banner,
        ///   the war in the reason.
        /// </summary>
        public bool OneHouse { get; init; }

        /// <summary>The houses of the row, strongest first.</summary>
        public IReadOnlyList<ClanRegardHouse> Houses { get; init; }
    }

    /// <summary>Sorts other houses into at odds and of affinity, each line with its reason and the war mark.</summary>
    public static class ClanRegardListPolicy
    {
        /// <summary>At most this many houses on each list (design 8.2).</summary>
        public const int MaxPerList = 6;

        /// <summary>
        ///   The houses at odds and of affinity, one line per reason, the houses sharing it after it ("had his brother
        ///   executed: Osticos, dey Meroc (at war)"); a house on the vanilla number alone, a line of its own ("Osticos:
        ///   Hertogi and Ulfhild resent each other (-42); their realms are at war"); the lines with a story first.
        /// </summary>
        public static (IReadOnlyList<string> AtOdds, IReadOnlyList<string> Affinity) Lists(IEnumerable<ClanRegardCandidate> houses)
        {
            (List<ClanRegardGroup> odds, List<ClanRegardGroup> affinity) = Sides(houses);

            return (Lines(odds), Lines(affinity));
        }

        /// <summary>
        ///   The same houses as rows (Gabriel, 22/10/2026: each with its banner): affinity first, then odds; one row per
        ///   reason, ordered by its strongest house; the houses named through their lord in a last row of their side.
        /// </summary>
        public static IReadOnlyList<ClanRegardGroup> Groups(IEnumerable<ClanRegardCandidate> houses)
        {
            (List<ClanRegardGroup> odds, List<ClanRegardGroup> affinity) = Sides(houses);

            return affinity.Concat(odds).ToList();
        }

        private static (List<ClanRegardGroup> Odds, List<ClanRegardGroup> Affinity) Sides(IEnumerable<ClanRegardCandidate> houses)
        {
            var odds = new List<(int Rank, string Reason, bool OneHouse, ClanRegardCandidate House)>();
            var affinity = new List<(int Rank, string Reason, bool OneHouse, ClanRegardCandidate House)>();
            // A house named through its lord carries no reason of its own (Reason null).

            foreach (ClanRegardCandidate c in houses ?? Array.Empty<ClanRegardCandidate>())
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Name)) continue;

                if (c.Derived != PairRegardVerdict.Neither)
                {
                    if (c.DerivedReason == null) continue; // a reason the player may not read lists the house nowhere

                    // A derived verdict outranks any vanilla number: it has a story.
                    bool through = !string.IsNullOrWhiteSpace(c.Through);
                    (c.Derived == PairRegardVerdict.Enemy ? odds : affinity).Add((1000, through ? null : c.DerivedReason, false, c));
                }
                else if (c.VanillaRelation <= global::NpcMemoryService.Core.Standing.VanillaRelation.EnemyTier)
                    odds.Add((-c.VanillaRelation, HeadsLine(c), true, c));
                else if (c.VanillaRelation >= global::NpcMemoryService.Core.Standing.VanillaRelation.FriendTier)
                    affinity.Add((c.VanillaRelation, HeadsLine(c), true, c));
            }

            return (Grouped(odds, false), Grouped(affinity, true));
        }

        // The six strongest houses (design 8.2), then one row per reason, ordered by its strongest house; the houses
        // named through their lord last, as tashmetu laid it out ("...: Dvarroving, Rungniring; Skylfing, through
        // Hertogi Gornlautir").
        private static List<ClanRegardGroup> Grouped(IEnumerable<(int Rank, string Reason, bool OneHouse, ClanRegardCandidate House)> side, bool affinity)
        {
            var top = side.OrderByDescending(x => x.Rank).Take(MaxPerList).ToList();
            List<ClanRegardGroup> groups = top.Where(x => x.Reason != null).GroupBy(x => (House: x.OneHouse ? x.House.Id ?? x.House.Name : null, x.Reason))
                                              .Select(g => new ClanRegardGroup {
                                                 Affinity = affinity, Reason = g.First().Reason, OneHouse = g.First().OneHouse,
                                                 Houses = g.Select(x => House(x.House, false)).ToList()
                                              })
                                              .ToList();
            List<ClanRegardHouse> through = top.Where(x => x.Reason == null).Select(x => House(x.House, true)).ToList();
            if (through.Count > 0) groups.Add(new ClanRegardGroup {Affinity = affinity, Reason = null, Houses = through});

            return groups;
        }

        private static ClanRegardHouse House(ClanRegardCandidate c, bool through)
            => new() {Name = c.Name, Id = c.Id, AtWar = c.AtWar, Through = through ? c.Through : null};

        /// <summary>
        ///   The label over the houses: "Houses:" on the head's own page, "Houses (Banu Azad, through Shaykh Yasir):" on a
        ///   member's, whose houses are those of the head. A blank name falls back to the plain label.
        /// </summary>
        public static string HousesLabel(bool isHead, string houseName, string headName)
            => isHead || string.IsNullOrWhiteSpace(houseName) || string.IsNullOrWhiteSpace(headName)
                ? "Houses:"
                : $"Houses ({houseName}, through {headName}):";

        /// <summary>
        ///   The line of a house standing on the vanilla number alone: the two heads of house named, how they hold each
        ///   other, the number, and the war ("Hertogi and Ulfhild trust each other deeply (+42); their realms are at war").
        /// </summary>
        public static string HeadsLine(ClanRegardCandidate c)
        {
            string heads = string.IsNullOrWhiteSpace(c.Lord) || string.IsNullOrWhiteSpace(c.OtherLord)
                ? "Their heads of house"
                : $"{c.Lord} and {c.OtherLord}";
            int r = c.VanillaRelation;
            string how = r switch
            {
                >= 30 => "trust each other deeply",
                >= 10 => "hold each other in good regard",
                > -30 => "quietly distrust each other",
                _ => "resent each other"
            };
            string line = $"{heads} {how} ({r:+#;-#;0})";

            return c.AtWar ? line + "; their realms are at war" : line;
        }

        // The text form: "reason: house, house (at war)", the houses named through their lord closing the last line; a
        // house of its own row "house: its line", the war already in the line.
        private static IReadOnlyList<string> Lines(List<ClanRegardGroup> groups)
        {
            List<string> lines = groups.Where(g => g.Reason != null)
                                       .Select(g => g.OneHouse ? $"{g.Houses[0].Name}: {g.Reason}" : $"{g.Reason}: {string.Join(", ", g.Houses.Select(Label))}")
                                       .ToList();
            string through = string.Join("; ", groups.Where(g => g.Reason == null).SelectMany(g => g.Houses).Select(h => $"{Label(h)}, through {h.Through}"));
            if (through.Length == 0) return lines;
            if (lines.Count == 0) return new[] {through};

            lines[lines.Count - 1] += "; " + through;

            return lines;
        }

        private static string Label(ClanRegardHouse h) => h.AtWar ? $"{h.Name} (at war)" : h.Name;
    }
}
