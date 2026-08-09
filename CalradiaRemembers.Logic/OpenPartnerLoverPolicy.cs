// Code written by Gabriel Mailhot, 09/08/2026.
// Open relationship (Inc 3a): the symmetry of Inc 2. An open partner who holds reciprocal open terms with
// the player does not merely tolerate the player's other loves (Inc 2 suppresses her jealousy); over time
// she exercises the SAME freedom and takes a lover of her OWN, the likelier the more the player neglects
// her. This pure policy owns the two decisions the mod-side autonomy must not fork: WHETHER she strays this
// week (a neglect-scaled weekly roll), and WHO is a plausible lover (the eligibility rule). The engine-
// coupled scan (walking heroes, reading orientation/relation) lives in the mod behavior; the RULE lives
// here, named and tested, so the tuning and the eligibility bar have exactly one home. Inc 3a forms only the
// LOVER LINK: the trysts, the player's awareness/jealousy, and pregnancy by the lover are later increments.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure rules for an OPEN partner autonomously taking a lover of her own: the neglect-scaled weekly
    ///   chance that she strays, and whether a given candidate is a plausible lover. Both are consumed by the
    ///   mod's weekly open-partner autonomy; the host supplies the engine facts, this holds the decision.
    /// </summary>
    public static class OpenPartnerLoverPolicy
    {
        // ── Tuning knobs (weekly chance) ─────────────────────────────────────────
        // A well-attended open partner rarely strays; a long-neglected one is much likelier, but never
        // certain in any single week. The floor keeps even an attended open partner faintly free; the neglect
        // bonus climbs the chance day by day past a one-week grace; the cap keeps it a chance, not a sentence.

        /// <summary>The weekly chance floor: even an attended open partner is faintly free to stray.</summary>
        public const int BaseChancePercent = 2;

        /// <summary>The weekly chance ceiling: no matter how neglected, she strays at most this often per week.</summary>
        public const int MaxChancePercent = 40;

        /// <summary>Neglect within the first week apart adds nothing: a partner briefly away is not neglected.</summary>
        public const int NeglectGraceDays = 7;

        /// <summary>Each neglected day BEYOND the grace adds this many percent to the weekly chance.</summary>
        public const int NeglectBonusPerDay = 1;

        /// <summary>
        ///   The weekly percent chance that an open partner takes a lover, rising with <paramref name="neglectDays" />:
        ///   the <see cref="BaseChancePercent" /> floor plus <see cref="NeglectBonusPerDay" /> for each neglected day
        ///   past <see cref="NeglectGraceDays" />, clamped to <see cref="MaxChancePercent" />. Monotonic in neglect.
        /// </summary>
        public static int ChancePercent(int neglectDays)
        {
            int bonusDays = neglectDays - NeglectGraceDays;
            if (bonusDays < 0) bonusDays = 0;
            int chance = BaseChancePercent + bonusDays * NeglectBonusPerDay;
            if (chance > MaxChancePercent) chance = MaxChancePercent;
            if (chance < BaseChancePercent) chance = BaseChancePercent;
            return chance;
        }

        /// <summary>
        ///   Whether a committed partner is ELIGIBLE at all to take a lover this week, before the neglect roll.
        ///   Her CULTURE decides: an egalitarian wife strays ONLY under a reciprocal open arrangement
        ///   (<paramref name="hasOpenArrangement" />), never otherwise; a harem / patriarchal (submissive-wife)
        ///   culture (<paramref name="isHaremCulture" />) may stray when neglected WHETHER OR NOT there is an
        ///   arrangement, but the unsolicited-infidelity path (no arrangement) is gated by
        ///   <paramref name="unsolicitedInfidelityAllowed" /> (the host's EnableJealousy toggle, so a drama-free
        ///   household keeps wives faithful). The open-arrangement paths are never gated: the player opted into
        ///   openness. The host still rolls <see cref="ShouldTakeLover" /> afterwards for the neglect chance.
        /// </summary>
        public static bool CanTakeLover(bool hasOpenArrangement, bool isHaremCulture, bool unsolicitedInfidelityAllowed)
        {
            if (hasOpenArrangement) return true;
            return isHaremCulture && unsolicitedInfidelityAllowed;
        }

        /// <summary>
        ///   Whether the lover this partner takes is SECRET (hidden from the player) rather than an OPEN,
        ///   acknowledged one. A harem / patriarchal-culture wife (<paramref name="isHaremCulture" />) always
        ///   hides her lover, even within an open arrangement (cultural submission); an egalitarian wife takes
        ///   an OPEN lover under her reciprocal open arrangement. Drives RomanticProfile.LoverIsSecret on the
        ///   mod side: a secret affair lays no public rumour and must be DISCOVERED later; an open lover keeps
        ///   the faint public rumour.
        /// </summary>
        public static bool LoverIsSecret(bool isHaremCulture) => isHaremCulture;

        /// <summary>
        ///   Whether an open partner takes a lover this week. Refuses unless she is an open committed partner with
        ///   no existing lover; then true when <paramref name="rollPercent" /> (0..99) falls under
        ///   <see cref="ChancePercent" /> for <paramref name="neglectDays" />. The host rolls the percent
        ///   (via MBRandom) so the decision stays pure and testable.
        /// </summary>
        public static bool ShouldTakeLover(bool isOpenCommittedPartner, bool alreadyHasLover, int neglectDays, int rollPercent)
        {
            if (!isOpenCommittedPartner) return false;
            if (alreadyHasLover) return false;
            return rollPercent < ChancePercent(neglectDays);
        }

        /// <summary>
        ///   Whether a scanned candidate is a plausible lover for the open partner. She prefers an UNWED,
        ///   orientation-compatible adult hero she is at least acquainted with, and NEVER the player or a member
        ///   of the player's own household (that cascade drama is out of scope this increment). Every condition
        ///   must hold; the host computes each fact from the engine and this pins the bar.
        /// </summary>
        public static bool IsPlausibleLover(
            bool candidateAlive,
            bool candidateIsHero,
            bool orientationCompatible,
            bool candidateUnwed,
            bool candidateAdult,
            bool acquainted,
            bool isThePlayer,
            bool isSameClanAsPlayerHousehold)
        {
            if (isThePlayer) return false;
            if (isSameClanAsPlayerHousehold) return false;
            if (!candidateAlive || !candidateIsHero) return false;
            if (!orientationCompatible) return false;
            if (!candidateUnwed || !candidateAdult) return false;
            return acquainted;
        }
    }
}
