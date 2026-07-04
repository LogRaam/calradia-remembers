// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, tone arbiter: today the prompt stacks CURRENT STANCE (warmth) + the stance note + the
// grudge note with equal authority, so a concealed vendetta and a stated deep fondness can collide with
// no precedence between them. This owner ruling gives a strong dominant grudge authority over the whole
// conversation's tone: past a threshold it OUTRANKS whatever warmth the NPC's overall regard would
// otherwise show, whether the player knows of the grievance (open coolness) or not (a warm mask over a
// cold, ruling truth).

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   How much a dominant live grudge should govern the NPC's overall tone this conversation, ranked
    ///   from weakest to strongest precedence over stated warmth.
    /// </summary>
    public enum ToneRuling
    {
        /// <summary>No live grudge worth mentioning, or its magnitude sits below <see cref="GrudgeToneArbiter.TemperThreshold" />: warmth reads unshadowed.</summary>
        WarmthUnshadowed = 0,

        /// <summary>Magnitude in [<see cref="GrudgeToneArbiter.TemperThreshold" />, <see cref="GrudgeToneArbiter.DominanceThreshold" />): a soft reserve should show through the warmth.</summary>
        WarmthTempered = 1,

        /// <summary>Magnitude at/above <see cref="GrudgeToneArbiter.DominanceThreshold" />: the grievance sets the tone, outranking stated warmth entirely.</summary>
        GrudgeDominates = 2
    }

    /// <summary>
    ///   Pure precedence rule: whether a dominant live grudge outranks the NPC's overall regard for the
    ///   player's tone this turn, and the directive line to voice when it does. The magnitude thresholds
    ///   alone decide the ruling; <c>reputationWithPlayer</c> is accepted so future callers can log or
    ///   reason about it alongside the ruling, but it does not currently shift either threshold (a
    ///   deliberately narrow first cut — see the owner's "a strong grudge DOMINATES" ruling, which speaks
    ///   only of the grudge's own magnitude).
    /// </summary>
    public static class GrudgeToneArbiter
    {
        /// <summary>Tuning: at/above this effective magnitude, the grievance SETS the tone, outranking warmth entirely.</summary>
        public const int DominanceThreshold = 40;

        /// <summary>Tuning: at/above this effective magnitude (but below <see cref="DominanceThreshold" />), a reserve shadows the warmth.</summary>
        public const int TemperThreshold = 20;

        private const string DominantKnownLine =
            "This grievance outweighs whatever warmth you otherwise feel; let it set your tone before any fondness.";

        private const string DominantHiddenLine =
            "Whatever warmth you show is a mask; underneath, this grievance rules you. Stay courteous, but never warm.";

        private const string TemperedLine =
            "A grievance shadows your regard; let a reserve show through your warmth.";

        /// <summary>
        ///   The tone ruling for this conversation, from the dominant live grudge's effective magnitude
        ///   alone. <paramref name="grudgeKnownToPlayer" /> and <paramref name="reputationWithPlayer" /> do
        ///   not change WHICH band applies (see <see cref="Line" /> for how knownness colours the wording
        ///   once <see cref="ToneRuling.GrudgeDominates" /> is reached).
        /// </summary>
        public static ToneRuling Resolve(int effectiveGrudgeMagnitude, bool grudgeKnownToPlayer, int reputationWithPlayer)
        {
            if (effectiveGrudgeMagnitude >= DominanceThreshold) return ToneRuling.GrudgeDominates;
            if (effectiveGrudgeMagnitude >= TemperThreshold) return ToneRuling.WarmthTempered;

            return ToneRuling.WarmthUnshadowed;
        }

        /// <summary>
        ///   The directive line for a given ruling, or null for <see cref="ToneRuling.WarmthUnshadowed" />
        ///   (nothing needs saying). At <see cref="ToneRuling.GrudgeDominates" />, the wording mirrors the
        ///   knownness mirror the rest of the Grudges pillar already uses: open coolness for a KNOWN
        ///   grudge, a concealment ruling (courteous but never warm) for a HIDDEN one.
        /// </summary>
        public static string Line(ToneRuling ruling, bool grudgeKnownToPlayer)
        {
            switch (ruling)
            {
                case ToneRuling.GrudgeDominates: return grudgeKnownToPlayer ? DominantKnownLine : DominantHiddenLine;
                case ToneRuling.WarmthTempered:  return TemperedLine;
                default:                         return null;
            }
        }
    }
}
