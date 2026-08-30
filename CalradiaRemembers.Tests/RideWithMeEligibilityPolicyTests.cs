// Code written by Gabriel Mailhot, 11/08/2026.
// ride_with_me's start gate: EncounterContextBuilder.Build and BannerlordGameStateBridge.ExecuteRideWithMe both
// call RideWithMeEligibilityPolicy.CanRideWithPlayer with the SAME facts, so the prompt teaching and the bridge's
// own re-check can never quietly diverge ("the prompt is advice, the bridge is law"). This is the exact
// complement of EscortEligibilityPolicy.CanEscort: follow_me demands leadsParty == true (their party escorts
// yours), ride_with_me demands leadsParty == false (they ride IN your party, staying in their own clan). These
// tests pin every guard, and above all the MUTUAL EXCLUSIVITY: a party-leading lord must be refused here (they
// are follow_me's job), so the two verbs can never both fire for one hero and a lord's own command is never
// orphaned by taking them into the player's roster. The factionAtWar guard (added 29/08/2026) mirrors
// EscortEligibilityPolicy's own: the author's key concern is a lord who rides along while his own realm fights a
// war, stopping him from answering his liege's muster call and quietly weakening his faction.

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
        // The regard the baseline uses: comfortably above the floor so only the guard under test can fail a case.
        private const int FriendlyRegard = RideWithMeEligibilityPolicy.RegardFloor + 10;

        // The baseline every other test perturbs one guard away from: an idle, alive, non-prisoner other-clan
        // lord at peace who esteems the player must be allowed to ride along, or a player who did everything
        // right could never get an idle lord to accompany them.
        [Test]
        public void GIVEN_every_guard_clear_WHEN_checked_THEN_riding_along_is_allowed()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeTrue();
        }

        // THE mutual-exclusivity guard and the reason this is the exact complement of follow_me: a lord who
        // LEADS their own field party is follow_me's case (their party escorts yours). Taking them into the
        // player's own roster would leave their command orphaned, so ride_with_me must refuse them outright.
        [Test]
        public void GIVEN_the_lord_leads_their_own_party_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: true,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeFalse();
        }

        // "Ride with me for a while" is meant for a lord/lady, not a wanderer or a bandit leading a scratch band;
        // a non-lord must be refused so the offer never appears in the wrong kind of conversation.
        [Test]
        public void GIVEN_the_hero_is_not_a_lord_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: false, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeFalse();
        }

        // A prisoner cannot walk out of their cell to ride at your side; their own path is a freedom bargain or
        // recruit_prisoner, never this verb.
        [Test]
        public void GIVEN_the_lord_is_a_prisoner_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: true, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeFalse();
        }

        // A lord already of the player's own clan rides with them by default; ride_with_me is for an OTHER
        // clan's lord, so a player-clan hero here would be a meaningless (and roster-double-counting) no-op.
        [Test]
        public void GIVEN_the_lord_is_already_of_the_player_clan_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: true, atWar: false, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeFalse();
        }

        // An enemy lord riding INSIDE the very army they may next field against would read as an impossible
        // fiction; the author's flagship rule (also RetainerLeavePolicy) forces a leave on war, so the offer
        // must never be made while at war in the first place.
        [Test]
        public void GIVEN_the_lord_is_at_war_with_the_player_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: true, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeFalse();
        }

        // One retainer arrangement at a time per lord: a second ride_with_me on a lord already riding must not
        // re-take or double-track them.
        [Test]
        public void GIVEN_the_lord_is_already_riding_as_a_retainer_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: true, regard: FriendlyRegard, factionAtWar: false)
                .Should().BeFalse();
        }

        // Regard one point below the floor must refuse: asking a lord to travel at your side is a real ask, so a
        // player who has not earned a close-friend bond cannot conscript a near-stranger to ride along.
        [Test]
        public void GIVEN_regard_is_below_the_floor_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: RideWithMeEligibilityPolicy.RegardFloor - 1, factionAtWar: false)
                .Should().BeFalse();
        }

        // The boundary pins exactly where "earned enough" begins: regard AT the floor must pass, or the
        // documented close-friend bar would silently sit one point higher than intended.
        [Test]
        public void GIVEN_regard_is_exactly_at_the_floor_WHEN_checked_THEN_riding_along_is_allowed()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: RideWithMeEligibilityPolicy.RegardFloor, factionAtWar: false)
                .Should().BeTrue();
        }

        // The author's key concern behind this guard: a lord whose OWN FACTION is at war (even at peace with the
        // player personally, and holding them in high regard) must be refused, or he rides off with the player
        // and stops answering his liege's muster call, quietly weakening his faction mid-war.
        [Test]
        public void GIVEN_the_lords_own_faction_is_at_war_with_anyone_WHEN_checked_THEN_riding_along_is_refused()
        {
            RideWithMeEligibilityPolicy.CanRideWithPlayer(
                    isLord: true, isAlive: true, isPrisoner: false, leadsParty: false,
                    isPlayerClan: false, atWar: false, alreadyRiding: false, regard: FriendlyRegard, factionAtWar: true)
                .Should().BeFalse();
        }
    }
}
