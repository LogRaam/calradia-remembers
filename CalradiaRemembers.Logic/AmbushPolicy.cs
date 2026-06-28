// Code written by Gabriel Mailhot, 27/06/2026.
// "Stance with teeth", increment 3 — the town ambush. A lord whose posture resolves to Assassinate
// (see StanceConsequencePolicy) arms an attempt on the player's life; when the player walks a town's streets,
// this decides whether the knives come out this visit, and how many cutthroats the paymaster can field. Pure
// and tunable so the odds stay protected by tests; the trigger, the narration, and the alley fight are host glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>When an armed assassination springs in town, and how heavy it is.</summary>
    public static class AmbushPolicy
    {
        /// <summary>The chance, per eligible town visit, that an armed assassination actually springs. Tuning.</summary>
        public const int SpringChancePercent = 35;

        private const int BaseAttackers = 2; // a paymaster can always scrape together a pair of cutthroats
        private const int MaxAttackers = 6;  // even a great house won't flood a street and risk the watch

        /// <summary>True when a 0..99 roll lands inside the spring chance — the ambush triggers this visit.</summary>
        public static bool ShouldSpring(int rollPercent)
            => rollPercent >= 0 && rollPercent < SpringChancePercent;

        /// <summary>
        ///   How many cutthroats the paymaster fields — scales with the clan's reach (tier), bounded so a
        ///   street ambush stays a street ambush. A negative/odd tier is treated as its floor.
        /// </summary>
        public static int AttackerCount(int paymasterClanTier)
        {
            int n = BaseAttackers + (paymasterClanTier < 0 ? 0 : paymasterClanTier) / 2;

            return n < BaseAttackers
                ? BaseAttackers
                : n > MaxAttackers
                    ? MaxAttackers
                    : n;
        }
    }
}
