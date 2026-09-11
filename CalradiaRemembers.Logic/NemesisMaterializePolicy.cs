// Code written by Gabriel Mailhot, 08/07/2026.
// Nemesis pillar, ACTIVE HUNT (map-presence mechanic ratified 2026-07-05, player-requested 2026-07-07): the
// nemesis keeps NO standing party on the map (nothing for the AI to attrit, persistence is free). He instead
// MATERIALIZES only at an encounter the PLAYER chose to start: when the player engages a bandit party of his
// band's faction, near the region he haunts, a random chance turns that band out to be led by him. So the hunt
// is emergent and his death is player-gated by construction. This is the pure gate; the world scan, the roster
// query, and attaching/detaching his Hero to the engaged party are host glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure region-proximity gate for the nemesis materializing into a bandit party the player engages. The
    ///   reveal CHANCE itself is the ratified pity-timer in <see cref="NemesisRevealPolicy"/> (rising to a
    ///   certainty), consumed by the host per qualifying encounter; this type only decides whether an engaged
    ///   band is close enough to the nemesis's region to be his in the first place.
    /// </summary>
    public static class NemesisMaterializePolicy
    {
        /// <summary>TUNE: campaign-map radius around the nemesis's region anchor within which an engaged band can be his.</summary>
        public const float RegionMatchRadius = 120f;

        /// <summary>
        ///   True when the engaged band is close enough to the nemesis's region anchor to be his. Distance is
        ///   compared squared (no sqrt). A zero/negative radius never matches; callers with an UNRESOLVABLE
        ///   anchor (a culture-key or "wilds" region, not a real settlement) skip this and match on culture alone.
        ///   <para>
        ///     Takes two positions rather than four loose floats (2026-09-10). The two are safe to sit side by
        ///     side, unlike the coordinates they replace, because the distance between them is the same either
        ///     way round: this is the rare run a swap genuinely cannot hurt, and saying so is the point.
        ///   </para>
        /// </summary>
        public static bool WithinRegion(MapPoint anchor, MapPoint party, float radius)
        {
            if (radius <= 0f) return false;

            return anchor.SquaredDistanceTo(party) <= radius * radius;
        }
    }
}
