// Code written by Gabriel Mailhot, 17/07/2026.
// Bastards (romance audit M-G2): deciding whether a player-parented child is a concealed BASTARD or a legitimate
// (if posthumous) heir. The old test read the player's spouse LIVE at birth: if the spouse died during the
// pregnancy, the surviving parent's Spouse is already null, so a child conceived inside the marriage was tagged
// a concealed bastard, drew seasonal discovery rolls, and cost its own legitimate line renown in a scandal. The
// engine adds a deceased spouse to the survivor's ExSpouses, so "was I married to the other parent" is knowable
// after the death; the one signal that separates that from a plain divorce is whether the ex is now DEAD. Pure
// so this legitimacy line is unit-tested, never re-derived by eye at each call site.

#region

#endregion

namespace CalradiaRemembers.Logic.Bastards
{
    /// <summary>Whether a player-parented child is the player's concealed bastard, given the marriage facts at birth.</summary>
    public static class BastardLegitimacyPolicy
    {
        /// <summary>
        ///   The marriage facts around one child's birth. FIVE booleans in a row, and the last two are the
        ///   whole of the posthumous-heir rule: a former spouse who is DEAD means a widowing, a former
        ///   spouse still alive means a divorce, and swapping them turns an heir into a scandal.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ParentageFacts
        {
           /// <summary>The player is one of the child's parents.</summary>
           public bool PlayerIsParent { get; set; }

           /// <summary>The other parent is known at all.</summary>
           public bool OtherParentExists { get; set; }

           /// <summary>
           ///   The other parent is the player's spouse today, so the child was born within the marriage.
           /// </summary>
           public bool OtherParentIsCurrentSpouse { get; set; }

           /// <summary>
           ///   The other parent is a former spouse. Whether that was a death or a divorce is the next fact.
           /// </summary>
           public bool OtherParentIsFormerSpouse { get; set; }

           /// <summary>
           ///   The other parent still lives. A former spouse now DEAD means the marriage ended by widowing and
           ///   the child is a posthumous heir; a living ex means a divorce, and a child by them is a bastard.
           /// </summary>
           public bool OtherParentAlive { get; set; }
        }

        /// <summary>
        ///   A child is the player's BASTARD only when the player is a parent, the other parent exists, and that
        ///   other parent is NOT the marriage the child belongs to: not the current spouse, and not a former
        ///   spouse the player was WIDOWED from (a former spouse now dead, romance audit M-G2 posthumous heir). A
        ///   former spouse still ALIVE is a divorce, so a child by them IS a bastard (conceived outside a
        ///   marriage). Everything else, a stranger, a lover, a living ex, is a bastard.
        /// </summary>
        public static bool IsPlayersBastard(ParentageFacts facts)
        {
           if (facts == null) return false;

           bool playerIsParent = facts.PlayerIsParent;
           bool otherParentExists = facts.OtherParentExists;
           bool otherParentIsCurrentSpouse = facts.OtherParentIsCurrentSpouse;
           bool otherParentIsFormerSpouse = facts.OtherParentIsFormerSpouse;
           bool otherParentAlive = facts.OtherParentAlive;

            if (!playerIsParent || !otherParentExists) return false;

            if (otherParentIsCurrentSpouse) return false; // born within the current marriage

            if (otherParentIsFormerSpouse && !otherParentAlive) return false; // posthumous child of a marriage death ended

            return true;
        }
    }
}
