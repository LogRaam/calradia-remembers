// Code written by Gabriel Mailhot, 21/06/2026.
// Protects the NPC-interception decision: who rides out to the player, why, and the guards that stop
// it becoming harassment. The trigger priority and the cooldown/pursuer/notoriety guards break in
// silence — a drift floods the player with challengers, or none ever come.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class InterceptionPolicyTests
    {
        [Test]
        public void GIVEN_a_notorious_player_in_hostile_land_WHEN_choosing_THEN_a_territorial_patrol_rides_out()
        {
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: true, notoriety: 60))
                .Should().Be(InterceptionTrigger.TerritorialPatrol);
        }

        [Test]
        public void GIVEN_notoriety_exactly_at_the_floor_WHEN_choosing_THEN_it_still_triggers()
        {
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: true, notoriety: InterceptionPolicy.NotorietyThreshold))
                .Should().Be(InterceptionTrigger.TerritorialPatrol);
        }

        [Test]
        public void GIVEN_a_nobody_in_hostile_land_WHEN_choosing_THEN_no_one_bothers()
        {
            //Arrange — below the notoriety floor and nothing else applies.
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: true, notoriety: 49))
                .Should().Be(InterceptionTrigger.None);
        }

        [Test]
        public void GIVEN_a_notorious_player_in_hostile_land_but_the_realm_is_at_peace_WHEN_choosing_THEN_no_patrol()
        {
            InterceptionPolicy.Choose(Facts(hostile: false, inTerritory: true, notoriety: 90))
                .Should().Be(InterceptionTrigger.None);
        }

        [Test]
        public void GIVEN_a_hostile_realm_but_the_player_is_elsewhere_WHEN_choosing_THEN_no_patrol()
        {
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: false, notoriety: 90))
                .Should().Be(InterceptionTrigger.None);
        }

        [Test]
        public void GIVEN_a_lord_who_has_heard_of_a_recent_deed_WHEN_choosing_THEN_they_ride_out_to_react()
        {
            InterceptionPolicy.Choose(Facts(heardDeed: true)).Should().Be(InterceptionTrigger.HeardOfDeed);
        }

        [Test]
        public void GIVEN_both_a_territorial_challenge_and_recent_news_WHEN_choosing_THEN_the_patrol_takes_priority()
        {
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: true, notoriety: 70, heardDeed: true))
                .Should().Be(InterceptionTrigger.TerritorialPatrol);
        }

        [Test]
        public void GIVEN_this_lord_is_on_cooldown_WHEN_choosing_THEN_nothing_fires_however_strong_the_trigger()
        {
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: true, notoriety: 90, heardDeed: true, onCooldown: true))
                .Should().Be(InterceptionTrigger.None);
        }

        [Test]
        public void GIVEN_another_pursuer_is_already_active_WHEN_choosing_THEN_no_second_interception_starts()
        {
            InterceptionPolicy.Choose(Facts(hostile: true, inTerritory: true, notoriety: 90, anotherActive: true))
                .Should().Be(InterceptionTrigger.None);
        }

        [Test]
        public void GIVEN_nothing_applies_WHEN_choosing_THEN_the_lord_stays_their_course()
        {
            InterceptionPolicy.Choose(Facts()).Should().Be(InterceptionTrigger.None);
            InterceptionPolicy.WouldIntercept(Facts()).Should().BeFalse();
        }

        [Test]
        public void GIVEN_null_facts_WHEN_choosing_THEN_it_is_a_safe_none()
        {
            InterceptionPolicy.Choose(null).Should().Be(InterceptionTrigger.None);
        }

        // ── ShouldAbandonChase ─────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_chase_past_the_timeout_WHEN_judging_THEN_it_is_abandoned()
        {
            InterceptionPolicy.ShouldAbandonChase(pursuerSpeed: 5f, playerSpeed: 1f, distance: 10f,
                daysPursuing: InterceptionPolicy.MaxPursuitDays).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_player_clearly_faster_and_well_clear_WHEN_judging_THEN_the_hopeless_chase_is_given_up()
        {
            //Arrange — player 25% faster, well beyond the encounter range.
            InterceptionPolicy.ShouldAbandonChase(pursuerSpeed: 4f, playerSpeed: 5f, distance: 20f, daysPursuing: 1)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_player_faster_but_almost_caught_WHEN_judging_THEN_the_pursuer_keeps_pressing()
        {
            //Arrange — within AbandonMinDistance, the encounter is imminent whatever the speed.
            InterceptionPolicy.ShouldAbandonChase(pursuerSpeed: 4f, playerSpeed: 8f, distance: 3f, daysPursuing: 1)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_player_only_marginally_faster_WHEN_judging_THEN_the_chase_continues()
        {
            //Arrange — 4.5/4 = 1.125, below the 1.15 outrun ratio.
            InterceptionPolicy.ShouldAbandonChase(pursuerSpeed: 4f, playerSpeed: 4.5f, distance: 20f, daysPursuing: 1)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_pursuer_at_least_as_fast_as_the_player_WHEN_judging_THEN_they_press_on()
        {
            InterceptionPolicy.ShouldAbandonChase(pursuerSpeed: 5f, playerSpeed: 4f, distance: 30f, daysPursuing: 2)
                .Should().BeFalse();
        }

        #region private

        private static InterceptionFacts Facts(
            bool hostile = false,
            bool inTerritory = false,
            int notoriety = 0,
            bool heardDeed = false,
            bool onCooldown = false,
            bool anotherActive = false)
            => new InterceptionFacts
            {
                FactionHostileToPlayer = hostile,
                PlayerInOwnTerritory   = inTerritory,
                PlayerNotoriety        = notoriety,
                HeardRecentPlayerDeed  = heardDeed,
                OnCooldown             = onCooldown,
                AnotherPursuerActive   = anotherActive
            };

        #endregion
    }
}
