// Code written by Gabriel Mailhot, 31/08/2026.
// Player report: the player's wife lost regard and accused him of cheating after EVERY conversation
// with a noblewoman, however innocent, and seemed "psychic", instantly aware of a liaison rumour
// from the far end of the map. The shared rumour scoring (RumorAwareness via WorldEventStore) gives
// a strong same-faction bonus that can push a witnessed flirt rumour over the awareness threshold at
// ANY distance; that scoring serves other systems too, so jealousy adds its OWN earshot gate on top:
// word of the liaison must have reached the jealous party's circles AND they must be close enough to
// where it happened (or riding beside the player) to plausibly have heard it.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure gate deciding whether a jealous party is close enough to a liaison's location to have heard word of it.</summary>
    public static class JealousyHearingPolicy
    {
        /// <summary>
        ///   TUNE: the furthest (map units) a jealous party can be from where the liaison happened and still
        ///   have heard of it. RumorAwareness's geographic bonus falls to zero at 300 units; this adds a 50-unit
        ///   margin beyond it, past this, even same-faction word of mouth has not carried the tale yet.
        /// </summary>
        public const float MaxHearingDistanceUnits = 350f;

        /// <summary>
        ///   True when the jealous party could have heard word of the liaison: always when they ride beside the
        ///   player (the affair happens under their nose), otherwise only when their distance from the liaison's
        ///   location is known and within <see cref="MaxHearingDistanceUnits" />. An unknown distance fails safe
        ///   toward NOT hearing, better a rumour that arrives late (or never) than a spouse who knows instantly
        ///   from across the map.
        /// </summary>
        public static bool WithinEarshot(float? distanceUnits, bool besidePlayer)
            => besidePlayer || (distanceUnits.HasValue && distanceUnits.Value <= MaxHearingDistanceUnits);
    }
}
