// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "delivery quests, recruitment quests". A lord leading a party asks the player to
// bring him fresh recruits of his own people; handed over in conversation, they join his ranks. Fresh means raw: the
// first two tiers, the men a recruiter actually brings back from the villages, not veterans the player trained.
// How many may be asked is the garrison task's own band (ReinforceGarrisonPolicy.TroopsToAsk), so the two ways of
// asking a player for men cannot drift apart.

namespace CalradiaRemembers.Logic
{
    /// <summary>One kind of soldier in the player's party, as a recruit task reads it.</summary>
    public sealed class TroopFacts
    {
        /// <summary>A hero (a companion, a lord): never a recruit.</summary>
        public bool IsHero { get; set; }

        /// <summary>The troop's tier.</summary>
        public int Tier { get; set; }

        /// <summary>The troop's culture id.</summary>
        public string CultureId { get; set; }
    }

    /// <summary>The rules of a recruit_troops task. Pure.</summary>
    public static class RecruitTroopsPolicy
    {
        /// <summary>The highest tier that is still a fresh recruit.</summary>
        public const int MaxRecruitTier = 2;

        /// <summary>Whether this soldier counts as a recruit of the culture asked for (any culture when none was asked).</summary>
        public static bool Counts(TroopFacts t, string cultureId)
            => t != null && !t.IsHero && t.Tier <= MaxRecruitTier
               && (string.IsNullOrEmpty(cultureId) || t.CultureId == cultureId);

        /// <summary>How many to ask for: the garrison task's band, against the room left in the giver's party.</summary>
        public static int? TroopsToAsk(int proposed, int roomLeft) => ReinforceGarrisonPolicy.TroopsToAsk(proposed, roomLeft);
    }
}
