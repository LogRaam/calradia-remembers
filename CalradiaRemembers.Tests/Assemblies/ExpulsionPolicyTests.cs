// Code written by Gabriel Mailhot, 04/08/2026.
// Documents expel_from_clan's pure gate: whether the player may cast a seated companion out of their own clan
// right now. The stake across every test here: expulsion is one of the gravest social costs the table can hand
// down (COUNCIL_ACTIONS.md's Kimi review, Partie 8), so getting the AND wrong either lets a non-leader force out
// a companion that was never theirs to dismiss, exposes a marriage-ending loophole through the wrong channel, or
// refuses a legal expulsion the lift should have carried out.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class ExpulsionPolicyTests
    {
        // The ordinary case: a clan leader casting out a live companion who is not their own spouse is legal.
        [Test]
        public void GIVEN_a_clan_leader_and_a_live_companion_WHEN_asked_to_expel_THEN_it_is_legal()
        {
            ExpulsionPolicy.CanExpel(playerIsClanLeader: true, targetIsCompanion: true, targetIsSpouse: false, targetAlive: true)
                .Should().BeTrue();
        }

        // A companion's hire/fire is a genuine CLAN-internal decision (unlike FiefTransferPolicy's cross-clan
        // sovereignty bar): without the player actually LEADING their own clan, expulsion is a hollow threat the
        // lift must refuse, never a consequence-free tyranny button for a member who is not even the chief.
        [Test]
        public void GIVEN_the_player_does_not_lead_the_clan_WHEN_asked_to_expel_THEN_it_is_refused()
        {
            ExpulsionPolicy.CanExpel(playerIsClanLeader: false, targetIsCompanion: true, targetIsSpouse: false, targetAlive: true)
                .Should().BeFalse();
        }

        // The target must be a REAL companion of the player's clan right now: this build's own v1 restriction
        // (COUNCIL_ACTIONS.md's family-member case is deliberately deferred, see ExpulsionPolicy's own header),
        // so a non-companion (family, a fief-holding lord, a stranger) must always be refused.
        [Test]
        public void GIVEN_the_target_is_not_a_companion_WHEN_asked_to_expel_THEN_it_is_refused()
        {
            ExpulsionPolicy.CanExpel(playerIsClanLeader: true, targetIsCompanion: false, targetIsSpouse: false, targetAlive: true)
                .Should().BeFalse();
        }

        // The player's own spouse has its own dissolution channel (end_marriage/divorce): expulsion must never
        // become a shortcut around that machinery, so a spousal target is refused even if every other condition
        // holds.
        [Test]
        public void GIVEN_the_target_is_the_players_own_spouse_WHEN_asked_to_expel_THEN_it_is_refused()
        {
            ExpulsionPolicy.CanExpel(playerIsClanLeader: true, targetIsCompanion: true, targetIsSpouse: true, targetAlive: true)
                .Should().BeFalse();
        }

        // A dead companion cannot be cast out of anything: refuse rather than pretend the deed lands on nobody.
        [Test]
        public void GIVEN_the_target_is_not_alive_WHEN_asked_to_expel_THEN_it_is_refused()
        {
            ExpulsionPolicy.CanExpel(playerIsClanLeader: true, targetIsCompanion: true, targetIsSpouse: false, targetAlive: false)
                .Should().BeFalse();
        }

        // Every condition must hold at once: this pins the AND, not an OR, is the whole point of the gate.
        [Test]
        public void GIVEN_no_condition_holds_WHEN_asked_to_expel_THEN_it_is_refused()
        {
            ExpulsionPolicy.CanExpel(playerIsClanLeader: false, targetIsCompanion: false, targetIsSpouse: true, targetAlive: false)
                .Should().BeFalse();
        }

        // The grudge magnitude and relation hit are documented, testable constants (COUNCIL_ACTIONS.md's Kimi
        // review: 50-70 grudge magnitude), not magic numbers buried in the lift executor: pin them here so a
        // future tuning change is a one-line, reviewed decision, not a silent drift. Deliberately below
        // FiefTransferPolicy's own maximum-tier revoke_fief figures (100 grudge / -50 relation): Gabriel's own
        // words reserve that ceiling for stripping a vassal's fief specifically.
        [Test]
        public void GIVEN_the_expulsion_tuning_constants_WHEN_read_THEN_they_match_the_documented_design()
        {
            ExpulsionPolicy.GrudgeMagnitude.Should().BeInRange(50, 70);
            ExpulsionPolicy.GrudgeMagnitude.Should().BeLessThan(FiefTransferPolicy.RevokeGrudgeMagnitude);
            ExpulsionPolicy.RelationHit.Should().BeLessThan(0);
            ExpulsionPolicy.RelationHit.Should().BeGreaterThan(FiefTransferPolicy.RevokeRelationHit);
        }
    }
}
