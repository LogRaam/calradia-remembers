// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (2026-07-09): every courier (6 of 6) was waylaid, which is far too frequent and, worse, loses
// the typed letter. Design ruling (Gabriel 2026-07-10): a courier is IGNORED by default, and each bandit party
// it passes gets an independent chance (1 in 3) to turn hostile and give chase, not the whole map at once. A fast
// courier then often outruns even a pursuer, so real interception stays rare and earned. This is the pure part:
// the per-bandit roll; scanning, ignoring, and the chase are engine glue (mod-side).

namespace CalradiaRemembers.Logic
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
    }
}
