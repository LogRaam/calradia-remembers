// Code written by Gabriel Mailhot, 29/07/2026.
// Pure tone gate for the KindnessInScene appreciation: a remembered TENDERNESS must actually have been tender.
// The mod plants a "kindness" whenever the LLM tags a turn as an intimacy event, but the model sometimes stamps
// a VIOLENT or COERCED scene as intimacy; without this gate the NPC's encyclopedia then reads "carries a small
// kindness for a tender moment we shared" while the very same turn LOWERED the relation (player report, Nexus:
// a violent encounter, relation -20, yet a tender-moment kindness). A fond appreciation forms only when the
// scene neither lowered the personal relation nor was a captive/coerced one; otherwise the intimacy is still
// recorded as a memory but leaves no fond mark. Because the plant only fires when it IS genuinely tender, the
// existing "a tender moment we shared" wording is then honest.

namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>Pure decision: does an intimacy-tagged scene leave a fondly-remembered KindnessInScene?</summary>
    public static class AppreciationTonePolicy
    {
        /// <summary>
        ///   True only for a GENUINELY tender intimacy: the same turn did not lower the personal relation, and
        ///   the scene was not a captive or captor (coerced) one. A relation-lowering or coerced "intimacy" is a
        ///   memory, not a kindness, so it must not stamp a fond appreciation on the encyclopedia.
        /// </summary>
        public static bool LeavesSceneKindness(bool relationLoweredThisTurn, bool coercedScene)
            => !relationLoweredThisTurn && !coercedScene;
    }
}
