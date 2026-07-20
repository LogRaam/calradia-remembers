// Code written by Gabriel Mailhot, 20/07/2026.
// Courier robustness batch (ROADMAP 2026-07-20): when does a courier get more grain. The spawn stock (100
// units, TravelingParty audit C3) covers any normal errand; this top-up exists solely for the pathological
// waits (the M5 landfall vigil for a sailing recipient) that can outlast any fixed stock, because a starved
// party is destroyed by the engine and the player is told a letter was "lost on the road" for no visible reason.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides when a courier's larder is refilled mid-errand.</summary>
    public static class CourierRationPolicy
    {
        /// <summary>
        ///   Only when the larder is EMPTY, never merely low: the spawn stock is the honest travel budget, and
        ///   the top-up is the minimal intervention that guarantees a courier can starve-proof an unbounded
        ///   wait without turning every courier into a bottomless pantry.
        /// </summary>
        public static bool ShouldTopUp(int foodUnitsAtInventory) => foodUnitsAtInventory <= 0;
    }
}
