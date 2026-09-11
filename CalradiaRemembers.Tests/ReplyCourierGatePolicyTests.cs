// Code written by Gabriel Mailhot, 20/07/2026.
// Documents the one gate deciding whether a rider can be on the road to the player (courier robustness batch,
// ROADMAP 2026-07-20). Three world states block the ride, and every blocked case resolves the same safe way:
// the letter stays on date-based delivery, so only the visible ride is skipped, never the letter itself.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ReplyCourierGatePolicyTests
    {
        // The open-road case: nothing blocks, the courier rides. If this failed, every reply letter would fall
        // back to the invisible date-based popup and the visible courier feature would be silently dead.
        [Test]
        public void GIVEN_an_ashore_free_player_with_a_road_WHEN_gated_THEN_the_courier_may_ride()
        {
            ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = false, PlayerIsPrisoner = false, LandRouteExists = true})
                .Should().Be(ReplyCourierBlock.None);
        }

        // A sailing player cannot be met by a land rider (tester report: a courier spawned into the water and
        // sat immobile). The ride is skipped, not the letter.
        [Test]
        public void GIVEN_a_player_at_sea_WHEN_gated_THEN_the_ride_is_blocked_as_at_sea()
        {
            ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = true, PlayerIsPrisoner = false, LandRouteExists = true})
                .Should().Be(ReplyCourierBlock.PlayerAtSea);
        }

        // A captive player cannot receive a rider: chasing them means a lone courier trotting into the captor's
        // own party (the gap the Realistic Courier decompile exposed, confirmed in CR and closed by this gate).
        [Test]
        public void GIVEN_a_captive_player_WHEN_gated_THEN_the_ride_is_blocked_as_prisoner()
        {
            ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = false, PlayerIsPrisoner = true, LandRouteExists = true})
                .Should().Be(ReplyCourierBlock.PlayerIsPrisoner);
        }

        // No overland route (an island, a shore across the water): a spawned courier could only starve on the
        // shore, so the ride is refused up front (the island report's lesson).
        [Test]
        public void GIVEN_no_land_route_to_the_player_WHEN_gated_THEN_the_ride_is_blocked_as_no_road()
        {
            ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = false, PlayerIsPrisoner = false, LandRouteExists = false})
                .Should().Be(ReplyCourierBlock.NoLandRoute);
        }

        // Precedence: AT SEA must outrank the road answer, because a land-path query against a player floating
        // on water is meaningless; reporting NoLandRoute there would tell the log (and the player) a false
        // story about the map. Captivity likewise outranks the road: the player's state forbids the ride
        // before the map even gets a say.
        [Test]
        public void GIVEN_several_blocks_at_once_WHEN_gated_THEN_sea_outranks_prisoner_which_outranks_the_road()
        {
            ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = true, PlayerIsPrisoner = true, LandRouteExists = false})
                .Should().Be(ReplyCourierBlock.PlayerAtSea);
            ReplyCourierGatePolicy.Decide(new ReplyCourierGatePolicy.CourierGateFacts {PlayerAtSea = false, PlayerIsPrisoner = true, LandRouteExists = false})
                .Should().Be(ReplyCourierBlock.PlayerIsPrisoner);
        }
    }
}
