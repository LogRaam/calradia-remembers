// Code written by Gabriel Mailhot, 09/08/2026.
// Open relationship (Inc 3a): the symmetry of Inc 2. An open partner who holds reciprocal open terms with
// the player does not merely tolerate the player's other loves (Inc 2 suppresses her jealousy); over time
// she exercises the SAME freedom and takes a lover of her OWN, the likelier the more the player neglects
// her. This pure policy owns the two decisions the mod-side autonomy must not fork: WHETHER she strays this
// week (a neglect-scaled weekly roll), and WHO is a plausible lover (the eligibility rule). The engine-
// coupled scan (walking heroes, reading orientation/relation) lives in the mod behavior; the RULE lives
// here, named and tested, so the tuning and the eligibility bar have exactly one home. Inc 3a forms only the
// LOVER LINK: the trysts, the player's awareness/jealousy, and pregnancy by the lover are later increments.

namespace CalradiaRemembers.Logic.Romance
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
        ///   What decides whether a committed partner may take a lover at all, before any neglect roll. Three
        ///   booleans in a row on three different levels, the arrangement, the culture and the host's toggle,
        ///   and only ONE of the two paths is gated by the toggle. A swap gates the wrong one, which either
        ///   keeps a harem culture faithful or makes an egalitarian one stray without the player opting in.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class LoverEligibilityFacts
        {
           /// <summary>
           ///   A reciprocal open arrangement is in place. Never gated by the toggle: the player opted into openness.
           /// </summary>
           public bool HasOpenArrangement { get; set; }

           /// <summary>
           ///   Hers is a harem, patriarchal, submissive-wife culture, which may stray when neglected whether or
           ///   not an arrangement exists.
           /// </summary>
           public bool IsHaremCulture { get; set; }

           /// <summary>
           ///   The host's EnableJealousy toggle. Gates the no-arrangement path ONLY, so a drama-free household
           ///   keeps wives faithful.
           /// </summary>
           public bool UnsolicitedInfidelityAllowed { get; set; }
        }

        /// <summary>
        ///   Whether a committed partner is ELIGIBLE at all to take a lover this week, before the neglect roll.
        ///   Her CULTURE decides: an egalitarian wife strays ONLY under a reciprocal open arrangement
        ///   (<c>HasOpenArrangement</c>), never otherwise; a harem / patriarchal (submissive-wife)
        ///   culture (<c>IsHaremCulture</c>) may stray when neglected WHETHER OR NOT there is an
        ///   arrangement, but the unsolicited-infidelity path (no arrangement) is gated by
        ///   <c>UnsolicitedInfidelityAllowed</c> (the host's EnableJealousy toggle, so a drama-free
        ///   household keeps wives faithful). The open-arrangement paths are never gated: the player opted into
        ///   openness. The host still rolls <see cref="ShouldTakeLover" /> afterwards for the neglect chance.
        /// </summary>
        public static bool CanTakeLover(LoverEligibilityFacts facts)
        {
           if (facts == null) return false;

           bool hasOpenArrangement = facts.HasOpenArrangement;
           bool isHaremCulture = facts.IsHaremCulture;
           bool unsolicitedInfidelityAllowed = facts.UnsolicitedInfidelityAllowed;

            if (hasOpenArrangement) return true;
            return isHaremCulture && unsolicitedInfidelityAllowed;
        }

        /// <summary>
        ///   Whether the lover this partner takes is SECRET (hidden from the player) rather than an OPEN,
        ///   acknowledged one. A harem / patriarchal-culture wife (<c>IsHaremCulture</c>) always
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
        ///   What is known about somebody the open partner might turn to, read off the engine by the caller.
        ///   <para>
        ///     Named rather than positional (2026-09-10): this was EIGHT booleans in a row, the longest such run
        ///     in the whole Logic project, and a silent swap is only possible inside a run like that. Nothing but
        ///     the caller's care stood between "is the player" and "is unwed", and a test would only have caught
        ///     the mix-up on a day the two happened to differ.
        ///   </para>
        /// </summary>
        public sealed class LoverCandidateFacts
        {
            /// <summary>They are alive.</summary>
            public bool IsAlive { get; set; }

            /// <summary>They are a hero rather than an anonymous troop.</summary>
            public bool IsHero { get; set; }

            /// <summary>Her orientation admits them.</summary>
            public bool OrientationCompatible { get; set; }

            /// <summary>They are unwed. She turns to someone free, not into a second scandal.</summary>
            public bool IsUnwed { get; set; }

            /// <summary>They are an adult.</summary>
            public bool IsAdult { get; set; }

            /// <summary>She knows them at least a little. A stranger is not a lover.</summary>
            public bool Acquainted { get; set; }

            /// <summary>They ARE the player, who is never the answer here.</summary>
            public bool IsThePlayer { get; set; }

            /// <summary>
            ///   They belong to the player's own household, whose cascade of drama is deliberately out of scope
            ///   for this increment.
            /// </summary>
            public bool IsOfPlayerHousehold { get; set; }
        }

        /// <summary>
        ///   Whether this candidate is a plausible lover for the open partner. She prefers an UNWED,
        ///   orientation-compatible adult she is at least acquainted with, and NEVER the player or a member of
        ///   the player's own household. Every condition must hold, and unknown facts refuse.
        /// </summary>
        public static bool IsPlausibleLover(LoverCandidateFacts candidate)
        {
            if (candidate == null) return false;

            if (candidate.IsThePlayer) return false;
            if (candidate.IsOfPlayerHousehold) return false;
            if (!candidate.IsAlive || !candidate.IsHero) return false;
            if (!candidate.OrientationCompatible) return false;
            if (!candidate.IsUnwed || !candidate.IsAdult) return false;

            return candidate.Acquainted;
        }
    }
}
