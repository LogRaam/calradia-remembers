// Code written by Gabriel Mailhot, 20/06/2026.
// The gate for persuading a LORD (not a wanderer) to leave their clan and join the player's — the
// most-requested AI-Influence feature. Pure so the three gates (deep trust, the player outranking
// the lord, and the lord being genuinely vulnerable) and their tuning stay protected by tests; they
// guard a powerful action and would break in silence.

namespace CalradiaRemembers.Logic
{
    /// <summary>Why a lord cannot (yet) be recruited — <see cref="LordRecruitmentBlock.None"/> means they can.</summary>
    public enum LordRecruitmentBlock
    {
        /// <summary>All gates pass — the lord is recruitable.</summary>
        None,

        /// <summary>The personal bond is not deep enough; no lord abandons their house for an acquaintance.</summary>
        TrustTooLow,

        /// <summary>The player's clan does not outrank the lord's — a duke does not follow a lesser house.</summary>
        PlayerRankTooLow,

        /// <summary>The lord is too comfortable to leave: landed, in a strong clan, and content with their liege.</summary>
        LordNotVulnerable,

        /// <summary>The lord IS their own clan's leader: dissolving their house into the player's by a chat line would orphan the clan.</summary>
        LordLeadsOwnClan
    }

    /// <summary>The engine-gathered facts a lord-recruitment decision rests on — pure to reason over.</summary>
    public sealed class LordRecruitmentFacts
    {
        /// <summary>The lord's personal relation toward the player.</summary>
        public int Relation { get; set; }

        /// <summary>The player's clan tier (0–6).</summary>
        public int PlayerClanTier { get; set; }

        /// <summary>The lord's clan tier (0–6).</summary>
        public int LordClanTier { get; set; }

        /// <summary>True when the lord's clan holds no town or castle.</summary>
        public bool LordIsLandless { get; set; }

        /// <summary>True when the lord's clan is destroyed, or so weakened it can no longer protect them.</summary>
        public bool LordClanWeakOrDestroyed { get; set; }

        /// <summary>True when the lord currently serves a liege (is a kingdom vassal under someone).</summary>
        public bool HasLiege { get; set; }

        /// <summary>The lord's relation toward their current liege; only weighed when <see cref="HasLiege"/>.</summary>
        public int RelationWithLiege { get; set; }

        /// <summary>
        ///   True when the lord IS the leader of their own clan. Such a lord can never be recruited into the
        ///   player's clan: reassigning their Clan would leave Clan.Leader / Kingdom.Leader dangling on the
        ///   abandoned house (save corruption). Only a non-leader member may defect.
        /// </summary>
        public bool IsClanLeader { get; set; }
    }

    /// <summary>
    ///   Decides whether a lord could be persuaded to defect into the player's clan. THREE gates,
    ///   all required: a genuinely deep bond, the player's house outranking the lord's, and the lord
    ///   being in a vulnerable position (landless, their clan broken, or estranged from their liege)
    ///   — no one abandons comfort lightly. The LLM negotiates the story; this (and the bridge that
    ///   re-checks it) is the law.
    /// </summary>
    public static class LordRecruitment
    {
        /// <summary>Trust must be genuinely deep, not merely friendly, to uproot a lord from their house.</summary>
        public const int MinRelation = 60;

        /// <summary>At or below this relation toward their liege, a lord is estranged enough to consider leaving.</summary>
        public const int LiegeResentmentThreshold = -20;

        /// <summary>The blocking reason, or <see cref="LordRecruitmentBlock.None"/> when the lord is recruitable.</summary>
        public static LordRecruitmentBlock Evaluate(LordRecruitmentFacts f)
        {
            if (f == null) return LordRecruitmentBlock.TrustTooLow;

            // Categorical, checked before every warmth/rank/vulnerability gate: a clan LEADER cannot be
            // uprooted into the player's house at all (it would orphan their own clan), however deep the bond.
            if (f.IsClanLeader) return LordRecruitmentBlock.LordLeadsOwnClan;

            if (f.Relation < MinRelation) return LordRecruitmentBlock.TrustTooLow;
            if (f.PlayerClanTier < f.LordClanTier) return LordRecruitmentBlock.PlayerRankTooLow;
            if (!IsVulnerable(f)) return LordRecruitmentBlock.LordNotVulnerable;

            return LordRecruitmentBlock.None;
        }

        /// <summary>True when the player passes every gate — convenience over <see cref="Evaluate"/>.</summary>
        public static bool IsEligible(LordRecruitmentFacts f) => Evaluate(f) == LordRecruitmentBlock.None;

        /// <summary>
        ///   A lord is vulnerable enough to court when their clan holds no land, their clan is
        ///   broken or too weak to shelter them, or they are estranged from the liege they serve.
        /// </summary>
        private static bool IsVulnerable(LordRecruitmentFacts f)
            => f.LordIsLandless
            || f.LordClanWeakOrDestroyed
            || (f.HasLiege && f.RelationWithLiege <= LiegeResentmentThreshold);
    }
}
