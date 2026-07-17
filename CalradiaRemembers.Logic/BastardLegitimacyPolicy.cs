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

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether a player-parented child is the player's concealed bastard, given the marriage facts at birth.</summary>
    public static class BastardLegitimacyPolicy
    {
        /// <summary>
        ///   A child is the player's BASTARD only when the player is a parent, the other parent exists, and that
        ///   other parent is NOT the marriage the child belongs to: not the current spouse, and not a former
        ///   spouse the player was WIDOWED from (a former spouse now dead, romance audit M-G2 posthumous heir). A
        ///   former spouse still ALIVE is a divorce, so a child by them IS a bastard (conceived outside a
        ///   marriage). Everything else, a stranger, a lover, a living ex, is a bastard.
        /// </summary>
        public static bool IsPlayersBastard(bool playerIsParent,
                                            bool otherParentExists,
                                            bool otherParentIsCurrentSpouse,
                                            bool otherParentIsFormerSpouse,
                                            bool otherParentAlive)
        {
            if (!playerIsParent || !otherParentExists) return false;

            if (otherParentIsCurrentSpouse) return false; // born within the current marriage

            if (otherParentIsFormerSpouse && !otherParentAlive) return false; // posthumous child of a marriage death ended

            return true;
        }
    }
}
