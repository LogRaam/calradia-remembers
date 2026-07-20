// Code written by Gabriel Mailhot, 20/07/2026.
// Courier robustness batch (ROADMAP 2026-07-20): the ONE gate deciding whether a rider can be on the road to
// the player at all. Three world states block it, and every blocked case resolves the same safe way: the letter
// stays on (or reverts to) date-based delivery, so nothing is ever lost, only the visible ride is skipped.
// Used at BOTH sites: refusing to spawn a new reply courier, and reclaiming one already in flight.

namespace CalradiaRemembers.Logic
{
    /// <summary>Why no reply courier may ride to the player right now (None = the road is open).</summary>
    public enum ReplyCourierBlock
    {
        /// <summary>The road is open; the courier may ride.</summary>
        None,

        /// <summary>The player is sailing: a land courier cannot meet them on the water.</summary>
        PlayerAtSea,

        /// <summary>The player is a prisoner: chasing them means riding into the captor's own party.</summary>
        PlayerIsPrisoner,

        /// <summary>No overland route reaches the player (an island, a shore across the water).</summary>
        NoLandRoute
    }

    /// <summary>Single source of truth for the reply-courier road gate.</summary>
    public static class ReplyCourierGatePolicy
    {
        /// <summary>
        ///   Precedence is deliberate: AT SEA outranks everything (a land path queried against a sea point is
        ///   meaningless, so its answer must never be the reported reason), captivity outranks the road (the
        ///   player's state, not the map, is what forbids the ride).
        /// </summary>
        public static ReplyCourierBlock Decide(bool playerAtSea, bool playerIsPrisoner, bool landRouteExists)
        {
            if (playerAtSea) return ReplyCourierBlock.PlayerAtSea;
            if (playerIsPrisoner) return ReplyCourierBlock.PlayerIsPrisoner;

            return landRouteExists ? ReplyCourierBlock.None : ReplyCourierBlock.NoLandRoute;
        }
    }
}
