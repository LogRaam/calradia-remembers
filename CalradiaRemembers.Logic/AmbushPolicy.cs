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

        /// <summary>The most companions who can stand with the player in a street ambush.</summary>
        public const int MaxCompanionsAtSide = 2;

        // Weighting (tunable): how likely one — or two — companions happen to be at the player's side.
        private const int OnePresentOneJoins = 60;  // 1 companion in the party → 60% they are with you
        private const int ManyPresentTwoJoin = 30;  // 2+ in the party → 30% two are with you …
        private const int ManyPresentOneJoins = 80; // … 30..79 one is with you, else you stand alone

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

        /// <summary>
        ///   How many companions happen to be at the player's side when the knives come out: 0, 1, or 2,
        ///   WEIGHTED by how many companions are actually in the party (more companions → more likely to be
        ///   accompanied, and a chance of two), capped at <see cref="MaxCompanionsAtSide" />. A 0..99 roll.
        /// </summary>
        public static int CompanionsAtPlayerSide(int companionsInParty, int rollPercent)
        {
            if (companionsInParty <= 0) return 0;
            if (companionsInParty == 1) return rollPercent < OnePresentOneJoins ? 1 : 0;

            // Two or more companions in the party.
            if (rollPercent < ManyPresentTwoJoin) return MaxCompanionsAtSide;

            return rollPercent < ManyPresentOneJoins ? 1 : 0;
        }
    }
}
