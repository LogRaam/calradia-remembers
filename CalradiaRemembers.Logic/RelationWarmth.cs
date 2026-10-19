// Code written by Gabriel Mailhot, 10/06/2026.
// Moved into CalradiaRemembers.Logic 20/06/2026 so the relation-gating policy it drives
// (RelationGate) can be unit-tested. Namespace kept as CalradiaRemembers so no caller changes, until 19/10/2026: the
// dependency map read every pure policy that takes a warmth as Logic using the MOD's root namespace, a cycle edge the
// projects cannot even have. Now in CalradiaRemembers.Logic like the rest of its project.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How readily NPCs grow to like the player through ordinary conversation.
    ///   A player-facing difficulty knob (MCM) that drives all three gating layers at
    ///   once: the change_relation action wording (when the NPC may emit a gain), the
    ///   "deeds over words" stance, and the mechanical <see cref="Logic.RelationGate"/>
    ///   cooldown/cap. It only loosens POSITIVE gains — insults and betrayals stay exactly
    ///   as punishing at every level.
    /// </summary>
    public enum RelationWarmth
    {
        /// <summary>The level the mod was designed and balanced around. Trust is slow, earned by deeds.</summary>
        Normal,

        /// <summary>Good company counts: compliments, humor, and shared moments earn small gains, more often.</summary>
        Easier,

        /// <summary>NPCs warm readily — most sincerely positive exchanges nudge regard, with little throttle.</summary>
        VeryEasy
    }
}
