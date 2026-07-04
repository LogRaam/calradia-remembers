// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: the player-initiatable gestures a conversation or a real deed can
// offer toward a grudge. Time is deliberately absent here: it is passive decay (GrudgePolicy.
// EffectiveMagnitude already models it), never something the player "does". Unforgivable is likewise
// absent: GrudgeResolutionPolicy.Evaluate rejects any gesture before it ever looks at which one was
// offered.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   A gesture the player can offer toward a grudge's holder, matched against the grudge's
    ///   <see cref="GrudgeResolutionKind" /> by <see cref="GrudgeResolutionPolicy" />. ORDINALS ARE
    ///   FROZEN once the engine wiring persists an in-flight attempt; today this enum is a pure input,
    ///   not yet part of any saved record.
    /// </summary>
    public enum GrudgeGesture
    {
        /// <summary>A sincere apology or conversational making-up.</summary>
        Amends = 0,

        /// <summary>A material gift or payment, sized by <see cref="GrudgeResolutionAttempt.GiftValue" />.</summary>
        Gift = 1,

        /// <summary>A verified deed, a real action in the world in the holder's favour.</summary>
        Deed = 2
    }
}
