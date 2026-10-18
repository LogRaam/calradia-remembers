// Code written by Gabriel Mailhot, 17/10/2026.
// The houses at odds with a character's house, and those of affinity (docs/design/NPC_REGARD.md sections 5.4 and 8.2).
// Gabriel, 17/10/2026, on question 1 ("Hostile clan: the leader's regard, or being at war?"): "les deux": a house is
// listed by its leader's regard, and a house of a realm at war is marked as such. A house's regard is its lord's toward
// the page's house lord, as vanilla means a clan relation (leader to leader); what really happened between the two
// lords wins, with its reason, and the vanilla number stands when nothing did (the rule the friends lists follow).

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

        /// <summary>What really happened between the two lords gives this verdict (Neither when nothing does).</summary>
        public PairRegardVerdict Derived { get; init; }

        /// <summary>The reason of the derived verdict that the player may know; null when none may be shown.</summary>
        public string DerivedReason { get; init; }

        /// <summary>The vanilla relation between the two lords, used only when nothing derived applies.</summary>
        public int VanillaRelation { get; init; }

        /// <summary>The two houses' realms are at war.</summary>
        public bool AtWar { get; init; }
    }

    /// <summary>Sorts other houses into at odds and of affinity, each line with its reason and the war mark.</summary>
    public static class ClanRegardListPolicy
    {
        /// <summary>At most this many houses on each list (design 8.2).</summary>
        public const int MaxPerList = 6;

        /// <summary>The houses at odds and of affinity, as lines ready to show ("dey Meroc: had his brother executed (at war)").</summary>
        public static (IReadOnlyList<string> AtOdds, IReadOnlyList<string> Affinity) Lists(IEnumerable<ClanRegardCandidate> houses)
        {
            var odds = new List<(int Rank, string Line)>();
            var affinity = new List<(int Rank, string Line)>();

            foreach (ClanRegardCandidate c in houses ?? Array.Empty<ClanRegardCandidate>())
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Name)) continue;

                string war = c.AtWar ? " (at war)" : "";
                if (c.Derived != PairRegardVerdict.Neither)
                {
                    if (c.DerivedReason == null) continue; // a reason the player may not read lists the house nowhere

                    // A derived verdict outranks any vanilla number: it has a story.
                    var line = (Rank: 1000, Line: $"{c.Name}: {c.DerivedReason}{war}");
                    (c.Derived == PairRegardVerdict.Enemy ? odds : affinity).Add(line);
                }
                else if (c.VanillaRelation <= global::NpcMemoryService.Core.Standing.VanillaRelation.EnemyTier)
                    odds.Add((-c.VanillaRelation, $"{c.Name}: {ThirdPartyDescriptor.DescribeRelation(c.VanillaRelation)} between their lords{war}"));
                else if (c.VanillaRelation >= global::NpcMemoryService.Core.Standing.VanillaRelation.FriendTier)
                    affinity.Add((c.VanillaRelation, $"{c.Name}: {ThirdPartyDescriptor.DescribeRelation(c.VanillaRelation)} between their lords{war}"));
            }

            return (odds.OrderByDescending(x => x.Rank).Select(x => x.Line).Take(MaxPerList).ToList(),
                    affinity.OrderByDescending(x => x.Rank).Select(x => x.Line).Take(MaxPerList).ToList());
        }
    }
}
