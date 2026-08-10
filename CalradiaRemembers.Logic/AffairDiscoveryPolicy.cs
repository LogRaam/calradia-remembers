// Code written by Gabriel Mailhot, 10/08/2026.
// Open relationship (Inc 3b-a): a SECRET affair leaks over time. This pure policy owns the rising discovery
// curve, the chance that a hidden affair is exposed to the player THIS tryst, climbing with the number of
// trysts it has seen (more meetings, more chances of a slip), clamped to a cap so exposure is never a certainty
// in a single week. An OPEN lover is never "discovered" here (he is simply known); an already-known affair
// never re-fires. Get the curve wrong and a hidden affair is exposed at once (no suspense) or never (the
// player's jealousy beat is dead). On discovery the mod surfaces the scandal and sets AffairDiscovered, which a
// later increment (3b-b) gates a confrontation on.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure rules for a SECRET affair being discovered by the player: the tryst-scaled discovery chance and
    ///   the gate that only a hidden, not-yet-known affair can be discovered. Consumed by the mod's tryst pass;
    ///   the host rolls the percent, this holds the bar.
    /// </summary>
    public static class AffairDiscoveryPolicy
    {
        /// <summary>The discovery chance at the first tryst: a hidden affair is unlikely to leak at once.</summary>
        public const int BaseChancePercent = 8;

        /// <summary>Each further tryst adds this much to the discovery chance (more meetings, more risk).</summary>
        public const int PerTrystPercent = 7;

        /// <summary>The discovery ceiling: even a long-running affair is never certain to be exposed in one week.</summary>
        public const int MaxChancePercent = 75;

        /// <summary>
        ///   The percent chance that a secret affair is discovered on a tryst, rising with
        ///   <paramref name="trystCount" />: <see cref="BaseChancePercent" /> at the first tryst plus
        ///   <see cref="PerTrystPercent" /> for each tryst beyond it, clamped to <see cref="MaxChancePercent" />.
        ///   Zero before the first tryst (nothing has happened to leak). Monotonic in the tryst count.
        /// </summary>
        public static int ChancePercent(int trystCount)
        {
            if (trystCount <= 0) return 0;
            int chance = BaseChancePercent + (trystCount - 1) * PerTrystPercent;
            if (chance > MaxChancePercent) chance = MaxChancePercent;
            if (chance < BaseChancePercent) chance = BaseChancePercent;
            return chance;
        }

        /// <summary>
        ///   Whether a secret affair is discovered on this tryst. False unless it is a SECRET affair
        ///   (<paramref name="isSecret" />) that is NOT already known (<paramref name="alreadyKnown" />); then
        ///   true when <paramref name="rollPercent" /> (0..99) falls under <see cref="ChancePercent" /> for
        ///   <paramref name="trystCount" />. An OPEN lover is never discovered here (he is simply known), and an
        ///   already-known affair never re-fires.
        /// </summary>
        public static bool IsDiscovered(bool isSecret, bool alreadyKnown, int trystCount, int rollPercent)
        {
            if (!isSecret) return false;
            if (alreadyKnown) return false;
            return rollPercent < ChancePercent(trystCount);
        }
    }
}
