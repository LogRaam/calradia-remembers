// Code written by Gabriel Mailhot, 10/06/2026.

namespace CalradiaRemembers
{
    /// <summary>
    ///   How readily NPCs grow to like the player through ordinary conversation.
    ///   A player-facing difficulty knob (MCM) that drives all three gating layers at
    ///   once: the change_relation action wording (when the NPC may emit a gain), the
    ///   "deeds over words" stance, and the mechanical <see cref="Mapping.RelationGate"/>
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
