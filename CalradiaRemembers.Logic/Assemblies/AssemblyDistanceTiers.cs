// Code written by Gabriel Mailhot, 12/07/2026.
// Buckets a raw map distance (host settlement to invitee) into the DistanceTier the RSVP policy reasons over,
// so "how far is worth the journey" stays a documented, tunable rule instead of magic numbers sprinkled in the
// mod. The thresholds are TUNABLE placeholders (Bannerlord map units), to be dialled in from play-testing; the
// tiers themselves (monotonic, 0 = near ... 3 = far) are what the tests pin.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Maps a raw map distance to a coarse tier (0 near, 1 close, 2 middling, 3 far).</summary>
    public static class AssemblyDistanceTiers
    {
        // ── Tuning (TUNABLE placeholders: map-unit thresholds between tiers) ──

        public const float Tier1Distance = 40f;
        public const float Tier2Distance = 100f;
        public const float Tier3Distance = 180f;

        /// <summary>0 when nearer than Tier1, 1 up to Tier2, 2 up to Tier3, 3 (far) beyond. Never negative.</summary>
        public static int Of(float mapDistance)
        {
            if (mapDistance < Tier1Distance) return 0;
            if (mapDistance < Tier2Distance) return 1;
            if (mapDistance < Tier3Distance) return 2;

            return 3;
        }
    }
}
