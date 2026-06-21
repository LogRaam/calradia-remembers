// Code written by Gabriel Mailhot, 21/06/2026.
// An NPC's posture toward the player on four orthogonal axes, so dialogue can read believable
// nuance ("dislikes you, yet respects and fears you"). Affection is the existing reputation absorbed
// as an axis (moved only by the gated change_relation path); Trust/Respect/Fear are moved by
// StanceGate — weakly by words, strongly by proven deeds. Pure: a plain, clampable carrier.

namespace CalradiaRemembers.Logic
{
    /// <summary>The four posture axes. Affection/Trust/Respect are bipolar (−100..+100); Fear is unipolar (0..100).</summary>
    public enum StanceAxis
    {
        /// <summary>Fondness ↔ hostility. The existing reputation, moved by change_relation (NOT by StanceGate).</summary>
        Affection,

        /// <summary>Faith in the player's word and deals ↔ suspicion.</summary>
        Trust,

        /// <summary>Esteem for the player's worth and prowess ↔ contempt.</summary>
        Respect,

        /// <summary>Intimidation. Unipolar: 0 = unafraid, 100 = terrified. Decays slowly over time.</summary>
        Fear
    }

    /// <summary>
    ///   A per-NPC stance toward the player. A plain state carrier: <see cref="StanceGate"/> moves it,
    ///   <see cref="StanceDescriptor"/> reads it for the prompt, and the host persists it. Affection
    ///   mirrors the NPC's stored reputation; the rest start neutral.
    /// </summary>
    public sealed class NpcStance
    {
        /// <summary>Fondness ↔ hostility (−100..+100) — sourced from the NPC's reputation.</summary>
        public int Affection { get; set; }

        /// <summary>Faith in the player's word ↔ suspicion (−100..+100).</summary>
        public int Trust { get; set; }

        /// <summary>Esteem for the player's worth ↔ contempt (−100..+100).</summary>
        public int Respect { get; set; }

        /// <summary>Intimidation (0..+100).</summary>
        public int Fear { get; set; }

        /// <summary>
        ///   Campaign-time hour of the last accepted word-round, so spoken influence is rate-limited
        ///   (see <see cref="StanceGate"/>). Null = none yet. Deeds ignore it.
        /// </summary>
        public double? LastWordInfluenceHour { get; set; }
    }
}
