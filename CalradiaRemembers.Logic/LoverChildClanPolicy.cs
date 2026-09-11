// Code written by Gabriel Mailhot, 10/08/2026.
// Open relationship (Inc 3c): the core attribution rule. A child a committed partner conceives by HER lover
// belongs to the genetic father (the lover) and its mother, NEVER the player-husband. The danger is a wife who
// married INTO the player's clan: the ordinary CR rule places a CR-conceived child in the MOTHER's clan, which
// for such a wife IS the player's clan, wrongly making her lover's child a member of the player's house. This
// pure policy owns the one decision the mod-side birth pipeline must not fork: which line a lover-child joins.
// It encodes the hard invariant as a testable rule so it can never silently regress: the lover's clan when he
// has one, else the mother's clan ONLY when that is not the player's, else neither (the host leaves the child
// where the engine placed it). The host maps the verdict to real Clan objects; the RULE lives here.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Which line a lover-child joins at birth, decided by <see cref="LoverChildClanPolicy" />. Never the
    ///   player's clan: the host maps <see cref="LoverClan" /> and <see cref="MotherClan" /> to the real clans,
    ///   and <see cref="LeaveAsIs" /> means neither is a valid non-player line so the host leaves the child
    ///   wherever the engine placed it (it must still never move it into the player's clan).
    /// </summary>
    public enum LoverChildClanTarget
    {
        /// <summary>Place the child in the lover's (genetic father's) clan: the normal, preferred outcome.</summary>
        LoverClan,

        /// <summary>The lover has no usable clan: fall back to the mother's clan, which is NOT the player's.</summary>
        MotherClan,

        /// <summary>Neither the lover nor the mother has a non-player clan: leave the child where it is.</summary>
        LeaveAsIs
    }

    /// <summary>
    ///   Pure rule for attributing a lover-child's clan (Inc 3c): the lover's line when he has one, else the
    ///   mother's line only when that is not the player's, else neither. The player's clan is never a valid
    ///   destination. The host supplies each engine fact (does the lover/mother have a clan, is it the player's)
    ///   and this pins the bar so the "never the player's clan" invariant cannot regress unnoticed.
    /// </summary>
    public static class LoverChildClanPolicy
    {
        /// <summary>
        ///   Which houses the two parents of a lover-child belong to. FOUR booleans in two matched pairs, and
        ///   the pairs are asked in PRIORITY order: swap the lover's pair with the mother's and the child
        ///   joins the wrong line, which is a thing a player reads in the encyclopedia years later.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class LoverChildFacts
        {
           /// <summary>The lover belongs to some clan.</summary>
           public bool LoverHasClan { get; set; }

           /// <summary>That clan is the player's own. The child never joins the player's clan by this route.</summary>
           public bool LoverClanIsPlayer { get; set; }

           /// <summary>The mother belongs to some clan.</summary>
           public bool MotherHasClan { get; set; }

           /// <summary>Her clan is the player's own, and so is not taken either.</summary>
           public bool MotherClanIsPlayer { get; set; }
        }

        /// <summary>
        ///   The line a lover-child joins. Prefers the LOVER's clan when he has one that is not the player's
        ///   (<c>LoverHasClan</c> and not <c>LoverClanIsPlayer</c>); otherwise the
        ///   MOTHER's clan when she has one that is not the player's (<c>MotherHasClan</c> and not
        ///   <c>MotherClanIsPlayer</c>); otherwise <see cref="LoverChildClanTarget.LeaveAsIs" />.
        ///   Because a lover is selected as a lord outside the player's household, the lover-clan branch is the
        ///   real path; the mother-clan fallback and LeaveAsIs guard the rare clanless-lover corner. In no case
        ///   does the child join the player's clan.
        /// </summary>
        public static LoverChildClanTarget Resolve(LoverChildFacts facts)
        {
           if (facts == null) return LoverChildClanTarget.LeaveAsIs;

           bool loverHasClan = facts.LoverHasClan;
           bool loverClanIsPlayer = facts.LoverClanIsPlayer;
           bool motherHasClan = facts.MotherHasClan;
           bool motherClanIsPlayer = facts.MotherClanIsPlayer;

            if (loverHasClan && !loverClanIsPlayer) return LoverChildClanTarget.LoverClan;
            if (motherHasClan && !motherClanIsPlayer) return LoverChildClanTarget.MotherClan;
            return LoverChildClanTarget.LeaveAsIs;
        }
    }
}
