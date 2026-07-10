// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (2026-07-10): after marrying the mother of his child, a player kept receiving child-support
// demand letters from her while she rode in his own party, escalating to a divorce for "unpaid support". Two
// bugs: a legitimate mother (the player's own spouse) should never dun him for support (the child is shared and
// kept in the household, not a debt collected by courier), and a mother riding beside the player should write
// nothing at all (the game already announces the birth; a letter to someone in your tent is absurd). This is the
// pure decision; the birth event wiring and the save-heal for already-queued letters are mod-side.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for which letters a birth to the player's child should trigger.</summary>
    public static class BirthLetterPolicy
    {
        /// <summary>
        ///   A birth announcement letter is sent only when the mother is apart from the player: it is a fond note
        ///   from afar ("you have a son"). When she rides in his own party the game already shows the birth, so a
        ///   letter would be redundant and out of place.
        /// </summary>
        public static bool ShouldSendAnnouncement(bool motherIsWithPlayer)
            => !motherIsWithPlayer;

        /// <summary>
        ///   A child-support request is sent only for a child born OUTSIDE the player's marriage AND only when the
        ///   mother is apart from him. The player's own spouse never demands support for their shared child, and no
        ///   mother sends a courier to someone travelling beside her.
        /// </summary>
        public static bool ShouldRequestChildSupport(bool motherIsWithPlayer, bool isLegitimate)
            => !motherIsWithPlayer && !isLegitimate;
    }
}
