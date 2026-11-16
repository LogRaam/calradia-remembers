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
//
// Aporo (Discord, 16/11/2026): Queen Rhagaea paid a castle of her realm, as a task's reward, to a player who was not her
// vassal; he became an independent lord of land cut out of her kingdom. Gabriel (16/11/2026): "b" - a ruler's land goes
// only to a lord sworn to her realm; to anyone else the promise degrades to the amends, as for any promise the world broke.
namespace CalradiaRemembers.Logic.Quests
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
        public static bool CanPromiseFief(FiefRewardFacts facts)
        {
           if (facts == null) return false;

           bool toggleOn = facts.ToggleOn;
           bool giverLeadsOwnClan = facts.GiverLeadsOwnClan;
           bool fiefOwnedByGiverClan = facts.FiefStillOwnedByGiverClan;

           return toggleOn && giverLeadsOwnClan && fiefOwnedByGiverClan;
        }

        /// <summary>
        ///   ISSUANCE gate for marriage_reward: legal only when the toggle is on, a spouse candidate resolved
        ///   (the giver themselves, or a kin of their house), the giver has real authority to give that hand,
        ///   the match is mechanically marriageable, and the PLAYER does not currently reign over a realm (a
        ///   reigning sovereign's marriage can move their own clan's crown as a side effect of MarriageAction,
        ///   COUNCIL_ACTIONS.md's Kimi review "Trou 3": never risked as a mere quest-reward side effect).
        /// </summary>
        public static bool CanPromiseMarriageReward(MarriageRewardFacts facts)
        {
           if (facts == null) return false;

           bool toggleOn = facts.ToggleOn;
           bool spouseResolved = facts.SpouseResolved;
           bool giverHasAuthorityOverSpouse = facts.GiverHasAuthorityOverSpouse;
           bool spouseMarriageable = facts.SpouseStillMarriageable;
           bool playerIsReigningSovereign = facts.PlayerIsReigningSovereign;

           return toggleOn && spouseResolved && giverHasAuthorityOverSpouse && spouseMarriageable && !playerIsReigningSovereign;
        }

        /// <summary>
        ///   What a promised fief is re-read against, live, at both ends of the promise: the ISSUANCE gate
        ///   <see cref="CanPromiseFief" /> and the PAYOUT re-validation <see cref="DecideFiefPayout" />
        ///   share this one type, so neither can quietly mean something different by the same fact.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class FiefRewardFacts
        {
           /// <summary>The heavy-reward feature is enabled at all.</summary>
           public bool ToggleOn { get; set; }

           /// <summary>The lord who promised the fief is still alive to hand it over.</summary>
           public bool GiverAlive { get; set; }

           /// <summary>The giver truly LEADS their own clan. A vassal has no fief of their own gift.</summary>
           public bool GiverLeadsOwnClan { get; set; }

           /// <summary>
           ///   The named town or castle is owned by that clan RIGHT NOW. Weeks pass, and fiefs change hands.
           /// </summary>
           public bool FiefStillOwnedByGiverClan { get; set; }

           /// <summary>
           ///   The player has since gone to war with the giver's clan. Delivering a fief to a wartime enemy is
           ///   absurd, and vanilla's own ChangeOwnerOfSettlementAction carries no such guard.
           /// </summary>
           public bool PlayerAtWarWithGiverClan { get; set; }

           /// <summary>
           ///   The giver's clan serves a realm and the player's clan is not sworn to it as a vassal (outside it, or there
           ///   for pay only). A realm's land is held by its own lords (Aporo, 16/11/2026). False when the giver serves none.
           /// </summary>
           public bool PlayerNotSwornToGiversRealm { get; set; }
        }

        /// <summary>
        ///   PAYOUT re-validation for grant_fief, mirroring the marriage_consent M-B4 pattern: everything the
        ///   promise depended on is re-read live, never trusted from the moment of issuance. Weeks may have
        ///   passed; the fief may have changed hands, the giver may have died or lost their own clan's
        ///   leadership, or the player may since have gone to war with the giver's clan (delivering a fief to
        ///   a wartime enemy is absurd, and vanilla's own ChangeOwnerOfSettlementAction carries no such guard).
        /// </summary>
        public static QuestRewardPayoutVerdict DecideFiefPayout(FiefRewardFacts facts)
        {
           if (facts == null) return QuestRewardPayoutVerdict.Refuse;

           bool toggleOn = facts.ToggleOn;
           bool giverAlive = facts.GiverAlive;
           bool giverLeadsOwnClan = facts.GiverLeadsOwnClan;
           bool fiefStillOwnedByGiverClan = facts.FiefStillOwnedByGiverClan;
           bool playerAtWarWithGiverClan = facts.PlayerAtWarWithGiverClan;

            if (!toggleOn) return QuestRewardPayoutVerdict.Refuse;
            if (!giverAlive) return QuestRewardPayoutVerdict.Refuse;
            if (!giverLeadsOwnClan) return QuestRewardPayoutVerdict.Refuse;
            if (!fiefStillOwnedByGiverClan) return QuestRewardPayoutVerdict.Refuse;
            if (playerAtWarWithGiverClan) return QuestRewardPayoutVerdict.Refuse;
            if (facts.PlayerNotSwornToGiversRealm) return QuestRewardPayoutVerdict.Refuse;

            return QuestRewardPayoutVerdict.Deliver;
        }

        /// <summary>
        ///   What a promised marriage is re-read against at payout, live, never trusted from the moment the
        ///   promise was made. Shared with the ISSUANCE gate <see cref="CanPromiseMarriageReward" /> so the
        ///   two ends of the same promise cannot drift apart on what a fact is called.
        ///   SIX booleans in a row where two are requirements, two are disqualifiers, and one, the toggle,
        ///   is neither: the shape a swap survives.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class MarriageRewardFacts
        {
           /// <summary>The heavy-reward feature is enabled at all.</summary>
           public bool ToggleOn { get; set; }

           /// <summary>The lord who made the promise is still alive to keep it.</summary>
           public bool GiverAlive { get; set; }

           /// <summary>The giver truly has the standing to give that hand away.</summary>
           public bool GiverHasAuthorityOverSpouse { get; set; }

           /// <summary>
           ///   The match is STILL marriageable: alive, unwed, and not since promised elsewhere. At issuance
           ///   this is simply "is marriageable"; at payout it is the same question asked again, weeks later.
           /// </summary>
           public bool SpouseStillMarriageable { get; set; }

           /// <summary>The player has since gone to war with the giver's clan, which no reward survives.</summary>
           public bool PlayerAtWarWithGiverClan { get; set; }

           /// <summary>
           ///   The player reigns over a realm. A sovereign's marriage can move their own clan's crown as a side
           ///   effect, which is never risked as a quest-reward afterthought.
           /// </summary>
           public bool PlayerIsReigningSovereign { get; set; }

           /// <summary>
           ///   A spouse candidate actually resolved: the giver themselves, or a kin of their house. Asked at
           ///   issuance; by payout the question has become whether that same match is still marriageable.
           /// </summary>
           public bool SpouseResolved { get; set; }
        }

        /// <summary>
        ///   PAYOUT re-validation for marriage_reward: the marriage sibling of <see cref="DecideFiefPayout" />.
        ///   Re-checks the giver's authority, the spouse's marriageability (alive, unwed, not since promised
        ///   elsewhere), no standing war with the giver's clan, and the sovereignty guard again (the player's
        ///   own standing may have changed between the promise and the completion, e.g. a war they won made
        ///   them a king).
        /// </summary>
        public static QuestRewardPayoutVerdict DecideMarriageRewardPayout(MarriageRewardFacts facts)
        {
           if (facts == null) return QuestRewardPayoutVerdict.Refuse;

           bool toggleOn = facts.ToggleOn;
           bool giverAlive = facts.GiverAlive;
           bool giverHasAuthorityOverSpouse = facts.GiverHasAuthorityOverSpouse;
           bool spouseStillMarriageable = facts.SpouseStillMarriageable;
           bool playerAtWarWithGiverClan = facts.PlayerAtWarWithGiverClan;
           bool playerIsReigningSovereign = facts.PlayerIsReigningSovereign;

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
