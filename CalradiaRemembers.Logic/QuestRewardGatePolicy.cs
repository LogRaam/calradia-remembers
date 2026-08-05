// Code written by Gabriel Mailhot, 04/08/2026.
// COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): a king promised a fief and marriage to himself as the
// reward of a quest, and both evaporated on completion because no executor existed. This is the pure gate
// behind the fix, guarding BOTH new heavy reward_grant kinds (RewardGrant.GrantFief / MarriageReward) at the
// two moments that matter: ISSUANCE (never promise what the giver cannot truly pay right now, the "golden
// rule" the rest of the quest system already follows for GiveItem/GiveTroops/ReleasePrisoner) and PAYOUT
// (re-validate everything on the live world, mirroring the marriage_consent M-B4 pattern, since weeks may pass
// between the promise and the completion and the world does not hold still). The whole feature is gated behind
// a single MCM opt-in (ModSettings.AllowFiefAndMarriageQuestRewards, default OFF): every method here takes that
// as its first flag and refuses outright when it is false, so a toggle flipped off after a promise was made
// degrades the SAME way an unmet condition would, never a silent bypass.
//
// SCOPED DELIBERATELY: this only covers validation-at-issuance, re-validation-at-payout, and the binary
// deliver/refuse decision. The CONTINUOUS daily surveillance COUNCIL_ACTIONS.md describes (RewardPremiseGone,
// notifying the player the moment a promise breaks rather than only at completion) is a documented FOLLOW-UP,
// not built here.
namespace CalradiaRemembers.Logic
{
    /// <summary>The binary outcome of re-validating a heavy quest reward (fief or marriage) at payout.</summary>
    public enum QuestRewardPayoutVerdict
    {
        /// <summary>Every precondition still holds on the live world: deliver the reward now.</summary>
        Deliver,

        /// <summary>Something the promise depended on has changed: refuse and degrade gracefully, never fake it.</summary>
        Refuse
    }

    /// <summary>Pure eligibility/re-validation gate for RewardGrant.GrantFief and RewardGrant.MarriageReward. See file header.</summary>
    public static class QuestRewardGatePolicy
    {
        /// <summary>
        ///   TUNE (COUNCIL_ACTIONS.md Partie 5, Kimi review: "Deadline OBLIGATOIRE... une promesse de fief
        ///   sans echeance est une bombe a retardement de save"). A heavy reward's deadline is capped here,
        ///   never left open-ended: an unresolved promise that can never expire is exactly the kind of save
        ///   rot the rest of the quest system (DeadlineDay, RunDailyMaintenance) exists to avoid.
        /// </summary>
        public const int MaxHeavyRewardDeadlineDays = 60;

        /// <summary>
        ///   ISSUANCE gate for grant_fief: legal only when the toggle is on, the giver truly LEADS their own
        ///   clan (a vassal has no fief of their own gift, mirroring the council's own grant_fief authority
        ///   rule), and the named town/castle is owned by that same clan right now.
        /// </summary>
        public static bool CanPromiseFief(bool toggleOn, bool giverLeadsOwnClan, bool fiefOwnedByGiverClan)
            => toggleOn && giverLeadsOwnClan && fiefOwnedByGiverClan;

        /// <summary>
        ///   ISSUANCE gate for marriage_reward: legal only when the toggle is on, a spouse candidate resolved
        ///   (the giver themselves, or a kin of their house), the giver has real authority to give that hand,
        ///   the match is mechanically marriageable, and the PLAYER does not currently reign over a realm (a
        ///   reigning sovereign's marriage can move their own clan's crown as a side effect of MarriageAction,
        ///   COUNCIL_ACTIONS.md's Kimi review "Trou 3": never risked as a mere quest-reward side effect).
        /// </summary>
        public static bool CanPromiseMarriageReward(bool toggleOn,
                                                    bool spouseResolved,
                                                    bool giverHasAuthorityOverSpouse,
                                                    bool spouseMarriageable,
                                                    bool playerIsReigningSovereign)
            => toggleOn && spouseResolved && giverHasAuthorityOverSpouse && spouseMarriageable && !playerIsReigningSovereign;

        /// <summary>
        ///   PAYOUT re-validation for grant_fief, mirroring the marriage_consent M-B4 pattern: everything the
        ///   promise depended on is re-read live, never trusted from the moment of issuance. Weeks may have
        ///   passed; the fief may have changed hands, the giver may have died or lost their own clan's
        ///   leadership, or the player may since have gone to war with the giver's clan (delivering a fief to
        ///   a wartime enemy is absurd, and vanilla's own ChangeOwnerOfSettlementAction carries no such guard).
        /// </summary>
        public static QuestRewardPayoutVerdict DecideFiefPayout(bool toggleOn,
                                                                bool giverAlive,
                                                                bool giverLeadsOwnClan,
                                                                bool fiefStillOwnedByGiverClan,
                                                                bool playerAtWarWithGiverClan)
        {
            if (!toggleOn) return QuestRewardPayoutVerdict.Refuse;
            if (!giverAlive) return QuestRewardPayoutVerdict.Refuse;
            if (!giverLeadsOwnClan) return QuestRewardPayoutVerdict.Refuse;
            if (!fiefStillOwnedByGiverClan) return QuestRewardPayoutVerdict.Refuse;
            if (playerAtWarWithGiverClan) return QuestRewardPayoutVerdict.Refuse;

            return QuestRewardPayoutVerdict.Deliver;
        }

        /// <summary>
        ///   PAYOUT re-validation for marriage_reward: the marriage sibling of <see cref="DecideFiefPayout" />.
        ///   Re-checks the giver's authority, the spouse's marriageability (alive, unwed, not since promised
        ///   elsewhere), no standing war with the giver's clan, and the sovereignty guard again (the player's
        ///   own standing may have changed between the promise and the completion, e.g. a war they won made
        ///   them a king).
        /// </summary>
        public static QuestRewardPayoutVerdict DecideMarriageRewardPayout(bool toggleOn,
                                                                          bool giverAlive,
                                                                          bool giverHasAuthorityOverSpouse,
                                                                          bool spouseStillMarriageable,
                                                                          bool playerAtWarWithGiverClan,
                                                                          bool playerIsReigningSovereign)
        {
            if (!toggleOn) return QuestRewardPayoutVerdict.Refuse;
            if (!giverAlive) return QuestRewardPayoutVerdict.Refuse;
            if (!giverHasAuthorityOverSpouse) return QuestRewardPayoutVerdict.Refuse;
            if (!spouseStillMarriageable) return QuestRewardPayoutVerdict.Refuse;
            if (playerAtWarWithGiverClan) return QuestRewardPayoutVerdict.Refuse;
            if (playerIsReigningSovereign) return QuestRewardPayoutVerdict.Refuse;

            return QuestRewardPayoutVerdict.Deliver;
        }

        /// <summary>
        ///   Clamps a heavy reward's own deadline (in days from issuance) to [1, <see cref="MaxHeavyRewardDeadlineDays" />],
        ///   defaulting to the max when the LLM proposed none at all: a fief or marriage promise MUST expire,
        ///   never linger open-ended (see this class's own TUNE note on <see cref="MaxHeavyRewardDeadlineDays" />).
        /// </summary>
        public static int ClampHeavyRewardDeadlineDays(int? proposedDays)
            => proposedDays is > 0 && proposedDays <= MaxHeavyRewardDeadlineDays
                ? proposedDays.Value
                : MaxHeavyRewardDeadlineDays;
    }
}
