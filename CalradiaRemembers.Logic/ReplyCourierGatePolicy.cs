// Code written by Gabriel Mailhot, 20/07/2026.
// Courier robustness batch (ROADMAP 2026-07-20): the ONE gate deciding whether a rider can be on the road to
// the player at all. Three world states and the player's own word block it, and every blocked case resolves the same safe way: the letter
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
        NoLandRoute,

        /// <summary>The player wants no rider for this letter: riders off in the MCM, or a letter that would arrive unseen.</summary>
        NotWanted
    }

    /// <summary>Single source of truth for the reply-courier road gate.</summary>
    public static class ReplyCourierGatePolicy
    {
        /// <summary>
        ///   Where the player is, and whether a rider could reach them. Three booleans in a row read in a
        ///   PRECEDENCE that is itself the rule: at sea outranks everything, because a land path queried
        ///   against a sea point is meaningless and its answer must never be the reason the player is told;
        ///   captivity outranks the road, because the player's state, not the map, is what forbids the ride.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class CourierGateFacts
        {
           /// <summary>The player is on the water. Checked first, and outranks everything.</summary>
           public bool PlayerAtSea { get; set; }

           /// <summary>The player is held. Outranks the road.</summary>
           public bool PlayerIsPrisoner { get; set; }

           /// <summary>A rider could actually get there overland.</summary>
           public bool LandRouteExists { get; set; }

           /// <summary>The player asked for no rider (kurushimi00, 31/10/2026). Outranks everything: it is the true reason.</summary>
           public bool NoRiderWanted { get; set; }
        }

        /// <summary>
        ///   Precedence is deliberate: AT SEA outranks everything (a land path queried against a sea point is
        ///   meaningless, so its answer must never be the reported reason), captivity outranks the road (the
        ///   player's state, not the map, is what forbids the ride).
        /// </summary>
        public static ReplyCourierBlock Decide(CourierGateFacts facts)
        {
           if (facts == null) return ReplyCourierBlock.None;
           if (facts.NoRiderWanted) return ReplyCourierBlock.NotWanted;

           bool playerAtSea = facts.PlayerAtSea;
           bool playerIsPrisoner = facts.PlayerIsPrisoner;
           bool landRouteExists = facts.LandRouteExists;

            if (playerAtSea) return ReplyCourierBlock.PlayerAtSea;
            if (playerIsPrisoner) return ReplyCourierBlock.PlayerIsPrisoner;

            return landRouteExists ? ReplyCourierBlock.None : ReplyCourierBlock.NoLandRoute;
        }
    }
}
