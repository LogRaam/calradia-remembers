// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 3 (bounty missions): pure reward math for a nemesis bounty. Scales with
// notoriety, the two counters NemesisIdentity already tracks for free (RecaptureCount = how often he
// has personally beaten the player, TimesEncountered = how often he has been the one behind an
// encounter at all): the more infamous he is, the richer the bounty on his head. Both counters are
// clamped to a sane ceiling so a long-lived, many-times-recaptured nemesis never breaks the quest
// reward economy (mirrors QuestFactory's own MaxRewardGold/MaxRewardRelation clamps).

#region

#endregion

namespace CalradiaRemembers.Logic.Nemesis
{
    /// <summary>
    ///   The gold and personal-relation reward a lord promises for a nemesis's capture or death,
    ///   scaled by his notoriety. Pure: takes the two counters already persisted on
    ///   <see cref="NemesisIdentity" /> and returns the reward, clamped.
    /// </summary>
    public static class NemesisBountyRewardPolicy
    {
        /// <summary>TUNE: floor payout for a nemesis nobody has ever slipped away from (RecaptureCount 0).</summary>
        public const int BaseGold = 200;

        /// <summary>TUNE: extra denars per time this nemesis has recaptured the player, the strongest notoriety signal.</summary>
        public const int GoldPerRecapture = 150;

        /// <summary>TUNE: extra denars per encounter he has been behind at all, a softer notoriety signal.</summary>
        public const int GoldPerEncounter = 30;

        /// <summary>TUNE: ceiling so a long-lived nemesis never breaks the quest reward economy.</summary>
        public const int MaxGold = 3000;

        /// <summary>TUNE: floor relation gain, matching a modest verified deed elsewhere in the quest framework.</summary>
        public const int BaseRelation = 3;

        /// <summary>TUNE: extra relation per recapture, a bounty on a foe who has personally beaten the player means more.</summary>
        public const int RelationPerRecapture = 1;

        /// <summary>TUNE: ceiling, matching QuestFactory's own MaxRewardRelation so a bounty never outstrips an ordinary quest's relation cap.</summary>
        public const int MaxRelation = 10;

        /// <summary>
        ///   The gold reward for this nemesis's bounty. A negative counter (should never happen) is treated
        ///   as zero rather than inflating the reward through subtraction.
        /// </summary>
        public static int ComputeGold(int recaptureCount, int timesEncountered)
        {
            if (recaptureCount < 0) recaptureCount = 0;
            if (timesEncountered < 0) timesEncountered = 0;

            int gold = BaseGold + recaptureCount * GoldPerRecapture + timesEncountered * GoldPerEncounter;

            return gold > MaxGold ? MaxGold : gold;
        }

        /// <summary>The personal-relation reward for this nemesis's bounty, scaled by RecaptureCount alone (the personal-grudge signal).</summary>
        public static int ComputeRelation(int recaptureCount)
        {
            if (recaptureCount < 0) recaptureCount = 0;

            int relation = BaseRelation + recaptureCount * RelationPerRecapture;

            return relation > MaxRelation ? MaxRelation : relation;
        }
    }
}
