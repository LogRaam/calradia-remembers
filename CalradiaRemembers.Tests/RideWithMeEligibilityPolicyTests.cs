// Code written by Gabriel Mailhot, 2026, reworked 10/09/2026.
// What this pins: when an idle lord or lady will ride inside the player's own party for a time, keeping their
// own clan, and the many ways they will not.
//
// ride_with_me is the exact COMPLEMENT of follow_me: that verb is for a lord who leads his own party and escorts
// the player's with it; this one is for a lord who leads none and rides along inside the player's. The inverted
// party fact is what keeps the two from ever both applying to the same person.
//
// WHY EACH TEST NOW NAMES ONE FACT. Until today the policy took nine parameters, eight of them booleans, and
// every test spelled out all nine to say something about one. Gabriel, 10/09/2026: "Le code doit pouvoir changer,
// evoluer, s'ameliorer, mais le comportement attendu ne doit jamais se degrader." Its twin had just cost seven
// rewrites for a signature change that altered nothing a player would notice, so both now take a facts object
// and both sets of tests name only what they are about.
//
// NO BEHAVIOUR CHANGED HERE, and one thing deliberately did not. RealmAtWar still refuses, where its twin stopped
// refusing on 2026-09-10: an escorting lord who is summoned has a party for the muster to reclaim and the escort
// ENDS, saying why; a retainer has no party by definition, so nothing reclaims him and nothing ends it. Same
// words, different situation, awaiting its own ruling.
//
// If these tests fail, either an idle lord has stopped agreeing to ride, or one is about to ride away from a war
// his own realm is fighting with no way back.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RideWithMeEligibilityPolicyTests
    {
        /// <summary>Comfortably above the floor, so only the fact under test can ever be the one that refuses.</summary>
        private const int FriendlyRegard = RideWithMeEligibilityPolicy.RegardFloor + 10;

        /// <summary>An idle, living, other-clan lord at peace who esteems the player. Each test spoils one fact.</summary>
        private static RideWithMeEligibilityPolicy.RideWithMeFacts AnIdleFriendlyLord()
            => new() {IsLord = true, IsAlive = true, Regard = FriendlyRegard};

        // The one case proving the others are not all refusals: a lord with nothing in the way rides along, or a
        // player who did everything right could never get anyone to accompany them.
        [Test]
        public void GIVEN_an_idle_lord_who_esteems_the_player_WHEN_asked_THEN_he_rides_along()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(AnIdleFriendlyLord()).Should().BeTrue();
        }

        // THE mutual-exclusivity guard, and the reason this is follow_me's exact complement: a lord who leads his
        // own field party is that verb's case. Taking him into the player's roster would orphan his command.
        [Test]
        public void GIVEN_he_leads_his_own_party_WHEN_asked_THEN_that_is_follow_me_s_business_not_this_one()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts facts = AnIdleFriendlyLord();
            facts.LeadsOwnParty = true;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(facts).Should().BeFalse();
        }

        // "Ride with me for a while" is an offer made to a lord or lady, not to a wanderer or a bandit chief.
        [Test]
        public void GIVEN_the_hero_is_no_lord_at_all_WHEN_asked_THEN_the_offer_does_not_apply_to_them()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts facts = AnIdleFriendlyLord();
            facts.IsLord = false;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(facts).Should().BeFalse();
        }

        // The dead and the captive ride nowhere.
        [Test]
        public void GIVEN_he_is_dead_or_in_a_cell_WHEN_asked_THEN_he_rides_nowhere()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts dead = AnIdleFriendlyLord();
            dead.IsAlive = false;
            RideWithMeEligibilityPolicy.RideWithMeFacts captive = AnIdleFriendlyLord();
            captive.IsPrisoner = true;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(dead).Should().BeFalse();
            RideWithMeEligibilityPolicy.CanRideWithPlayer(captive).Should().BeFalse();
        }

        // A lord of the player's own clan already rides with them by default; there is nothing here to arrange.
        [Test]
        public void GIVEN_he_is_already_of_the_player_s_own_clan_WHEN_asked_THEN_there_is_nothing_to_arrange()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts facts = AnIdleFriendlyLord();
            facts.IsPlayerClan = true;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(facts).Should().BeFalse();
        }

        // No lord rides inside the very army his own realm may fight tomorrow.
        [Test]
        public void GIVEN_their_two_realms_are_at_war_WHEN_asked_THEN_he_will_not_ride_inside_that_army()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts facts = AnIdleFriendlyLord();
            facts.AtWarWithPlayer = true;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(facts).Should().BeFalse();
        }

        // One retainer arrangement at a time.
        [Test]
        public void GIVEN_he_already_rides_as_a_retainer_WHEN_asked_THEN_he_will_not_promise_it_twice()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts facts = AnIdleFriendlyLord();
            facts.AlreadyRiding = true;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(facts).Should().BeFalse();
        }

        // The bond that travelling at someone's side actually costs, and its boundary: at the floor he agrees, a
        // point below it he does not. Riding along is a real ask, deliberately dearer than a favour and cheaper
        // than forsaking one's own house.
        [Test]
        public void GIVEN_the_bond_is_one_short_of_what_he_asks_WHEN_asked_THEN_he_declines_until_it_reaches_it()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts below = AnIdleFriendlyLord();
            below.Regard = RideWithMeEligibilityPolicy.RegardFloor - 1;
            RideWithMeEligibilityPolicy.RideWithMeFacts atTheFloor = AnIdleFriendlyLord();
            atTheFloor.Regard = RideWithMeEligibilityPolicy.RegardFloor;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(below).Should().BeFalse();
            RideWithMeEligibilityPolicy.CanRideWithPlayer(atTheFloor).Should().BeTrue();
        }

        // Still a refusal here, where its twin stopped refusing on 2026-09-10. An escorting lord who is summoned
        // has a party for the muster to reclaim, and the escort ends with him saying why; a retainer has no party
        // by the very precondition above, so nothing reclaims him and nothing ends the arrangement. Dropping this
        // would let him ride the whole term while his realm fights, with no way out.
        [Test]
        public void GIVEN_his_own_realm_is_at_war_WHEN_asked_THEN_he_stays_available_for_the_muster()
        {
            RideWithMeEligibilityPolicy.RideWithMeFacts facts = AnIdleFriendlyLord();
            facts.RealmAtWar = true;

            RideWithMeEligibilityPolicy.CanRideWithPlayer(facts).Should().BeFalse();
        }

        // Facts we could not read are not permission: the safe answer to "I do not know" is that nobody rides.
        [Test]
        public void GIVEN_nothing_could_be_read_about_him_WHEN_asked_THEN_nobody_rides()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(null).Should().BeFalse();
        }
    }
}
