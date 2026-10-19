// Code written by Gabriel Mailhot, 09/08/2026.
// The captivity marker appended to a kinsman's line in an NPC's RELATIONSHIPS block. A player report
// (09/08/2026): the player held enemy lords prisoner, then met their parent, who called the player a
// liar ("my child is safe within our walls") because the relationships prompt named the captured child
// with no hint they were captive. A parent knows their child was taken, and knows it when they stand
// before the captor. Pure so the exact wording that closes that denial is pinned by a test; the host
// decides isPrisoner and whether the player's own clan is the captor.

namespace CalradiaRemembers.Logic.Captivity
{
    /// <summary>Builds the captivity clause for a kinsman named in an NPC's RELATIONSHIPS block.</summary>
    public static class CaptiveKinDescriber
    {
        /// <summary>The clause when the person the NPC is speaking to (the player) is the captor.</summary>
        public const string HeldByPlayerClause = " (whom you currently hold prisoner)";

        /// <summary>The clause when the kinsman is a prisoner of someone other than the player.</summary>
        public const string HeldByOtherClause = " (currently held prisoner)";

        /// <summary>
        ///   The clause to append after a kinsman's name. Empty when they are free (the common case, so
        ///   ordinary family lines are untouched). When they are a prisoner the clause says so, and names
        ///   the player as the captor when the player's own clan holds them, so an NPC can never truthfully
        ///   deny a captivity the player caused.
        /// </summary>
        public static string Marker(bool isPrisoner, bool heldByPlayer)
        {
            if (!isPrisoner) return "";

            return heldByPlayer
                ? HeldByPlayerClause
                : HeldByOtherClause;
        }
    }
}
