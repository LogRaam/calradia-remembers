// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod's ContextualNameResolver: the rules that decide how an NPC describes a third
// party the player mentioned (close friend … bitter enemy … stranger) and how confidently they could
// place that person. Pure so the relation thresholds and the kin/clan/faction gating of gossip — used
// on every message that names someone, and silent when they drift — stay protected by tests.

using NpcMemoryService.Core.Standing;

namespace CalradiaRemembers.Logic
{
    /// <summary>How confidently an NPC could say where a third party is, from their social tie alone.</summary>
    public enum KnowledgeTier
    {
        /// <summary>Would not know at all.</summary>
        None,

        /// <summary>Same faction/liege — rough military hearsay only.</summary>
        Faction,

        /// <summary>Genuine regard — recent hearsay.</summary>
        Friend,

        /// <summary>Family, clan, or a very close friend — confident.</summary>
        Close,

        /// <summary>Physically in the same place — certain.</summary>
        Present
    }

    /// <summary>
    ///   Pure categorisation of a third party from an NPC's point of view: the relationship label the
    ///   prompt injects, the natural-language tone of a relation score, and how plausibly the NPC
    ///   could relay that person's whereabouts. The host gathers the kin/clan/faction facts and the
    ///   relation score from the engine; these turn them into the exact words the prompt carries.
    /// </summary>
    /// <summary>
    ///   How one character stands to another, read off the engine by the caller: their kinship if any, their
    ///   house, their standing, and whether they are rivals for the same turf.
    ///   <para>
    ///     Named rather than positional (2026-09-10). This took an int and SIX booleans in a row, and its own
    ///     tests called it half-named: Categorize(-50, isSpouse: true, false, false, false, sameClan: false),
    ///     with three bare values in the middle standing for father, mother and child. Nothing but care kept
    ///     them in the right order, in the very file meant to prove the ordering.
    ///   </para>
    /// </summary>
    public sealed class ThirdPartyFacts
    {
        /// <summary>The engine's relation score between the two of them.</summary>
        public int Relation { get; set; }

        /// <summary>They are married to one another.</summary>
        public bool IsSpouse { get; set; }

        /// <summary>The other is this character's father.</summary>
        public bool IsFather { get; set; }

        /// <summary>The other is this character's mother.</summary>
        public bool IsMother { get; set; }

        /// <summary>The other is this character's child.</summary>
        public bool IsChild { get; set; }

        /// <summary>They belong to the same clan.</summary>
        public bool SameClan { get; set; }

        /// <summary>
        ///   Both are gang leaders holding the same turf, which makes them structural rivals whatever their
        ///   culture or proximity would otherwise suggest (player report: gangs hallucinated friendships with
        ///   the boss across the square).
        /// </summary>
        public bool AreRivalGangLeaders { get; set; }
    }

    public static class ThirdPartyDescriptor
    {
        // THE WORDS THE MODEL READS, on the rungs the game already decides by. Gabriel's own example of a
        // duplication nobody could see (20/09/2026): "FriendThreshold" was a private constant here, in
        // RelationshipsBuilder and in SchemeBacklash, all three at 10, and answering "how friendly is a friend" meant
        // reading five files. One home now; no value moved.
        private const int FriendThreshold = VanillaRelation.FriendTier;
        private const int EnemyThreshold = VanillaRelation.EnemyTier;
        private const int CloseThreshold = VanillaRelation.CloseTier;
        private const int BitterThreshold = VanillaRelation.BitterTier;

        /// <summary>
        ///   The label for how the NPC regards <c>other</c>. Kinship wins first, then the relation
        ///   tiers; a clanmate at neutral standing is named as such; otherwise an acquaintance or a
        ///   stranger. Mirrors the original ordering exactly.
        /// </summary>
        public static string Categorize(ThirdPartyFacts facts)
        {
            if (facts == null) return "a stranger";

            int relation = facts.Relation;
            bool isSpouse = facts.IsSpouse;
            bool isFather = facts.IsFather;
            bool isMother = facts.IsMother;
            bool isChild = facts.IsChild;
            bool sameClan = facts.SameClan;
            bool areRivalGangLeaders = facts.AreRivalGangLeaders;

            if (isSpouse) return "your spouse";
            if (isFather) return "your father";
            if (isMother) return "your mother";
            if (isChild) return "your child";

            // Same-turf gang leaders are structural RIVALS, never friends by shared culture or proximity
            // (player report: gangs hallucinated friendships/alliances with same-town rival bosses). This
            // overrides the friendly and neutral tiers; a genuine ENEMY relation below is stronger and kept.
            if (areRivalGangLeaders && relation > EnemyThreshold)
                return "a rival gang leader (a competitor for the same turf, not a friend)";

            if (relation >= CloseThreshold) return $"a very close friend ({DescribeRelation(relation)})";
            if (relation >= FriendThreshold) return $"a friend ({DescribeRelation(relation)})";
            if (relation <= BitterThreshold) return $"a bitter enemy ({DescribeRelation(relation)})";
            if (relation <= EnemyThreshold) return $"an enemy ({DescribeRelation(relation)})";
            if (sameClan) return "a member of your clan";
            if (relation != VanillaRelation.NeutralTier) return $"an acquaintance ({DescribeRelation(relation)})";

            return "not personally known to you";
        }

        /// <summary>
        ///   The gender noun for a third party, so the NPC never guesses it from a gender-ambiguous name
        ///   (a player reported an NPC "correcting" them that a woman named Redeye Gunnor was a man).
        /// </summary>
        public static string GenderNoun(bool isFemale) => isFemale ? "a woman" : "a man";

        /// <summary>The natural-language tone of a relation score, for the parenthetical in a label.</summary>
        public static string DescribeRelation(int r) => r switch
        {
            >= 30 => "deep trust",
            >= 10 => "genuine regard",
            >= -9 => "neutral",
            >= -29 => "quiet distrust",
            _ => "resentment"
        };

        /// <summary>
        ///   How confidently the NPC could place <c>other</c>: present together is certain; close kin
        ///   or clan (or a very close friend) is confident; a friend has recent hearsay; a faction-mate
        ///   knows rough movements; anyone else, nothing.
        /// </summary>
        public static KnowledgeTier KnowledgeOf(bool isPresentTogether, bool isCloseKinOrClan, int relation, bool sameFaction)
        {
            if (isPresentTogether) return KnowledgeTier.Present;
            if (isCloseKinOrClan) return KnowledgeTier.Close;
            if (relation >= CloseThreshold) return KnowledgeTier.Close;
            if (relation >= FriendThreshold) return KnowledgeTier.Friend;
            if (sameFaction) return KnowledgeTier.Faction;
            return KnowledgeTier.None;
        }

        /// <summary>
        ///   The whereabouts clause the NPC could plausibly relay, framed by how they would know it,
        ///   or null when they would not know at all. <paramref name="place" /> is a ready phrase
        ///   ("at Pravend" / a map activity), null when nothing concrete can be said about a person
        ///   the NPC otherwise knows. <paramref name="presentPlaceName" /> names where they both are.
        /// </summary>
        public static string WhereaboutsClause(KnowledgeTier tier, string place, string presentPlaceName)
        {
            switch (tier)
            {
                case KnowledgeTier.None:
                    return null;
                case KnowledgeTier.Present:
                    return $"they are here with you, at {presentPlaceName}";
                default:
                    if (place == null) return "you could not say where they are just now";
                    switch (tier)
                    {
                        case KnowledgeTier.Close:   return $"last you knew, they were {place}";
                        case KnowledgeTier.Friend:  return $"you last heard they were {place}";
                        case KnowledgeTier.Faction: return $"word among your own faction has them {place}";
                        default:                    return null;
                    }
            }
        }

        /// <summary>Who currently holds a mentioned person prisoner, if anyone.</summary>
        public enum PrisonerHolder
        {
            /// <summary>Not a prisoner (or unknown).</summary>
            None,

            /// <summary>THIS NPC (or their party/clan) holds them.</summary>
            ThisNpc,

            /// <summary>The player holds them.</summary>
            Player,

            /// <summary>A third party holds them.</summary>
            Other
        }

        /// <summary>
        ///   A CURRENT, hard fact about a mentioned person's captivity, so an NPC who has just taken someone
        ///   prisoner states it plainly instead of inventing a false backstory (player report 2026-08-16: a captor
        ///   who had just won a battle claimed he had held the loser "two moons past" and already sold her off).
        ///   Distinct from the whereabouts clause, which is stale hearsay; this is present-tense truth. Null when
        ///   the person is not held.
        /// </summary>
        public static string PrisonerClause(PrisonerHolder holder, string captorName, string name)
        {
            switch (holder)
            {
                case PrisonerHolder.ThisNpc:
                    return $"You are holding {name} prisoner RIGHT NOW. If asked, say so plainly; do NOT invent a past capture, a sale, or any other fate for them.";
                case PrisonerHolder.Player:
                    return $"The player is currently holding {name} prisoner.";
                case PrisonerHolder.Other:
                    return string.IsNullOrWhiteSpace(captorName)
                        ? $"{name} is currently being held prisoner by someone else."
                        : $"{name} is currently being held prisoner by {captorName}.";
                default:
                    return null;
            }
        }
    }
}
