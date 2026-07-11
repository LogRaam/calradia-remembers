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
    ///   decide the ruling, but <c>reputationWithPlayer</c> now DOES shift them: a genuinely warm
    ///   relationship (see <see cref="WarmthTemperingFloor" />) raises both bars smoothly, so a real bond
    ///   needs a heavier grudge before it is tempered or dominated. A neutral or cold relationship rules
    ///   exactly as before (see the owner's original "a strong grudge DOMINATES" ruling).
    /// </summary>
    public static class GrudgeToneArbiter
    {
        /// <summary>Tuning: at/above this effective magnitude, the grievance SETS the tone, outranking warmth entirely.</summary>
        public const int DominanceThreshold = 40;

        /// <summary>Tuning: at/above this effective magnitude (but below <see cref="DominanceThreshold" />), a reserve shadows the warmth.</summary>
        public const int TemperThreshold = 20;

        /// <summary>
        ///   Tuning: the personal regard at/above which warmth begins to soften a grudge's grip. Below this
        ///   floor, regard does not change the ruling at all, so a neutral or cold relationship behaves
        ///   exactly as if regard did not exist.
        /// </summary>
        public const int WarmthTemperingFloor = 20;

        /// <summary>
        ///   The number of points by which both <see cref="TemperThreshold" /> and
        ///   <see cref="DominanceThreshold" /> rise for a given personal regard: zero at or below
        ///   <see cref="WarmthTemperingFloor" /> (including all negative regard), then rising point for
        ///   point above it.
        /// </summary>
        public static int RegardTempering(int reputationWithPlayer) => System.Math.Max(0, reputationWithPlayer - WarmthTemperingFloor);

        private const string DominantKnownLine =
            "This grievance outweighs whatever warmth you otherwise feel; let it set your tone before any fondness.";

        private const string DominantHiddenLine =
            "Whatever warmth you show is a mask; underneath, this grievance rules you. Stay courteous, but never warm.";

        private const string TemperedLine =
            "A grievance shadows your regard; let a reserve show through your warmth.";

        /// <summary>
        ///   The tone ruling for this conversation, from the dominant live grudge's effective magnitude
        ///   weighed against a regard-raised pair of bars. <paramref name="reputationWithPlayer" /> raises
        ///   both <see cref="DominanceThreshold" /> and <see cref="TemperThreshold" /> by
        ///   <see cref="RegardTempering" /> points, so a warmer relationship needs a heavier grudge before
        ///   it is tempered or dominated; at or below <see cref="WarmthTemperingFloor" /> the bars sit at
        ///   their base values, unchanged from before. <paramref name="grudgeKnownToPlayer" /> does not
        ///   change WHICH band applies (see <see cref="Line" /> for how knownness colours the wording once
        ///   <see cref="ToneRuling.GrudgeDominates" /> is reached).
        /// </summary>
        public static ToneRuling Resolve(int effectiveGrudgeMagnitude, bool grudgeKnownToPlayer, int reputationWithPlayer)
        {
            int dominance = DominanceThreshold + RegardTempering(reputationWithPlayer);
            int temper = TemperThreshold + RegardTempering(reputationWithPlayer);

            if (effectiveGrudgeMagnitude >= dominance) return ToneRuling.GrudgeDominates;
            if (effectiveGrudgeMagnitude >= temper) return ToneRuling.WarmthTempered;

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
