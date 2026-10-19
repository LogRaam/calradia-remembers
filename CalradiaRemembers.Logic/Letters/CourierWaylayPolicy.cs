// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (2026-07-09): every courier (6 of 6) was waylaid, which is far too frequent and, worse, loses
// the typed letter. Design ruling (Gabriel 2026-07-10): a courier is IGNORED by default, and each bandit party
// it passes gets an independent chance (1 in 3) to turn hostile and give chase, not the whole map at once. A fast
// courier then often outruns even a pursuer, so real interception stays rare and earned. This is the pure part:
// the per-bandit roll; scanning, ignoring, and the chase are engine glue (mod-side).

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>Pure rules for whether a single bandit party waylays a passing courier.</summary>
    public static class CourierWaylayPolicy
    {
        /// <summary>TUNE: the chance, per bandit party (rolled once), that it decides to give chase.</summary>
        public const int BanditHostileChancePercent = 33; // roughly 1 in 3

        /// <summary>
        ///   TUNE: given a chase, the chance the band actually catches the courier. The courier is fast, so it
        ///   usually outruns pursuit (the player's "speed should keep them out of trouble"): this is that filter.
        /// </summary>
        public const int ChaseCatchesCourierPercent = 30;

        /// <summary>True when a 0..99 roll lands inside the hostile chance for this bandit party.</summary>
        public static bool ShouldTurnHostile(int rollPercent)
            => rollPercent >= 0 && rollPercent < BanditHostileChancePercent;

        /// <summary>True when a 0..99 roll means a chasing band catches the fast courier (it escapes otherwise).</summary>
        public static bool ChaseCatchesCourier(int rollPercent)
            => rollPercent >= 0 && rollPercent < ChaseCatchesCourierPercent;

        /// <summary>
        ///   CAP: the most bandit bands that may ever roll against a single courier, whatever the trip length.
        ///   Player report (2026-08-05): letters get lost too often (almost every time) when the sender is far
        ///   away. The per-band roll above is INDEPENDENT, so its ~10% per-band loss (33% hostile x 30% catch)
        ///   COMPOUNDS with distance: a short trip passes few bands (low loss), a long trip passes many bands and
        ///   the cumulative loss climbs toward near-certain (10 bands ~65%, 20 bands ~88%). That breaks the
        ///   original design intent that interception stay rare and speed-rewarding. Capping the number of bands
        ///   that ever roll at N=2 makes the worst-case loss distance-independent: 1 - (1 - 0.10)^2 = ~19%,
        ///   regardless of how far the courier must travel.
        /// </summary>
        public const int MaxWaylayAttemptsPerCourier = 2;

        /// <summary>
        ///   True while this courier still has bands left that may roll against it (fewer than
        ///   <see cref="MaxWaylayAttemptsPerCourier" /> have decided so far). Once the budget is spent the
        ///   courier is past the gauntlet and no further band rolls, so long trips no longer compound the loss.
        /// </summary>
        public static bool HasWaylayBudget(int bandsAlreadyRolled)
            => bandsAlreadyRolled < MaxWaylayAttemptsPerCourier;
    }
}
